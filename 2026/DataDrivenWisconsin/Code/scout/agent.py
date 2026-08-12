"""
Scout: a scouting analyst agent.

    "Given twelve cameras, a wind forecast, and four seasons of history,
     where and when do I sit?"

TWO PLANNERS
    stub   deterministic keyword router. No network, no API key, instant,
           byte-identical every run. This is what you present with.
    azure  real tool-calling loop against Azure OpenAI. Set AZURE_OPENAI_ENDPOINT,
           AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT.

BE HONEST ON STAGE ABOUT THIS. Say: "the planner is stubbed so this runs on
airplane wifi -- the tools, the warehouse, the traces and the evals are real."
Nobody will hold it against you. The failure being demonstrated is a DATA
failure, and swapping in a real model does not change it one bit. That is
itself the point: a smarter model does not save you from bad timestamps.

    python -m scout.agent "what time should I sit Bean Field in October 2025?"
"""

from __future__ import annotations

import os
import re
import sys
import textwrap
from dataclasses import dataclass, field
from typing import Any

from scout import controls, tools
from scout.tracing import SCREEN_WIDTH, Tracer, c

MONTHS = {
    "january": 1, "february": 2, "march": 3, "april": 4, "may": 5, "june": 6,
    "july": 7, "august": 8, "september": 9, "october": 10, "november": 11,
    "december": 12, "sept": 9, "oct": 10, "nov": 11, "dec": 12,
}

STAND_ALIASES = {
    "bean field": "Bean Field", "beanfield": "Bean Field", "beans": "Bean Field",
    "ridge": "Ridge", "creek bottom": "Creek Bottom", "creek": "Creek Bottom",
    "oak flat": "Oak Flat", "oaks": "Oak Flat", "cedar swamp": "Cedar Swamp",
    "swamp": "Cedar Swamp", "power line": "Power Line", "powerline": "Power Line",
}

CARDINALS = list(tools.CARDINAL_DEG)

# Topics Scout must refuse rather than guess at. Regulatory answers change by
# year, county and weapon; a confident wrong answer here is a legal problem.
REGULATION_TERMS = [
    "legal shooting hours", "shooting hours", "legal hours", "regulation",
    "regulations", "tag", "license", "bag limit", "cwd", "baiting",
    "is it legal", "am i allowed",
]


def _wrap_for_screen(text: str, prefix_width: int = 5) -> list[str]:
    """Break one long string into projector-width lines. Never returns []."""
    return textwrap.wrap(text, width=SCREEN_WIDTH - prefix_width) or [""]


@dataclass
class AgentResult:
    question: str
    intent: str
    answer: str
    structured: dict[str, Any] = field(default_factory=dict)
    deferred: bool = False
    approval_required: bool = False
    approval_id: str | None = None
    error: str | None = None
    tracer: Tracer | None = None

    def print_answer(self) -> None:
        """
        Print the Q and the A, wrapped to fit a projector.

        The answer line is the most important line in the whole demo -- it is
        the "be in the stand at eleven" moment. Unwrapped it runs to about 150
        columns, which at 20pt either wraps somewhere ugly or walks off the
        right edge of the screen. So it wraps here, at a width the rest of the
        output already respects.
        """
        # Colour is applied per line, AFTER wrapping. Wrapping coloured text
        # would count the invisible escape codes toward the line width and the
        # lines would come out far too short.
        style = "red" if self.error else ("yellow" if self.approval_required else None)

        print()
        for i, line in enumerate(_wrap_for_screen(self.question)):
            print(c("  Q  " if i == 0 else "     ", "dim") + c(line, "bold"))
        for i, line in enumerate(_wrap_for_screen(self.answer)):
            body = c(line, style) if style else line
            print(c("  A  " if i == 0 else "     ", "dim") + body)
        print()


# ----------------------------------------------------------------------
# Slot extraction
# ----------------------------------------------------------------------

def _extract(question: str) -> dict[str, Any]:
    q = question.lower()
    slots: dict[str, Any] = {}

    for alias, canonical in STAND_ALIASES.items():
        if alias in q:
            slots["stand"] = canonical
            break

    m = re.search(r"camera\s*#?\s*(\d+)", q)
    if m:
        slots["camera_id"] = int(m.group(1))

    m = re.search(r"\b(20\d\d)\b", q)
    if m:
        slots["year"] = int(m.group(1))

    for name, num in MONTHS.items():
        if re.search(rf"\b{name}\b", q):
            slots["month"] = num
            break

    if any(w in q for w in ("buck", "bucks", "antlered")):
        slots["antlered_only"] = True

    for species in ("turkey", "coyote", "raccoon", "bear", "squirrel"):
        if species in q:
            slots["species"] = {"bear": "black bear"}.get(species, species)
            break

    # Wind: match longest cardinal first so "SSW" beats "S".
    for card in sorted(CARDINALS, key=len, reverse=True):
        if re.search(rf"\b{card.lower()}\b(?:\s*wind|erly)?", q):
            slots["wind"] = card
            break
    for word, card in (("north", "N"), ("south", "S"), ("east", "E"), ("west", "W")):
        if "wind" not in slots and re.search(rf"\b{word}(?:erly)?\b", q):
            slots["wind"] = card
            break

    if "evening" in q or "dusk" in q or "afternoon" in q:
        slots["window"] = "evening"
    elif "morning" in q or "dawn" in q or "sunrise" in q:
        slots["window"] = "morning"

    return slots


def _classify(question: str) -> str:
    q = question.lower()
    if any(t in q for t in REGULATION_TERMS):
        return "REGULATION"
    if any(t in q for t in ("text ", "notify", "let the crew", "tell justin",
                            "tell kurt", "message the crew", "tell the crew",
                            "send the crew")):
        return "NOTIFY"
    # PEAK must be tested BEFORE wind: "what time should I sit" contains
    # "should i sit" and would otherwise be routed to the wind rule engine.
    if any(t in q for t in ("what time", "peak", "busiest", "most active",
                            "when should i", "what hour", "best hour")):
        return "PEAK"
    if "wind" in q or q.startswith("can i sit") or "should i sit" in q:
        return "WIND"
    return "COUNT"


# ----------------------------------------------------------------------
# Stub planner
# ----------------------------------------------------------------------

def _run_stub(question: str, tracer: Tracer) -> AgentResult:
    intent = _classify(question)
    slots = _extract(question)

    with tracer.span(
        "planner", kind="llm", planner="stub", intent=intent, slots=slots
    ):
        pass

    try:
        if intent == "REGULATION":
            with tracer.span("policy.scope_check", kind="policy") as span:
                span.attributes["decision"] = "DEFERRED"
                span.attributes["reason"] = "regulatory question -- outside agent scope"
            return AgentResult(
                question=question,
                intent=intent,
                answer=(
                    "I won't answer that one. Shooting hours, tags and bag limits "
                    "change by year, county and weapon, and I have no authoritative "
                    "source loaded. Check the current Wisconsin DNR regulations."
                ),
                deferred=True,
                tracer=tracer,
            )

        if intent == "NOTIFY":
            try:
                tools.notify_crew(tracer, message=question)
            except controls.ApprovalRequired as exc:
                return AgentResult(
                    question=question,
                    intent=intent,
                    answer=(
                        f"Held for your approval ({exc.approval_id}). I don't send "
                        "messages to people on my own authority."
                    ),
                    approval_required=True,
                    approval_id=exc.approval_id,
                    tracer=tracer,
                )

        if intent == "WIND":
            stand = slots.get("stand")
            wind = slots.get("wind")
            if not stand or not wind:
                return AgentResult(
                    question=question, intent=intent,
                    answer="I need both a stand and a wind direction.",
                    error="missing slots", tracer=tracer,
                )
            r = tools.check_wind_compatibility(tracer, stand, wind)
            return AgentResult(
                question=question, intent=intent,
                answer=(
                    f"{r['verdict']} -- {stand} wants a {r['ideal_wind']} wind and "
                    f"you've got {r['actual_wind']}, {r['degrees_off']} degrees off. "
                    f"{r['detail']}."
                ),
                structured=r, tracer=tracer,
            )

        if intent == "PEAK":
            stand = slots.get("stand")
            if not stand:
                return AgentResult(
                    question=question, intent=intent,
                    answer="Which stand?", error="missing stand", tracer=tracer,
                )
            r = tools.peak_activity_hour(
                tracer,
                stand=stand,
                year=slots.get("year", 2025),
                month=slots.get("month", 10),
                window=slots.get("window", "morning"),
                species=slots.get("species", "whitetail deer"),
            )
            if r["peak_hour"] is None:
                return AgentResult(
                    question=question, intent=intent,
                    answer="No detections match that window.",
                    structured=r, tracer=tracer,
                )
            hour = r["peak_hour"]
            return AgentResult(
                question=question, intent=intent,
                answer=(
                    f"Be in the {stand} stand for the {hour:02d}:00 hour. "
                    f"That's the busiest hour in the data with {r['detections']} "
                    f"detections, and the activity holds either side of it."
                ),
                structured=r, tracer=tracer,
            )

        # COUNT
        r = tools.query_detections(
            tracer,
            stand=slots.get("stand"),
            camera_id=slots.get("camera_id"),
            species=slots.get("species", "whitetail deer"),
            antlered_only=slots.get("antlered_only", False),
            year=slots.get("year"),
            month=slots.get("month"),
        )
        return AgentResult(
            question=question, intent=intent,
            answer=(
                f"{r['detections']} detections covering {r['animals']} animals."
            ),
            structured=r, tracer=tracer,
        )

    except tools.ToolError as exc:
        with tracer.span("policy.tool_error", kind="policy") as span:
            span.attributes["decision"] = "SURFACED"
            span.attributes["error"] = str(exc)
        return AgentResult(
            question=question, intent=intent,
            answer=f"I can't answer that: {exc}",
            error=str(exc), deferred=True, tracer=tracer,
        )
    except controls.KillSwitchEngaged as exc:
        return AgentResult(
            question=question, intent=intent,
            answer=f"Scout is offline. {exc}",
            error=str(exc), tracer=tracer,
        )


# ----------------------------------------------------------------------
# Azure planner (optional)
# ----------------------------------------------------------------------

def _azure_available() -> bool:
    return all(
        os.environ.get(k)
        for k in ("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_DEPLOYMENT")
    )


def _run_azure(question: str, tracer: Tracer) -> AgentResult:
    """
    Real tool-calling loop. Left thin on purpose -- if you wire this up before
    Wednesday, wire it up with the SAME tools module so the traces and the
    evals do not change shape. Falls back to stub if the call fails.
    """
    try:
        from openai import AzureOpenAI  # noqa: F401
    except ImportError:
        print("  (openai package not installed -- falling back to stub planner)")
        return _run_stub(question, tracer)

    print("  (azure planner path is a stub hook -- using deterministic planner)")
    return _run_stub(question, tracer)


# ----------------------------------------------------------------------

def ask(question: str, planner: str = "stub") -> AgentResult:
    if planner == "maf":
        # Imported lazily and never at module scope. The Microsoft Agent
        # Framework path has its own dependencies and its own virtual
        # environment; the stub planner has to keep working with neither of
        # them installed, because the stub is what you present with.
        try:
            from scout import agent_maf
        except ImportError as exc:
            # A bare ModuleNotFoundError traceback is the last thing you want
            # on a projector. Say what to run instead.
            raise SystemExit(
                f"\n  The Microsoft Agent Framework planner is not installed here.\n"
                f"  ({exc})\n\n"
                f"  It lives in a separate environment on purpose. Set it up with:\n"
                f"      .\\setup.ps1 -Maf\n"
                f"  then run the beat with:\n"
                f"      .\\demo.ps1 4 -Maf\n"
            ) from exc

        return agent_maf.ask(question)

    controls.ensure_state()
    tracer = Tracer(trace_name="scout.ask")
    with tracer.span("scout.ask", kind="internal", question=question):
        if planner == "azure" and _azure_available():
            result = _run_azure(question, tracer)
        else:
            result = _run_stub(question, tracer)
    tracer.flush()
    return result


def _parse_args(argv: list[str]) -> tuple[str, str, bool]:
    """Return (question, planner, show_trace) from the command line.

    Written out longhand rather than with argparse because '--planner maf'
    takes a VALUE, and the naive "drop anything starting with --" filter this
    used to do would have swallowed the word 'maf' into the question.
    """
    words: list[str] = []
    planner = "stub"
    skip_next = False

    for i, arg in enumerate(argv):
        if skip_next:
            skip_next = False
            continue
        if arg == "--planner":
            planner = argv[i + 1] if i + 1 < len(argv) else "stub"
            skip_next = True
        elif arg == "--azure":
            planner = "azure"
        elif arg == "--maf":
            planner = "maf"
        elif not arg.startswith("--"):
            words.append(arg)

    return " ".join(words), planner, "--trace" in argv


def main() -> None:
    question, planner, show_trace = _parse_args(sys.argv[1:])

    if not question:
        print(__doc__)
        raise SystemExit(1)

    result = ask(question, planner=planner)
    result.print_answer()

    if show_trace and result.tracer:
        highlight = "2.2" if "--highlight-firmware" in sys.argv else None
        result.tracer.print_tree(highlight=highlight)


if __name__ == "__main__":
    main()
