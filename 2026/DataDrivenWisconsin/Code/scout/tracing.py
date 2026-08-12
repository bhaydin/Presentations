"""
Minimal OTel-shaped tracing. Spans go to a JSONL file, which is deliberate:
a trace is just another fact table. If you can land it in your lakehouse you
can query agent behavior with the same SQL you use for everything else.

The printer is tuned for a projector at the back of a wide room. Big glyphs,
high contrast, no more than ~72 columns.
"""

from __future__ import annotations

import json
import textwrap
import time
import uuid
from contextlib import contextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

TRACE_PATH = Path(__file__).resolve().parent.parent / "traces.jsonl"

# Set False if your terminal theme fights these.
USE_COLOR = True

# Everything printed anywhere in this demo stays inside this many columns.
# A 20pt terminal at the back of a wide room is the constraint, and the answer
# line -- "be in the stand at eleven" -- is the line that must not wrap badly.
SCREEN_WIDTH = 72

# Span attributes indent further, so they get a little less room.
WRAP_WIDTH = 70

# Longest attribute value we will print before giving up and ellipsizing.
# The SQL attribute is the reason this is generous: on stage you point at the
# trace and say "I can read the exact query that hit the warehouse." That claim
# has to be true, so the query gets wrapped across lines rather than truncated.
MAX_ATTR_LINES = 8

_C = {
    "reset": "\033[0m",
    "dim": "\033[2m",
    "bold": "\033[1m",
    "red": "\033[91m",
    "green": "\033[92m",
    "yellow": "\033[93m",
    "cyan": "\033[96m",
}


def c(text: str, *styles: str) -> str:
    if not USE_COLOR:
        return text
    return "".join(_C[s] for s in styles) + text + _C["reset"]


def _attribute_lines(indent: str, key: str, value: Any) -> list[str]:
    """
    Render one span attribute as one or more display lines.

    Long values WRAP rather than truncate. That matters for the `sql`
    attribute specifically: the whole point of putting the query in the trace
    is that a human can read what actually hit the warehouse. A value clipped
    at "SELECT EXTRACT(hour FROM d.captured_at) AS hour_of_..." proves nothing.
    """
    prefix = f"{indent}    {key} = "
    continuation = " " * (len(indent) + 6)

    # Collapse any embedded newlines/indentation so wrapping is predictable.
    body = " ".join(str(value).split())
    if not body:
        return [prefix]

    lines = textwrap.wrap(
        body,
        width=WRAP_WIDTH,
        initial_indent=prefix,
        subsequent_indent=continuation,
        break_long_words=False,
        break_on_hyphens=False,
    )
    if len(lines) > MAX_ATTR_LINES:
        lines = lines[:MAX_ATTR_LINES]
        lines[-1] += " ..."
    return lines


@dataclass
class Span:
    name: str
    kind: str
    span_id: str
    parent_id: str | None
    start: float
    attributes: dict[str, Any] = field(default_factory=dict)
    duration_ms: float = 0.0
    status: str = "OK"
    depth: int = 0


class Tracer:
    def __init__(self, trace_name: str) -> None:
        self.trace_id = uuid.uuid4().hex[:12]
        self.trace_name = trace_name
        self.spans: list[Span] = []
        self._stack: list[Span] = []

    @contextmanager
    def span(self, name: str, kind: str = "internal", **attributes: Any):
        s = Span(
            name=name,
            kind=kind,
            span_id=uuid.uuid4().hex[:8],
            parent_id=self._stack[-1].span_id if self._stack else None,
            start=time.perf_counter(),
            attributes=dict(attributes),
            depth=len(self._stack),
        )
        self.spans.append(s)
        self._stack.append(s)
        try:
            yield s
        except Exception as exc:
            # An approval hold is NOT an error -- it is the control plane doing
            # its job. Distinguishing these matters: if held writes show up in
            # your error rate, someone will "fix" the gate to clean the dashboard.
            if type(exc).__name__ == "ApprovalRequired":
                s.status = "HELD"
            elif type(exc).__name__ == "KillSwitchEngaged":
                s.status = "STOPPED"
            else:
                s.status = f"ERROR: {type(exc).__name__}"
            raise
        finally:
            s.duration_ms = (time.perf_counter() - s.start) * 1000
            self._stack.pop()

    def flush(self) -> None:
        with TRACE_PATH.open("a") as fh:
            for s in self.spans:
                fh.write(
                    json.dumps(
                        {
                            "trace_id": self.trace_id,
                            "trace_name": self.trace_name,
                            "span_id": s.span_id,
                            "parent_id": s.parent_id,
                            "name": s.name,
                            "kind": s.kind,
                            "duration_ms": round(s.duration_ms, 2),
                            "status": s.status,
                            "attributes": s.attributes,
                        }
                    )
                    + "\n"
                )

    # ------------------------------------------------------------------
    # Projector output
    # ------------------------------------------------------------------

    def print_tree(self, highlight: str | None = None) -> None:
        """
        highlight: substring of an attribute value to flag in yellow. Use it to
        point at the ONE field that explains the failure.
        """
        print()
        print(c("  TRACE " + self.trace_id, "bold", "cyan"), c(self.trace_name, "dim"))
        print(c("  " + "-" * 68, "dim"))

        for s in self.spans:
            indent = "  " + ("  " * s.depth)
            glyph = {
                "llm": "◆",
                "tool": "▸",
                "sql": "▪",
                "policy": "⛔",
                "internal": "·",
            }.get(s.kind, "·")

            status_mark = ""
            if s.status in ("HELD", "STOPPED"):
                status_mark = "  " + c(s.status, "yellow", "bold")
            elif s.status != "OK":
                status_mark = "  " + c(s.status, "red", "bold")
            elif s.kind == "policy":
                status_mark = "  " + c(s.attributes.get("decision", ""), "yellow", "bold")

            print(
                f"{indent}{glyph} {c(s.name, 'bold')}"
                f"  {c(f'{s.duration_ms:.0f}ms', 'dim')}{status_mark}"
            )

            for k, v in s.attributes.items():
                if k == "decision":
                    continue
                flagged = bool(highlight) and highlight in str(v)
                for line in _attribute_lines(indent, k, v):
                    print(c(line, "yellow", "bold") if flagged else c(line, "dim"))
        print(c("  " + "-" * 68, "dim"))
        total = sum(s.duration_ms for s in self.spans if s.depth == 0)
        tool_calls = sum(1 for s in self.spans if s.kind == "tool")
        sql_calls = sum(1 for s in self.spans if s.kind == "sql")
        errors = sum(1 for s in self.spans if s.status.startswith("ERROR"))
        held = sum(1 for s in self.spans if s.status in ("HELD", "STOPPED"))

        footer = (
            f"  {tool_calls} tool calls   {sql_calls} queries   "
            f"{total:.0f}ms total   {errors} errors"
        )
        if held:
            footer += f"   {held} held by policy"

        # When errors == 0 this line is the whole argument. Make it readable,
        # not dim -- you want the room to see "0 errors" under a wrong answer.
        print(c(footer, "bold") if errors == 0 and not held else c(footer, "dim"))
        print()
