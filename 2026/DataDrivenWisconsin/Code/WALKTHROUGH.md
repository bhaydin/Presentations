# Walking through the code

Written for someone who does not live in Python. Every section gives you the
plain-English version first, the code second, and the sentence to say out loud
third.

You do not need to understand all 1,100 lines. You need to be able to open six
files on a projector and say something true and interesting about each one.

---

## 1. The whole thing in four sentences

Twelve trail cameras write rows into a database. An agent answers questions by
picking a tool, the tool runs SQL, and the answer comes back with a receipt
called a trace. A vendor firmware update quietly shifts timestamps on eight of
the twelve cameras. Nothing errors, the agent keeps answering confidently, and
the only thing that notices is the eval suite.

**Say:** *"There is no magic in here. It's a database, five tools, and a test suite."*

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
.\demo.ps1 all       # full rehearsal, pauses between beats
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
├── demo.ps1               run this on stage
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
> someone asks, "it's what lets `scout/` be addressed as a unit" is a complete
> answer.

---

## 4. Follow one question all the way through

This is the section worth rehearsing. One question, five stops.

**The question:** *"what time should I sit Bean Field in October 2025?"*

### Stop 1 — `agent.py`, `_classify()` (line 151)

Decide what kind of question this is. It looks for keywords and returns one of
six labels: `PEAK`, `COUNT`, `WIND`, `NOTIFY`, `REGULATION`.

"what time" → `PEAK`.

There is a comment worth reading aloud at line 159:

```python
# PEAK must be tested BEFORE wind: "what time should I sit" contains
# "should i sit" and would otherwise be routed to the wind rule engine.
```

**Say:** *"Order matters in a router, and that comment is a bug someone already
paid for."*

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

**Say:** *"The trace is not a log. A log is what the program says about itself.
This is the query that hit the warehouse."*

### Stop 5 — back in `agent.py`, `_run_stub()` (line 173)

Wrap the number in a sentence and hand it back. Green warehouse: `06:00`.
After the firmware rollout: `11:00`. Same code, both times.

---

## 5. File by file

### `scout/schema.sql` — the five tables

Read the comment at the top. It is the whole demo in six lines: `detections`
has **no timezone column**. `captured_at` is a bare timestamp with nothing
recording what clock it came off.

**Say:** *"That one missing column is what makes everything after this possible.
It is also in production in your building right now."*

Also note `sits` (line 57) — the outcome table. Which nights you actually sat,
what you actually saw. Almost nobody has this.

**Say:** *"Without an outcome table you cannot tell a bad recommendation from
bad luck."*

### `scout/build_db.py` — invents the data

Makes four seasons of believable detections. Three things make it believable:

- Deer move at **civil dawn and dusk**, not at fixed clock times, and those
  drift about 90 minutes across a Wisconsin season (`solar.py` does that math).
- Volume peaks in **early November** for the rut (`rut_multiplier`, line 108).
- Bean Field and Ridge get **three cameras each** — which is exactly why a
  partial rollout hurts most at the stands you care about most.

It uses a fixed **seed** (line 24), so it generates identical data every single
time. That is the only reason the eval suite can assert exact integers.

**Say:** *"Same seed, same data, same numbers, every run. If your test data isn't
reproducible you don't have tests, you have vibes."*

### `scout/solar.py` — real dawn and dusk

NOAA sunrise/sunset math for Milwaukee. It exists so the fake data peaks at
*real* twilight for each date.

You will not show this file. You just need one sentence if asked.

**Say:** *"The synthetic data peaks at real civil twilight for Milwaukee on each
specific date, which is why the break looks like a genuine finding instead of
noise."*

### `scout/agent.py` — question in, answer out

Classify, extract, call a tool, phrase the answer. That is the file.

Be honest about the planner. It is a **keyword router**, not a language model.
The docstring at the top says so and so should you:

> *"The planner is stubbed so this runs without wifi. The warehouse, the tools,
> the traces and the evals are all real. And the failure I'm about to show you
> is a **data** failure — swapping in a frontier model does not fix it. That's
> the point."*

Nobody will hold it against you. It makes the argument stronger.

### `scout/tools.py` — the five things Scout can do

| Tool | What it is |
|---|---|
| `query_detections` | plain counting |
| `peak_activity_hour` | **the one that breaks** |
| `get_weather` | someone else's feed, someone else's SLA |
| `check_wind_compatibility` | a rule engine — no model in the path |
| `notify_crew` | texts humans — never runs unattended |

Two of these are worth stopping on.

**`check_wind_compatibility` (line 243)** is pure arithmetic: compare the wind
you have to the wind the stand needs, and if it is more than 90 degrees off, the
answer is NO. No probability, no model, no judgment.

**Say:** *"Some decisions don't get a probability. If this is wrong you walk your
scent into the bedding area and burn the stand for a week — so it's an if
statement, and the eval suite pins it exactly."*

**`notify_crew` (line 303)** never sends anything. Look at the last line: it
queues the request and then **raises** — it deliberately fails.

**Say:** *"The write tool cannot succeed. That's not a limitation, that's the
design."*

### `scout/tracing.py` — the receipt

Spans go to `traces.jsonl` — one JSON object per line.

**Say:** *"A trace is just another fact table. Land it in your lakehouse and
query agent behavior with the SQL you already own."*

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

The state is just files in a `.control/` folder. That is deliberate — you can
trip a kill switch from a second terminal, mid-demo, without restarting
anything.

**Say:** *"This is an if statement and a flag file. It is not hard. It's just
that nobody asks for it in the demo."*

### `scout/apply_firmware_rollout.py` — the break

Eight of twelve cameras get "improved timestamp reliability: capture times now
recorded in UTC." Their timestamps shift five or six hours. Four cameras were
out of cell range and stayed on the old firmware.

Two details carry the whole talk:

**It is partial (line 50).** A clean cutover moves the entire distribution and
any human looking at a chart catches it. A partial rollout leaves the real dawn
peak in place at reduced height and grows a *second* peak five hours later.

**Say:** *"That doesn't look like a bug. That looks like a finding. It looks like
the deer changed their pattern. Plausible failures survive — that's why you need
evals."*

**It is one UPDATE with a CASE, not two UPDATEs.** The comment at line 76
explains: two sequential updates would double-shift any row that crossed the
November daylight-saving boundary. That is a real bug that was hit while
building this. Good throwaway aside if you have ten spare seconds.

### `evals/golden_cases.yaml` — 20 fixed questions

Eight factual, five temporal, three rule, two refusal, two write-gate.

**The thing to point at is what is *not* in the file:** there is not one
assertion about how the answer is *worded*. Every assertion is a number or a
verdict.

**Say:** *"The day you assert on prose is the day your suite fails on a prompt
edit, and the week after that is the week somebody turns it off."*

**The year split is the clever bit.** Factual cases ask about 2023 and 2024.
Temporal cases ask about 2025. The firmware rolled out in September 2025. So
when it breaks, factual stays green and temporal goes red — and the *shape* of
the failure points straight at the cause.

**Say:** *"That's not an alarm. That's a diagnostic. It just turned a two-day
investigation into a twenty-minute one."*

### `evals/run_evals.py` — runs them, prints the signature

The bar chart at the bottom is the slide. `check()` (line 36) is just a list of
comparisons — no cleverness anywhere in it.

It exits non-zero when anything fails, which is what makes it usable as a CI
gate. (`demo.ps1` swallows that so a red suite doesn't stop your script — a red
suite is the demo *working*.)

### `evals/run_data_evals.py` — checks the data, not the agent

One invariant: deer are crepuscular, so at least **80%** of deer detections
should land within 100 minutes of civil dawn or dusk. Three seasons sit at 92%.
2025 comes in at 33%.

Then it breaks the number down per camera and every failing camera is on
firmware 2.2.

**Say:** *"Nothing here violates a schema, so a data contract never fires. Every
row is a perfectly valid timestamp. You need assertions about the **shape** of
each partition against a known-good baseline. Most agent failures are data
failures wearing a costume."*

### `scout/agent_maf.py` — the same agent on Microsoft Agent Framework

Optional, and the answer to the only real objection to this demo.

Everything is shared with the stub — same warehouse, same `tools.py`, same
`controls.py`, same tracer, same twenty golden cases. **Only the planner
changes.** And the failure is identical:

| Warehouse | Stub | MAF + real model |
|---|---|---|
| green | 20/20 | 20/20 |
| after firmware 2.2 | 15/20, temporal 0/5 | 15/20, temporal 0/5 |

Same wrong hours. Same volume collapses. Not one assertion edited.

**Say:** *"That's not the stub. That's Microsoft Agent Framework with a real
model doing real tool calling, against the same twenty assertions. It fails
exactly the same way — because it was never a planning failure."*

Three things worth pointing at if you open the file:

**The tool adapters exist because of one sharp edge.** The framework builds each
tool's JSON schema by reading its Python signature. Our real tools take `tracer`
as their first argument — so a straight pass-through would advertise a `tracer`
field to the model and invite it to hallucinate values into it. The adapters
expose only the arguments the model should choose, and pick the tracer up from a
context variable.

**Say:** *"The model can only see the arguments you put in the signature. That's
a schema, and it's yours to control."*

**The write gate became one decorator argument:**

```python
@tool(approval_mode="always_require")
def notify_crew(message: ...) -> str:
```

The framework will not execute it. The run returns early with a pending request
instead — the same thing `controls.py` does by hand for the stub.

**Say:** *"I hand-rolled this pattern before I knew the framework had it. That's
the tell that it's the right pattern — it's the one everybody arrives at."*

**Refusal is a tool call, not a hoped-for sentence.** `defer_regulatory_question`
is a real tool. The model calls it, so the refusal lands in the trace as a span
and the eval suite can assert on it.

**Say:** *"If your only evidence that the agent refused is that it happened to
say the right words, you can't assert on it and you can't audit it. Make the
refusal a tool call."*

Offline checks that need no model and no credentials:

```powershell
.\.venv-maf\Scripts\python.exe -m evals.test_maf_wiring
```

### `evals/derive_baseline.py` — maintenance only

You will never run this on stage. If you ever change the random seed in
`build_db.py`, this tells you which expected numbers went stale.

It refuses to run against a drifted warehouse — deriving a baseline from broken
data would pin the bug as the expected answer and turn the suite permanently
green on garbage.

---

## 6. Python you will see on screen

Enough to not be caught out, in plain terms.

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

The one worth knowing properly is `with`, because it is on every screen:

```python
with tracer.span("peak_activity_hour", kind="tool") as span:
    ...do the work...
```

**Say:** *"That's a timer that can't be forgotten. It starts when the block
opens and stops when it closes — even if the code inside blows up."*

---

## 7. Questions you will probably get

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

## 8. If something goes wrong on stage

| Symptom | Do this |
|---|---|
| Any command errors | `.\demo.ps1 reset` and move to the next beat. Do not debug in front of the room. |
| Numbers look wrong | You are probably on a drifted warehouse. `.\demo.ps1 reset` |
| Agent says "Scout is offline" | A kill switch is still set. `.\demo.ps1 release` |
| Nothing runs at all | `.\setup.ps1` — rebuilds the environment from scratch |
| Fell badly behind | Beats 1, 3, 4, 5, 9. That is the whole argument in five minutes. |

**Never cut beats 1, 3, 4, 5.** Green → change → wrong → red. That is the talk.
