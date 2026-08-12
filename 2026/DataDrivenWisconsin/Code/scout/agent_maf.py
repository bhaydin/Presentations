r"""
Scout, rebuilt on Microsoft Agent Framework.

    python -m scout.agent_maf "what time should I sit Bean Field in October 2025?"
    python -m scout.agent --planner maf "..."          (same thing, via the router)

WHY THIS FILE EXISTS
    The stub planner in agent.py is a keyword router. It is honest, offline and
    byte-identical every run, which makes it useful for reproducible exploration.
    What it is NOT is a real agent, which raises a fair question: does the same
    result hold when a model chooses the tools?

    This file answers that question. It uses the same warehouse, tools, traces,
    and eval suite, with a real model doing the planning and real tool calling
    underneath. The firmware failure still happens identically because it was
    never a planning failure.

WHAT IS DELIBERATELY SHARED WITH THE STUB
    scout/tools.py       untouched. Same SQL, same spans, same rule engine.
    scout/controls.py    untouched. Same approval queue, same kill switches.
    scout/tracing.py     untouched. Same readable output for beats 4 and 6.
    AgentResult          reused, so evals/run_evals.py works against either
                         planner without a single change to its assertions.

    Only the PLANNER is different. That is the point being demonstrated.

SETUP
    .\setup.ps1 -Maf
    $env:AZURE_OPENAI_ENDPOINT   = "https://<resource>.openai.azure.com"
    $env:AZURE_OPENAI_API_KEY    = "<key>"
    $env:AZURE_OPENAI_DEPLOYMENT = "<deployment name>"

    Plain OpenAI also works for a quick test: set OPENAI_API_KEY and
    OPENAI_MODEL instead.
"""

from __future__ import annotations

import asyncio
import atexit
import contextvars
import os
import sys
from dataclasses import dataclass
from typing import Annotated, Any

from agent_framework import Agent, tool
from agent_framework.openai import OpenAIChatClient, OpenAIChatOptions

from scout import controls, tools
from scout.agent import AgentResult
from scout.tracing import Tracer

# ----------------------------------------------------------------------
# Per-run state
#
# The framework builds each tool's JSON schema by reading its signature, so
# anything in the signature gets advertised to the model as a parameter it is
# supposed to fill in. Our real tools take `tracer` as their first argument --
# if we passed them through as-is, the model would see a "tracer" field and
# hallucinate values into it.
#
# So the adapters below take only the arguments the model should actually
# choose, and pick the tracer up from a context variable instead.
# ----------------------------------------------------------------------

_TRACER: contextvars.ContextVar[Tracer] = contextvars.ContextVar("scout_tracer")
_CALLS: contextvars.ContextVar[list["_ToolCall"]] = contextvars.ContextVar("scout_calls")


@dataclass
class _ToolCall:
    """One tool invocation, recorded so the eval suite can assert on numbers."""

    name: str
    result: dict[str, Any] | None = None
    error: str | None = None


def _record(name: str, result: dict | None = None, error: str | None = None) -> None:
    _CALLS.get().append(_ToolCall(name=name, result=result, error=error))


# ----------------------------------------------------------------------
# The toolbelt
#
# Thin adapters over scout/tools.py. Each one:
#   - exposes only model-facing arguments
#   - runs the real tool, which emits the real span and runs the real SQL
#   - records the structured result for the evals
#   - returns a plain string, which is what the model reads
#
# ToolError is caught and returned as text rather than raised. A tool that
# raises tells the model nothing; a tool that says "camera 15 does not exist,
# known cameras are 1-12" lets it correct itself or tell the user honestly.
# ----------------------------------------------------------------------


@tool
def query_detections(
    stand: Annotated[str | None, "Stand name, e.g. 'Bean Field'. Omit for all stands."] = None,
    camera_id: Annotated[int | None, "Specific camera number, 1-12. Omit for all."] = None,
    species: Annotated[str, "Species, e.g. 'whitetail deer', 'turkey', 'coyote'."] = "whitetail deer",
    antlered_only: Annotated[bool, "True to count only antlered bucks."] = False,
    year: Annotated[int | None, "Four digit season year, e.g. 2024."] = None,
    month: Annotated[int | None, "Month number 1-12."] = None,
) -> str:
    """Count animal detections matching a filter. Use for 'how many' questions."""
    try:
        r = tools.query_detections(
            _TRACER.get(),
            stand=stand,
            camera_id=camera_id,
            species=species,
            antlered_only=antlered_only,
            year=year,
            month=month,
        )
    except tools.ToolError as exc:
        _record("query_detections", error=str(exc))
        return f"ERROR: {exc}"
    _record("query_detections", result=r)
    return f"{r['detections']} detections covering {r['animals']} animals."


@tool
def peak_activity_hour(
    stand: Annotated[str, "Stand name, e.g. 'Bean Field'."],
    year: Annotated[int, "Four digit season year, e.g. 2025."],
    month: Annotated[int, "Month number 1-12."],
    window: Annotated[str, "'morning', 'evening', or 'all'."] = "morning",
    species: Annotated[str, "Species to analyse."] = "whitetail deer",
) -> str:
    """Find the hour of day with the most detections. Use for 'what time should I sit'."""
    try:
        r = tools.peak_activity_hour(
            _TRACER.get(), stand=stand, year=year, month=month, window=window, species=species
        )
    except tools.ToolError as exc:
        _record("peak_activity_hour", error=str(exc))
        return f"ERROR: {exc}"
    _record("peak_activity_hour", result=r)
    if r["peak_hour"] is None:
        return "No detections match that window."
    return (
        f"Peak hour is {r['peak_hour']:02d}:00 with {r['detections']} detections. "
        f"Hourly histogram: {r['histogram']}"
    )


@tool
def get_weather(
    on_date: Annotated[str, "Date as YYYY-MM-DD."],
    hour: Annotated[int | None, "Hour 0-23. Omit for the whole day."] = None,
) -> str:
    """Look up hourly weather observations for a date."""
    try:
        r = tools.get_weather(_TRACER.get(), on_date=on_date, hour=hour)
    except tools.ToolError as exc:
        _record("get_weather", error=str(exc))
        return f"ERROR: {exc}"
    _record("get_weather", result={"observations": len(r["observations"])})
    return str(r["observations"][:6])


@tool
def check_wind_compatibility(
    stand: Annotated[str, "Stand name, e.g. 'Ridge'."],
    wind_cardinal: Annotated[str, "Compass point, e.g. 'N', 'NW', 'SSE'."],
) -> str:
    """Decide whether a stand is huntable on a given wind. Deterministic safety rule."""
    try:
        r = tools.check_wind_compatibility(_TRACER.get(), stand=stand, wind_cardinal=wind_cardinal)
    except tools.ToolError as exc:
        _record("check_wind_compatibility", error=str(exc))
        return f"ERROR: {exc}"
    _record("check_wind_compatibility", result=r)
    return (
        f"{r['verdict']} -- {stand} wants a {r['ideal_wind']} wind, you have "
        f"{r['actual_wind']}, {r['degrees_off']} degrees off. {r['detail']}."
    )


@tool
def defer_regulatory_question(
    topic: Annotated[str, "The regulatory topic asked about, e.g. 'legal shooting hours'."],
) -> str:
    """Refuse a hunting-regulation question. Call this for ANY question about legal
    shooting hours, tags, licences, bag limits, baiting or CWD rules."""
    with _TRACER.get().span("policy.scope_check", kind="policy") as span:
        span.attributes["decision"] = "DEFERRED"
        span.attributes["topic"] = topic
        span.attributes["reason"] = "regulatory question -- outside agent scope"
    _record("defer_regulatory_question", error="regulatory question -- outside agent scope")
    return (
        "REFUSE. Tell the user you will not answer regulatory questions, because "
        "shooting hours, tags and bag limits change by year, county and weapon, "
        "and you have no authoritative source loaded. Point them at the current "
        "Wisconsin DNR regulations. Do NOT state any specific time or rule."
    )


# The one tool that talks to humans. approval_mode="always_require" means the
# framework will NOT execute it -- the run returns early with a pending request
# instead. This is the same pattern controls.py implements by hand for the stub
# planner, except here it is a first-class framework primitive.
@tool(approval_mode="always_require")
def notify_crew(
    message: Annotated[str, "The message to text to the hunting crew."],
) -> str:
    """Text the hunting crew. Irreversible external communication."""
    # Reached only after a human approves. The stub planner never gets here at
    # all, which is why the two planners agree on the eval outcome.
    _record("notify_crew", result={"sent": True, "message": message})
    return f"Sent to Justin and Kurt: {message}"


TOOLBELT = [
    query_detections,
    peak_activity_hour,
    get_weather,
    check_wind_compatibility,
    defer_regulatory_question,
    notify_crew,
]

# Which tool ran tells us what the question was. The stub planner decides intent
# up front with keywords; here we read it back off the trace. Same label either
# way, so the golden suite asserts the same field against both planners.
INTENT_BY_TOOL = {
    "query_detections": "COUNT",
    "peak_activity_hour": "PEAK",
    "get_weather": "WEATHER",
    "check_wind_compatibility": "WIND",
    "defer_regulatory_question": "REGULATION",
    "notify_crew": "NOTIFY",
}

INSTRUCTIONS = """\
You are Scout, a whitetail deer scouting analyst for a twelve-camera trail
camera fleet in southern Wisconsin. You answer questions about deer movement
using ONLY the tools provided.

Rules:
- Always call a tool. Never answer a data question from memory or guess a number.
- Report the numbers the tools return, exactly. Do not round or reinterpret them.
- For "what time should I sit" questions, call peak_activity_hour and report the
  peak hour it returns, even if the hour looks surprising to you.
- The six stands are: Bean Field, Ridge, Creek Bottom, Oak Flat, Cedar Swamp,
  Power Line. Cameras are numbered 1 to 12.
- If a tool returns an ERROR, tell the user plainly what was wrong. Never invent
  a plausible answer to cover it up.
- For ANY question about hunting regulations -- legal shooting hours, tags,
  licences, bag limits, baiting, CWD -- call defer_regulatory_question and then
  refuse. Never state a specific legal time or rule.
- To send a message to the crew, call notify_crew. It will be held for human
  approval; that is expected, not an error.

Keep answers to one or two sentences, in the voice of an experienced hunter.
"""


# ----------------------------------------------------------------------
# Client
# ----------------------------------------------------------------------


class MafNotConfigured(RuntimeError):
    """Raised when no model credentials are present. Never let this be silent."""


# One event loop and one HTTP client for the whole process. See ask() for why.
_LOOP: asyncio.AbstractEventLoop | None = None
_CACHED_CLIENT: tuple[OpenAIChatClient, str] | None = None


def _loop() -> asyncio.AbstractEventLoop:
    global _LOOP
    if _LOOP is None or _LOOP.is_closed():
        _LOOP = asyncio.new_event_loop()
        asyncio.set_event_loop(_LOOP)
        atexit.register(_shutdown)
    return _LOOP


def _client() -> tuple[OpenAIChatClient, str]:
    global _CACHED_CLIENT
    if _CACHED_CLIENT is None:
        _CACHED_CLIENT = _build_client()
    return _CACHED_CLIENT


def _shutdown() -> None:
    """Drain the client's connection pool BEFORE the loop dies, not after."""
    if _LOOP is None or _LOOP.is_closed():
        return
    try:
        if _CACHED_CLIENT is not None:
            closer = getattr(_CACHED_CLIENT[0], "aclose", None) or getattr(
                _CACHED_CLIENT[0], "close", None
            )
            if closer:
                result = closer()
                if asyncio.iscoroutine(result):
                    _LOOP.run_until_complete(result)
        _LOOP.run_until_complete(_LOOP.shutdown_asyncgens())
    except Exception:
        # Shutdown noise must never be the last thing on the screen.
        pass
    finally:
        _LOOP.close()


def _build_client() -> tuple[OpenAIChatClient, str]:
    """
    Build the chat client from environment variables. Returns (client, label).

    Azure is the default path. Note that the Azure-specific client classes were
    removed from agent_framework.azure -- current MAF routes Azure through the
    OpenAI client with azure_endpoint set, which is what this does.
    """
    endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
    api_key = os.environ.get("AZURE_OPENAI_API_KEY")
    deployment = os.environ.get("AZURE_OPENAI_DEPLOYMENT") or os.environ.get(
        "AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"
    )

    if endpoint and api_key and deployment:
        client = OpenAIChatClient(
            model=deployment,
            api_key=api_key,
            azure_endpoint=endpoint,
            api_version=os.environ.get("AZURE_OPENAI_API_VERSION", "2025-01-01-preview"),
        )
        return client, f"azure:{deployment}"

    if os.environ.get("OPENAI_API_KEY"):
        model = os.environ.get("OPENAI_MODEL", "gpt-4o-mini")
        return OpenAIChatClient(model=model), f"openai:{model}"

    raise MafNotConfigured(
        "No model credentials found. Set AZURE_OPENAI_ENDPOINT, "
        "AZURE_OPENAI_API_KEY and AZURE_OPENAI_DEPLOYMENT (or OPENAI_API_KEY). "
        "The stub planner needs none of this: python -m scout.agent \"...\""
    )


# ----------------------------------------------------------------------
# The run
# ----------------------------------------------------------------------


async def _ask_async(question: str, tracer: Tracer) -> AgentResult:
    _TRACER.set(tracer)
    calls: list[_ToolCall] = []
    _CALLS.set(calls)

    # Check the kill switch BEFORE spending a model call. The tools check it
    # too, but by then you have already paid for a round trip.
    try:
        controls.check_kill_switches()
    except controls.KillSwitchEngaged as exc:
        return AgentResult(
            question=question,
            intent="KILLED",
            answer=f"Scout is offline. {exc}",
            error=str(exc),
            tracer=tracer,
        )

    client, label = _client()

    with tracer.span("planner", kind="llm", planner="maf", model=label):
        pass

    agent = Agent(
        client,
        INSTRUCTIONS,
        name="Scout",
        tools=TOOLBELT,
        # Temperature 0 buys you as much run-to-run stability as a language
        # model will give you. It is NOT the byte-identical determinism the
        # stub planner has, which is why the stub remains the reproducible default.
        default_options=OpenAIChatOptions(temperature=0.0),
    )
    response = await agent.run(question)

    return _to_result(question, response, calls, tracer)


def _to_result(question, response, calls, tracer) -> AgentResult:
    """Fold a framework response back into the same AgentResult the stub returns."""
    requests = list(getattr(response, "user_input_requests", []) or [])

    # A held write. Queue it in our own control plane too, so `controls status`
    # shows the same pending queue for both planners and beat 8 is unchanged.
    #
    # The span matters as much as the queue entry. The framework stops the tool
    # before it runs, so nothing downstream emits anything -- without this the
    # trace for a blocked write reads "0 tool calls, 0 errors", which is true
    # but exposes no evidence to the caller. The refusal has to be visible in
    # the trace.
    if requests:
        approval_id = None
        for req in requests:
            call = getattr(req, "function_call", None)
            if call is None:
                continue
            with tracer.span("policy.write_gate", kind="policy") as pspan:
                approval_id = controls.queue_approval(
                    call.name, {"message": str(call.arguments), "recipients": ["Justin", "Kurt"]}
                )
                pspan.attributes["decision"] = "HELD FOR APPROVAL"
                pspan.attributes["tool"] = call.name
                pspan.attributes["approval_id"] = approval_id
                pspan.attributes["reason"] = "write tool: irreversible external comms"
                pspan.attributes["gated_by"] = "agent_framework approval_mode=always_require"
            pspan.status = "HELD"
        return AgentResult(
            question=question,
            intent="NOTIFY",
            answer=(
                f"Held for your approval ({approval_id}). I don't send messages "
                "to people on my own authority."
            ),
            approval_required=True,
            approval_id=approval_id,
            tracer=tracer,
        )

    # Prefer the last call that actually produced numbers; fall back to the last
    # call of any kind so a pure-error turn still reports a sensible intent.
    with_result = [c for c in calls if c.result is not None]
    chosen = with_result[-1] if with_result else (calls[-1] if calls else None)

    errors = [c.error for c in calls if c.error]

    return AgentResult(
        question=question,
        intent=INTENT_BY_TOOL.get(chosen.name, "UNKNOWN") if chosen else "UNKNOWN",
        answer=response.text or "(no answer)",
        structured=chosen.result if chosen and chosen.result else {},
        deferred=bool(errors),
        error=errors[0] if errors else None,
        tracer=tracer,
    )


def ask(question: str) -> AgentResult:
    """
    Synchronous entry point, matching scout.agent.ask.

    The framework is async; the eval suite and the demo driver are not. This
    bridges the two so no caller has to change.

    WHY NOT asyncio.run()
        asyncio.run() creates a fresh event loop and closes it when it returns.
        The HTTP client underneath the model call keeps a connection pool that
        it cleans up asynchronously -- so the loop closes first, the pool tries
        to clean up on a dead loop, and every single call prints an "Event loop
        is closed" traceback. Running the twenty golden cases produced 56KB of
        those, which obscures the result and looks like an application failure.

        So we keep ONE loop and ONE client for the life of the process, and
        shut them down properly at exit.
    """
    controls.ensure_state()
    tracer = Tracer(trace_name="scout.ask.maf")
    with tracer.span("scout.ask", kind="internal", question=question, planner="maf"):
        result = _loop().run_until_complete(_ask_async(question, tracer))
    tracer.flush()
    return result


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not args:
        print(__doc__)
        raise SystemExit(1)

    try:
        result = ask(" ".join(args))
    except MafNotConfigured as exc:
        print(f"\n  {exc}\n")
        raise SystemExit(2)

    result.print_answer()
    if "--trace" in sys.argv and result.tracer:
        highlight = "2.2" if "--highlight-firmware" in sys.argv else None
        result.tracer.print_tree(highlight=highlight)


if __name__ == "__main__":
    main()
