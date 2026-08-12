# Scout — an AgentOps demo

Built for **AgentOps for Data Teams**, Data-Driven Wisconsin 2026, MSOE.

Scout is a scouting analyst agent over a trail camera fleet. Twelve cameras,
four seasons of detections, a weather feed, and one decision to make:

> Given the cameras, the wind, and four seasons of history — where and when do I sit?

It is a sensor network with an on-board classifier, a time-series store, business
rules, and a recommendation someone acts on at real cost. Structurally identical
to "which accounts do I call Monday."

Then a vendor ships firmware 2.2 to eight of the twelve cameras. Release notes:
*"Improved timestamp reliability. Capture times now recorded in UTC."*

Every row still loads. No schema changes. No errors. No alerts. Scout keeps
answering confidently and is now wrong, and the only thing that catches it is
an eval suite.

---

## Setup

**Windows:**

```powershell
.\setup.ps1
```

**Mac / Linux:**

```bash
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements.txt
.venv/bin/python -m scout.build_db
```

Runs fully offline. No API key, no network at runtime, no cloud. Deterministic —
same seed, same data, same numbers every time.

> New to this code? Read **[WALKTHROUGH.md](WALKTHROUGH.md)** — a plain-language
> tour of every file, with the sentence to say out loud for each one.

## The demo

```powershell
.\demo.ps1 reset     # back to green before every rehearsal
.\demo.ps1 1         # Scout answers correctly: be in the stand at 06:00
.\demo.ps1 2         # golden suite: 20/20, gate clear
.\demo.ps1 3         # firmware 2.2 rollout   <-- THE CHANGE
.\demo.ps1 4         # same question -> 11:00, confident, 0 errors
.\demo.ps1 5         # golden suite: 15/20, temporal category only
.\demo.ps1 6         # trace archaeology
.\demo.ps1 7         # data eval names the root cause
.\demo.ps1 8         # approval gate holds a write
.\demo.ps1 9         # kill switch

.\demo.ps1 all       # full rehearsal, pauses between beats
```

On Mac/Linux use `./demo.sh` with the same arguments.

Beat-by-beat narration is in **[run_of_show.md](run_of_show.md)**.

## Layout

```text
scout/      the agent, its tools, its warehouse, its control plane
evals/      the golden suite and the data invariants
```

Two folders, and the split is the argument: `scout/` is the thing everybody
builds, `evals/` is the thing everybody skips.

## What each piece is teaching

| File | The point |
|---|---|
| [scout/schema.sql](scout/schema.sql) | `detections.captured_at` has **no offset column**. That single omission is what makes silent drift possible. |
| [scout/apply_firmware_rollout.py](scout/apply_firmware_rollout.py) | The rollout is **partial**. A clean cutover shifts the whole distribution and a human catches it. A partial rollout leaves the real dawn peak at reduced amplitude and grows a second peak five hours later — that looks like a *finding*. |
| [scout/tools.py](scout/tools.py) | `check_wind_compatibility` is a deterministic rule engine with **no model in the path**. Some decisions don't get a probability. |
| [scout/controls.py](scout/controls.py) | An approval gate is an `if` statement and a queue. Teams skip these because nobody asked for them in the demo. |
| [scout/tracing.py](scout/tracing.py) | A trace is just another fact table. Land it in your lakehouse and query agent behavior with the SQL you already have. |
| [evals/golden_cases.yaml](evals/golden_cases.yaml) | Not one assertion about how the answer is **worded**. Assert on numbers and verdicts. Prose assertions fail on every prompt edit and you will turn the suite off inside a week. |
| [evals/run_evals.py](evals/run_evals.py) | The category rollup turns "something is broken" into "the temporal aggregations are broken and nothing else is." |
| [evals/run_data_evals.py](evals/run_data_evals.py) | Most agent failures are data failures wearing a costume. This one fires *before* any agent eval and names the eight cameras. |

## The year split in the golden suite

Factual cases target the 2023 and 2024 seasons. Temporal cases target 2025.
The firmware rolled out September 2025.

That split is deliberate: it makes the suite a **diagnostic** rather than an
alarm. When it goes red, 8 factual cases still pass, 3 rule cases still pass,
and all 5 temporal cases fail. The shape of the failure points at the cause.

Two of the five temporal cases fail on **volume only** — the peak hour is
unchanged but 65% and 52% of the detections vanish, because after the November
DST change the drifted dawn detections land past noon and leave the morning
window entirely. That is why every temporal case asserts the hour *and* the
count. Assert the shape, not just the headline number.

## Honest disclosure about the planner

The default planner is a **deterministic keyword router**, not a language model.
Say so on stage:

> "The planner is stubbed so this runs without wifi. The warehouse, the tools,
> the traces, and the evals are real. And the failure I'm about to show you is a
> *data* failure — swapping in a frontier model does not fix it. That's the point."

You no longer have to take that on faith. See below.

## The second planner: Microsoft Agent Framework

[scout/agent_maf.py](scout/agent_maf.py) is the same agent rebuilt on
**Microsoft Agent Framework** — real `Agent`, real `@tool` calling, real model.
It shares the warehouse, the tools, the traces, the control plane and the eval
suite with the stub. Only the planner differs.

```powershell
.\setup.ps1 -Maf                     # separate .venv-maf, never touches .venv
$env:AZURE_OPENAI_ENDPOINT   = "https://<resource>.openai.azure.com"
$env:AZURE_OPENAI_API_KEY    = "<key>"
$env:AZURE_OPENAI_DEPLOYMENT = "<deployment name>"

.\demo.ps1 4 -Maf                    # same beat, real model
.\demo.ps1 5 -Maf                    # same twenty cases, real model
```

**Verified against a live model (gpt-4o-mini), with `golden_cases.yaml`
completely unmodified:**

| Warehouse | Stub planner | MAF planner |
|---|---|---|
| green | 20/20, gate clear | **20/20, gate clear** |
| after firmware 2.2 | 15/20, temporal 0/5 | **15/20, temporal 0/5** |

Identical failure signature. Same wrong hours (11, 11, 23), same volume
collapses (−65%, −52%). That is the whole argument, and it is now demonstrable
rather than asserted:

> "This is not the stub. This is Microsoft Agent Framework with a real model
> doing real tool calling. Same twenty assertions, not one of them edited. It
> fails in exactly the same way, because it was never a planning failure."

Two design notes worth saying out loud:

- `notify_crew` is gated with `@tool(approval_mode="always_require")`. The
  framework **refuses to execute it** and returns a pending request instead —
  the same pattern [scout/controls.py](scout/controls.py) implements by hand for
  the stub. A hand-rolled control became a first-class framework primitive.
- Refusal is a **tool call** (`defer_regulatory_question`), not a hoped-for turn
  of phrase. That makes the refusal auditable in the trace and assertable in the
  suite.

Offline sanity checks that need no model or credentials:

```powershell
.\.venv-maf\Scripts\python.exe -m evals.test_maf_wiring
```

### ARM64 Windows note

Install `agent-framework-core` and `agent-framework-openai`, **not** the
`agent-framework` meta-package. The meta-package pulls `grpcio`, `cryptography`
and `pydantic-monty`, none of which ship win_arm64 wheels — pip falls back to
building them from source and needs Rust plus MSVC. `requirements-maf.txt`
already pins the two that install as clean wheels.

## Reset

```powershell
.\demo.ps1 reset
```

Rebuilds the green warehouse, clears approvals and kill switches, and deletes
`traces.jsonl`. Run it before every rehearsal and once more before you walk on.

## Maintenance

If you ever change `SEED` or the volume constants in `scout/build_db.py`, the
expected integers in `evals/golden_cases.yaml` go stale. To find out which:

```powershell
.\.venv\Scripts\python.exe -m evals.derive_baseline
```

It prints the stale values and refuses to run against a drifted warehouse.
