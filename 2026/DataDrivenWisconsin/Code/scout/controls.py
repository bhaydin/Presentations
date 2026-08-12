"""
The control plane: approval gates, escalation, and kill switches.

Crude on purpose. A gate is an if-statement and a queue. Teams skip these not
because they are hard but because nobody asked for them in the demo.

Three levels of kill switch, matching what you actually want in production:
    TOOL  -- disable one capability, agent keeps working
    AGENT -- this agent stops, others keep running
    FLEET -- everything stops now

State lives in flag files so you can trip a switch from a second terminal
mid-demo without restarting anything.
"""

from __future__ import annotations

import json
import textwrap
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STATE_DIR = ROOT / ".control"
APPROVALS_PATH = STATE_DIR / "pending_approvals.jsonl"

# Tools that mutate the world or talk to humans. Never auto-execute.
WRITE_TOOLS = {"notify_crew"}


class KillSwitchEngaged(RuntimeError):
    pass


class ApprovalRequired(Exception):
    def __init__(self, tool: str, payload: dict, approval_id: str) -> None:
        self.tool = tool
        self.payload = payload
        self.approval_id = approval_id
        super().__init__(f"approval required for {tool} (id={approval_id})")


def _flag(name: str) -> Path:
    return STATE_DIR / name


def ensure_state() -> None:
    STATE_DIR.mkdir(exist_ok=True)


# ----------------------------------------------------------------------
# Kill switches
# ----------------------------------------------------------------------

def engage_kill(level: str, reason: str) -> None:
    ensure_state()
    assert level in {"fleet", "agent", "tool"}
    _flag(f"kill.{level}").write_text(
        json.dumps({"reason": reason, "at": datetime.now().isoformat(timespec="seconds")})
    )


def release_kill(level: str) -> None:
    p = _flag(f"kill.{level}")
    if p.exists():
        p.unlink()


def check_kill_switches(tool: str | None = None) -> None:
    for level in ("fleet", "agent"):
        p = _flag(f"kill.{level}")
        if p.exists():
            info = json.loads(p.read_text())
            raise KillSwitchEngaged(
                f"{level.upper()} kill switch engaged at {info['at']}: {info['reason']}"
            )
    if tool:
        p = _flag(f"kill.tool.{tool}")
        if p.exists():
            info = json.loads(p.read_text())
            raise KillSwitchEngaged(
                f"TOOL kill switch on {tool}: {info['reason']}"
            )


def disable_tool(tool: str, reason: str) -> None:
    ensure_state()
    _flag(f"kill.tool.{tool}").write_text(
        json.dumps({"reason": reason, "at": datetime.now().isoformat(timespec="seconds")})
    )


# ----------------------------------------------------------------------
# Approval gate
# ----------------------------------------------------------------------

def requires_approval(tool: str) -> bool:
    return tool in WRITE_TOOLS


def _next_approval_id() -> str:
    """
    Sequential, human-readable, and guaranteed unique.

    This used to be a HHMMSS timestamp, which collided whenever two writes
    were queued inside the same second -- exactly what the eval suite does.
    Two rows sharing an id means approving one silently approves the other,
    which is the kind of bug you do not want in the thing whose entire job
    is to stop unattended writes.
    """
    n = 0
    if APPROVALS_PATH.exists():
        n = sum(1 for line in APPROVALS_PATH.read_text().splitlines() if line.strip())
    return f"apr-{n + 1:03d}"


def queue_approval(tool: str, payload: dict) -> str:
    ensure_state()
    approval_id = _next_approval_id()
    with APPROVALS_PATH.open("a") as fh:
        fh.write(
            json.dumps(
                {
                    "approval_id": approval_id,
                    "tool": tool,
                    "payload": payload,
                    "queued_at": datetime.now().isoformat(timespec="seconds"),
                    "status": "pending",
                }
            )
            + "\n"
        )
    return approval_id


def list_pending() -> list[dict]:
    if not APPROVALS_PATH.exists():
        return []
    return [
        json.loads(line)
        for line in APPROVALS_PATH.read_text().splitlines()
        if line.strip() and json.loads(line).get("status") == "pending"
    ]


def resolve(approval_id: str, decision: str) -> None:
    """decision: approved | denied"""
    if not APPROVALS_PATH.exists():
        return
    rows = [json.loads(l) for l in APPROVALS_PATH.read_text().splitlines() if l.strip()]
    for r in rows:
        if r["approval_id"] == approval_id:
            r["status"] = decision
            r["resolved_at"] = datetime.now().isoformat(timespec="seconds")
    APPROVALS_PATH.write_text("\n".join(json.dumps(r) for r in rows) + "\n")


def reset() -> None:
    """Clean slate between rehearsals."""
    if STATE_DIR.exists():
        for p in STATE_DIR.iterdir():
            p.unlink()


if __name__ == "__main__":
    import sys

    cmd = sys.argv[1] if len(sys.argv) > 1 else "status"

    if cmd == "status":
        ensure_state()
        pending = list_pending()
        print("Pending approvals:", len(pending))
        for p in pending:
            # One line per approval, one for its message. Printing the raw
            # payload dict ran to 130+ columns and wrapped into mush on the
            # projector -- and this queue is the thing you point at on stage.
            print(f"  {p['approval_id']}  {p['tool']}  -> {p['payload']['recipients']}")
            message = p["payload"].get("message", "")
            for line in textwrap.wrap(message, width=64) or [""]:
                print(f"      {line}")
        kills = [p.name for p in STATE_DIR.glob("kill.*")]
        print("Kill switches engaged:", kills or "none")
    elif cmd == "approve":
        resolve(sys.argv[2], "approved")
        print(f"{sys.argv[2]} approved")
    elif cmd == "deny":
        resolve(sys.argv[2], "denied")
        print(f"{sys.argv[2]} DENIED")
    elif cmd == "kill":
        engage_kill(sys.argv[2], " ".join(sys.argv[3:]) or "manual")
        print(f"{sys.argv[2].upper()} kill switch ENGAGED")
    elif cmd == "release":
        release_kill(sys.argv[2])
        print(f"{sys.argv[2]} kill switch released")
    elif cmd == "reset":
        reset()
        print("control state cleared")
