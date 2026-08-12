r"""
Offline checks for the Microsoft Agent Framework planner.

    .\.venv-maf\Scripts\python.exe -m evals.test_maf_wiring

Everything here runs with NO model, NO network and NO credentials. It cannot
tell you the model picks the right tool -- only a live run does that. What it
CAN tell you is that the wiring around the model is correct, which is where
this kind of port actually breaks:

    1. no tool leaks `tracer` into its model-facing schema
    2. every tool the model can see is one we meant to expose
    3. notify_crew is the only tool gated for approval
    4. the tools really do run the real SQL against the real warehouse
    5. a tripped kill switch stops the agent BEFORE it spends a model call
    6. missing credentials fail loudly instead of silently falling back

Exits non-zero on any failure so you can gate on it.
"""

from __future__ import annotations

import contextvars
import os

from scout import agent_maf, controls
from scout.tracing import Tracer, c

PASS = c("  PASS  ", "green", "bold")
FAIL = c("  FAIL  ", "red", "bold")

failures: list[str] = []


def check(name: str, condition: bool, detail: str = "") -> None:
    print(f"{PASS if condition else FAIL}{name}")
    if not condition:
        failures.append(name)
        if detail:
            print(c(f"          {detail}", "red"))


def main() -> None:
    print()
    print(c("  MAF WIRING CHECKS  (offline, no model)", "bold"))
    print(c("  " + "=" * 68, "dim"))

    # --- 1. no tracer leaks into any tool schema -----------------------
    leaked = []
    for t in agent_maf.TOOLBELT:
        props = t.parameters().get("properties", {})
        if any(k in props for k in ("tracer", "self", "con")):
            leaked.append(t.name)
    check(
        "no tool exposes 'tracer' to the model",
        not leaked,
        f"leaking: {leaked}",
    )

    # --- 2. the toolbelt is exactly what we intended -------------------
    expected = {
        "query_detections",
        "peak_activity_hour",
        "get_weather",
        "check_wind_compatibility",
        "defer_regulatory_question",
        "notify_crew",
    }
    actual = {t.name for t in agent_maf.TOOLBELT}
    check("toolbelt is exactly the six intended tools", actual == expected, f"got {actual}")

    # --- 3. only notify_crew requires approval -------------------------
    gated = {t.name for t in agent_maf.TOOLBELT if str(t.approval_mode) == "always_require"}
    check("notify_crew is the only approval-gated tool", gated == {"notify_crew"}, f"gated: {gated}")

    # --- 4. tools really hit the warehouse -----------------------------
    # Call the underlying function directly, with the context set up the way
    # the framework would. If this returns real numbers, the adapter layer is
    # wired to the real SQL and not to a mock.
    tracer = Tracer(trace_name="test")
    ctx = contextvars.copy_context()

    def run_peak():
        agent_maf._TRACER.set(tracer)
        agent_maf._CALLS.set([])
        text = agent_maf.peak_activity_hour.func(
            stand="Bean Field", year=2025, month=10, window="morning"
        )
        return text, agent_maf._CALLS.get()

    text, calls = ctx.run(run_peak)
    got_numbers = bool(calls) and calls[0].result is not None
    check("peak_activity_hour runs real SQL and records a result", got_numbers, text)
    if got_numbers:
        print(c(f"          -> {text[:60]}", "dim"))

    # --- 4b. a bad camera surfaces as an error, not a guess ------------
    def run_bad():
        agent_maf._TRACER.set(tracer)
        agent_maf._CALLS.set([])
        text = agent_maf.query_detections.func(camera_id=15)
        return text, agent_maf._CALLS.get()

    text, calls = contextvars.copy_context().run(run_bad)
    check(
        "unknown camera returns ERROR text and records the error",
        text.startswith("ERROR:") and bool(calls) and calls[0].error,
        text,
    )

    # --- 5. kill switch stops the run before any model call ------------
    controls.ensure_state()
    controls.engage_kill("agent", "wiring test")
    try:
        # No credentials are set in this test, so if the kill switch did NOT
        # short-circuit first, this would raise MafNotConfigured instead.
        result = agent_maf.ask("what time should I sit Bean Field in October 2025?")
        stopped = result.error is not None and "kill switch" in result.error.lower()
        check("kill switch stops the agent before spending a model call", stopped, str(result.error))
    except agent_maf.MafNotConfigured:
        check(
            "kill switch stops the agent before spending a model call",
            False,
            "reached the client factory -- the kill switch check is too late",
        )
    finally:
        controls.release_kill("agent")

    # --- 6. missing credentials fail loudly ----------------------------
    saved = {k: os.environ.pop(k, None) for k in
             ("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_DEPLOYMENT",
              "AZURE_OPENAI_CHAT_DEPLOYMENT_NAME", "OPENAI_API_KEY")}
    try:
        agent_maf._build_client()
        check("missing credentials raise MafNotConfigured", False, "no exception raised")
    except agent_maf.MafNotConfigured:
        check("missing credentials raise MafNotConfigured", True)
    finally:
        for k, v in saved.items():
            if v is not None:
                os.environ[k] = v

    print(c("  " + "=" * 68, "dim"))
    if failures:
        print(c(f"  {len(failures)} FAILED: {failures}", "red", "bold"))
        print()
        raise SystemExit(1)
    print(c("  All wiring checks passed.", "green", "bold"))
    print(c("  Tool SELECTION still needs a live model -- run .\\demo.ps1 4 -Maf", "dim"))
    print()


if __name__ == "__main__":
    main()
