# Walking through the code

Written for someone who does not live in Python. Every section gives the
plain-English version first, the code second, and the main takeaway third.

You do not need to understand all 1,100 lines to learn from the example. Start
with the six files highlighted below and follow one question through the system.

---

## 1. The whole thing in four sentences

Twelve trail cameras write rows into a database. An agent answers questions by
picking a tool, the tool runs SQL, and the answer comes back with a receipt
called a trace. A vendor firmware update quietly shifts timestamps on eight of
the twelve cameras. Nothing errors, the agent keeps answering confidently, and
the only thing that notices is the eval suite.

**Key takeaway:** *There is no magic in here. It is a database, five tools, and
a test suite.*

---

## 2. How to run it

One time only:

```powershell
.\setup.ps1
```

Then, forever after:

```powershell
.\demo.ps1 reset     # back to green
.\demo.ps1 1         # ... through 9
.\demo.ps1 all       # full sequence, pauses between beats
```

`setup.ps1` builds a private Python environment in `.venv` and installs the two
libraries this needs. `demo.ps1` runs each beat using that private environment.

**Why a `.venv`:** it is a private copy of Python that belongs to this folder.
Installing into it cannot break anything else on your laptop, and nothing else
on your laptop can break the demo. `demo.ps1` calls it by its full path, so it
works whether or not it is "activated."

---

## 3. The shape of the folder

```
Python/
├── setup.ps1              run once
├── demo.ps1               replay the demonstration sequence
├── demo.sh                same thing for Mac/Linux
├── requirements.txt       the two libraries: duckdb, PyYAML
│
├── scout/                 THE AGENT AND ITS WORLD
│   ├── schema.sql         the five tables
│   ├── build_db.py        invents four seasons of data -> scout.duckdb
│   ├── solar.py           real dawn/dusk math for Milwaukee
│   ├── agent.py           question in, answer out
│   ├── tools.py           the five things the agent can do
│   ├── tracing.py         the receipt
│   ├── controls.py        approval gates and kill switches
│   └── apply_firmware_rollout.py    <- THE BREAK
│
├── evals/                 THE THING THAT CATCHES IT
│   ├── golden_cases.yaml  20 fixed questions with known answers
│   ├── run_evals.py       runs them, prints the failure signature
│   ├── run_data_evals.py  checks the data itself, not the agent
│   └── derive_baseline.py maintenance: regenerate expected numbers
│
└── scout.duckdb           the warehouse (rebuilt by reset, never edit by hand)
```

Two folders, and the split is the argument: **`scout/` is the thing everybody
builds. `evals/` is the thing everybody skips.**

> **A note on `__init__.py`:** you will see an empty-looking file by that name in
> both folders. It marks the folder as importable. That is its entire job. If
> its presence lets `scout/` be addressed as an importable unit.

---

## 4. Follow one question all the way through

This is the best place to start reviewing the implementation: one question,
five stops.

**The question:** *"what time should I sit Bean Field in October 2025?"*

### Stop 1 — `agent.py`, `_classify()` (line 151)

Decide what kind of question this is. It looks for keywords and returns one of
six labels: `PEAK`, `COUNT`, `WIND`, `NOTIFY`, `REGULATION`.

"what time" → `PEAK`.

There is a comment worth noticing at line 159:

```python
# PEAK must be tested BEFORE wind: "what time should I sit" contains
# "should i sit" and would otherwise be routed to the wind rule engine.
```

**Key takeaway:** *Order matters in a router, and this comment records a bug
that has already been found and fixed.*

### Stop 2 — `agent.py`, `_extract()` (line 103)

Pull the details out of the sentence. "Bean Field" → the stand. "2025" → the
year. "October" → month 10.

These are called **slots**. It is the boring half of every agent and it is where
most real bugs live.

### Stop 3 — `tools.py`, `peak_activity_hour()` (line 122)

The tool runs one SQL query: group October 2025 Bean Field deer detections by
hour, sort by count, take the top row.

**This is the tool that breaks.** The docstring says why, in one line:

```python
# Note that it reads captured_at directly -- no offset column exists
# to join against.
```

### Stop 4 — `tracing.py`, `Tracer.span()` (line 109)

Every step above recorded itself. Not the agent's *summary* of what it did —
the actual SQL, the actual parameters, the actual row count.

**Key takeaway:** *The trace is not a summary of what the program says it did;
it contains the query that actually hit the warehouse.*

### Stop 5 — back in `agent.py`, `_run_stub()` (line 173)

Wrap the number in a sentence and hand it back. Green warehouse: `06:00`.
After the firmware rollout: `11:00`. Same code, both times.

---

## 5. File by file

### `scout/schema.sql` — the five tables

The comment at the top summarizes the scenario in six lines: `detections`
has **no timezone column**. `captured_at` is a bare timestamp with nothing
recording what clock it came off.

**Key takeaway:** *One missing column makes everything after this possible, and
the same modeling gap can exist in production systems.*

Also note `sits` (line 57) — the outcome table. Which nights you actually sat,
what you actually saw. Almost nobody has this.

**Key takeaway:** *Without an outcome table, a bad recommendation cannot be
distinguished from bad luck.*

### `scout/build_db.py` — invents the data

Makes four seasons of believable detections. Three things make it believable:

- Deer move at **civil dawn and dusk**, not at fixed clock times, and those
  drift about 90 minutes across a Wisconsin season (`solar.py` does that math).
- Volume peaks in **early November** for the rut (`rut_multiplier`, line 108).
- Bean Field and Ridge get **three cameras each** — which is exactly why a
  partial rollout hurts most at the stands you care about most.

It uses a fixed **seed** (line 24), so it generates identical data every single
time. That is the only reason the eval suite can assert exact integers.

**Key takeaway:** *The same seed produces the same data and numbers on every
run. Reproducible test data is a prerequisite for reliable tests.*

### `scout/solar.py` — real dawn and dusk

NOAA sunrise/sunset math for Milwaukee. It exists so the fake data peaks at
*real* twilight for each date.

This file is optional on a first pass. Its role can be summarized in one
sentence:

**Key takeaway:** *The synthetic data peaks at real civil twilight for Milwaukee
on each date, which is why the break resembles a genuine finding instead of
noise.*

### `scout/agent.py` — question in, answer out

Classify, extract, call a tool, phrase the answer. That is the file.

An important limitation is explicit: the default planner is a **keyword
router**, not a language model. Interpret the default results accordingly:

> *The planner is stubbed so this runs without network access. The warehouse,
> tools, traces, and evals are real. The demonstrated failure is a **data**
> failure, so swapping in a frontier model does not fix it.*

### `scout/tools.py` — the five things Scout can do

| Tool | What it is |
|---|---|
| `query_detections` | plain counting |
| `peak_activity_hour` | **the one that breaks** |
| `get_weather` | someone else's feed, someone else's SLA |
| `check_wind_compatibility` | a rule engine — no model in the path |
| `notify_crew` | texts humans — never runs unattended |

Two of these are worth inspecting in detail.

**`check_wind_compatibility` (line 243)** is pure arithmetic: compare the wind
you have to the wind the stand needs, and if it is more than 90 degrees off, the
answer is NO. No probability, no model, no judgment.

**Key takeaway:** *Some decisions should not receive a probability. A bad wind
decision can spoil the stand for a week, so this rule is deterministic and the
eval suite pins it exactly.*

**`notify_crew` (line 303)** never sends anything. Look at the last line: it
queues the request and then **raises** — it deliberately fails.

**Key takeaway:** *The write tool cannot succeed without approval. That is the
design, not a limitation.*

### `scout/tracing.py` — the receipt

Spans go to `traces.jsonl` — one JSON object per line.

**Key takeaway:** *A trace is another fact table. It can be landed in a
lakehouse and queried with the same SQL used for other operational data.*

One detail is worth pointing at, in `span()` around line 124:

```python
# An approval hold is NOT an error -- it is the control plane doing
# its job. Distinguishing these matters: if held writes show up in
# your error rate, someone will "fix" the gate to clean the dashboard.
```

That is why the footer prints `0 errors   1 held by policy` as two separate
numbers.

### `scout/controls.py` — the boring stuff that matters

Approval gates and kill switches. Three kill levels: one **tool**, one **agent**,
the whole **fleet**.

The state is just files in a `.control/` folder. That is deliberate: a kill
switch can be tripped from a second terminal without restarting the agent.

**Key takeaway:** *This control is an `if` statement and a flag file. The hard
part is treating operational controls as requirements from the start.*

### `scout/apply_firmware_rollout.py` — the break

Eight of twelve cameras get "improved timestamp reliability: capture times now
recorded in UTC." Their timestamps shift five or six hours. Four cameras were
out of cell range and stayed on the old firmware.

Two details carry the scenario:

**It is partial (line 50).** A clean cutover moves the entire distribution and
any human looking at a chart catches it. A partial rollout leaves the real dawn
peak in place at reduced height and grows a *second* peak five hours later.

**Key takeaway:** *The result resembles a finding rather than a bug: it looks
like the deer changed their pattern. Plausible failures are why evals are
needed.*

**It is one UPDATE with a CASE, not two UPDATEs.** The comment at line 76
explains: two sequential updates would double-shift any row that crossed the
November daylight-saving boundary. That is a real implementation bug encountered
while building this example and a useful reminder to reason about sequential
data mutations.

### `evals/golden_cases.yaml` — 20 fixed questions

Eight factual, five temporal, three rule, two refusal, two write-gate.

**The important detail is what is *not* in the file:** there is not one
assertion about how the answer is *worded*. Every assertion is a number or a
verdict.

**Key takeaway:** *Asserting on prose makes a suite fail on harmless prompt
edits and encourages teams to disable it. Assert on numbers and verdicts.*

**The year split is the clever bit.** Factual cases ask about 2023 and 2024.
Temporal cases ask about 2025. The firmware rolled out in September 2025. So
when it breaks, factual stays green and temporal goes red — and the *shape* of
the failure points straight at the cause.

**Key takeaway:** *The category pattern is a diagnostic, not merely an alarm;
it can turn a two-day investigation into a twenty-minute one.*

### `evals/run_evals.py` — runs them, prints the signature

The bar chart at the bottom provides the most useful summary. `check()` (line
36) is just a list of comparisons; there is no hidden complexity.

It exits non-zero when anything fails, which is what makes it usable as a CI
gate. (`demo.ps1` handles that exit code so an intentionally red suite does not
stop the demonstration sequence.)

### `evals/run_data_evals.py` — checks the data, not the agent

One invariant: deer are crepuscular, so at least **80%** of deer detections
should land within 100 minutes of civil dawn or dusk. Three seasons sit at 92%.
2025 comes in at 33%.

Then it breaks the number down per camera and every failing camera is on
firmware 2.2.

**Key takeaway:** *Nothing here violates a schema, so a structural data contract
never fires. Detecting this problem requires assertions about the **shape** of
each partition against a known-good baseline.*

### `scout/agent_maf.py` — the same agent on Microsoft Agent Framework

This optional path answers the natural question of whether a real model changes
the result.

Everything is shared with the stub — same warehouse, same `tools.py`, same
`controls.py`, same tracer, same twenty golden cases. **Only the planner
changes.** And the failure is identical:

| Warehouse | Stub | MAF + real model |
|---|---|---|
| green | 20/20 | 20/20 |
| after firmware 2.2 | 15/20, temporal 0/5 | 15/20, temporal 0/5 |

Same wrong hours. Same volume collapses. Not one assertion edited.

**Key takeaway:** *Microsoft Agent Framework uses a real model and real tool
calling against the same twenty assertions. It fails in the same way because
the defect was never a planning failure.*

Three things are worth noticing when reviewing the file:

**The tool adapters exist because of one sharp edge.** The framework builds each
tool's JSON schema by reading its Python signature. Our real tools take `tracer`
as their first argument — so a straight pass-through would advertise a `tracer`
field to the model and invite it to hallucinate values into it. The adapters
expose only the arguments the model should choose, and pick the tracer up from a
context variable.

**Key takeaway:** *The model can see only the arguments exposed in the
signature. That signature is a schema controlled by the application.*

**The write gate became one decorator argument:**

```python
@tool(approval_mode="always_require")
def notify_crew(message: ...) -> str:
```

The framework will not execute it. The run returns early with a pending request
instead — the same thing `controls.py` does by hand for the stub.

**Key takeaway:** *The hand-rolled and framework-native implementations converge
on the same approval pattern, which makes the control portable and explicit.*

**Refusal is a tool call, not a hoped-for sentence.** `defer_regulatory_question`
is a real tool. The model calls it, so the refusal lands in the trace as a span
and the eval suite can assert on it.

**Key takeaway:** *A refusal expressed only as prose is difficult to assert on
or audit. Representing it as a tool call creates structured evidence.*

Offline checks that need no model and no credentials:

```powershell
.\.venv-maf\Scripts\python.exe -m evals.test_maf_wiring
```

### `evals/derive_baseline.py` — maintenance only

This command is for maintenance rather than the normal sequence. After changing
the random seed in `build_db.py`, it identifies which expected numbers went
stale.

It refuses to run against a drifted warehouse — deriving a baseline from broken
data would pin the bug as the expected answer and turn the suite permanently
green on garbage.

---

## 6. Python used in the example

The following table explains the recurring syntax in plain terms.

| What you see | What it means |
|---|---|
| `def name(...):` | defines a function |
| `with tracer.span("x"):` | do this block, and automatically clean up after — here, start a timer and stop it no matter what happens |
| `raise SomeError(...)` | stop and signal a problem upward |
| `try: / except:` | attempt something, and handle the failure if it comes |
| `@dataclass` | "this class is just a bundle of named fields" |
| `f"...{value}..."` | string with values slotted in |
| `dict` / `{...}` | key-value lookup table |
| `list` / `[...]` | ordered list |
| `python -m scout.agent` | "run the `agent` file inside the `scout` folder as a program" |
| `_leading_underscore` | by convention, internal — not meant to be called from outside |

The `with` statement is especially important because it appears throughout the
tracing implementation:

```python
with tracer.span("peak_activity_hour", kind="tool") as span:
    ...do the work...
```

**Key takeaway:** *This is a timer that cannot be forgotten. It starts when the
block opens and stops when it closes, even if the enclosed code fails.*

---

## 7. Common questions

**"Is that a real LLM?"**
No, and deliberately. The planner is a keyword router so this runs without wifi.
The warehouse, the tools, the traces and the evals are real. The failure is a
data failure — a better model does not fix it. That's the point.

**"Would GPT-5 / Claude have caught this?"**
No. The model was never shown anything wrong. It got clean rows, ran correct
SQL, and did correct arithmetic on bad inputs. There is nothing in the trace for
a model to object to.

**"Wouldn't a data contract catch it?"**
No. Nothing violated a schema. Every row is a valid timestamp in a
`TIMESTAMP` column. Contracts check structure; this needs a check on
distribution.

**"Why didn't anyone notice?"**
Because the answer stayed plausible. 11 AM is absurd *to a hunter*. When your
agent says which accounts are warm this week, nobody in the building has that
instinct. There is no absurdity check on a plausible number.

**"How long did this take to build?"**
It is about 1,100 lines and two dependencies. The eval suite is 20 cases in a
YAML file. That is the uncomfortable part — this is not the expensive bit.

**"What would have caught it in production?"**
Any one of three, cheapest first: a distributional check on each partition
against last season's baseline; a golden suite split by category so the failure
shape points somewhere; or joining `firmware_events` to anything at all. The
information was recorded the whole time. It was just never joined to something
that mattered.

---

## 8. Troubleshooting a local run

| Symptom | Do this |
|---|---|
| Any command errors | Run `.\demo.ps1 reset`, then retry the failing beat. |
| Numbers look wrong | The warehouse may be in the drifted state. Run `.\demo.ps1 reset`. |
| Agent says "Scout is offline" | A kill switch is still set. `.\demo.ps1 release` |
| Nothing runs at all | `.\setup.ps1` — rebuilds the environment from scratch |
| Need a shorter replay | Run beats 1, 3, 4, 5, and 9 for the core failure sequence. |

The core sequence is beats 1, 3, 4, and 5: green baseline → upstream change →
wrong answer → red eval suite.
