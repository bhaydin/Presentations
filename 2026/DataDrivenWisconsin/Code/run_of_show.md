# Replaying the demo — 9-minute sequence

This guide reproduces the sequence used in the Data-Driven Wisconsin session.
Each beat pairs a command with the behavior and design point to inspect.

Start from the green baseline with `.\demo.ps1 reset`. A second terminal in the
same directory is useful for experimenting with the kill switch in beat 9.

**First run on a machine:** Run `.\setup.ps1` once. `.\demo.ps1 all` then replays
the full sequence with a pause between beats.
Plain-language tour of the code is in [WALKTHROUGH.md](WALKTHROUGH.md).

---

## Beat 1 — Scout, working (0:45)

```
.\demo.ps1 1
```

Scout analyzes twelve trail cameras, four seasons, and a weather feed to
recommend where and when to sit. Against the green baseline it recommends the
Bean Field stand at 06:00, which aligns with civil dawn in October.

## Beat 2 — Green suite (1:00)

```
.\demo.ps1 2
```

The suite contains twenty golden cases: eight factual, five temporal, three
rules, two refusals, and two write gates. All pass against the baseline. The
cases assert numbers and verdicts rather than wording, which keeps harmless
prompt edits from making the suite brittle.

## Beat 3 — The change (0:45)

```
.\demo.ps1 3
```

Firmware 2.2 changes capture times to UTC. Although the release note is
accurate, the change breaks downstream assumptions. Eight of twelve cameras
receive the update; four remain on 2.1, leaving an unreconciled mixed fleet.

**What to notice:** *No schema changes. Every row loads. Nothing errors.*

## Beat 4 — Ask again (1:00)

```
.\demo.ps1 4
```

The same question, agent, and code now produce an **11:00** recommendation.

**Why the answer matters:**

An experienced hunter recognizes 11:00 as implausible, but many business-agent
recommendations have no nearby expert with the same instinct. A plausible
number can therefore escape an informal absurdity check.

## Beat 5 — Red suite (1:30)

```
.\demo.ps1 5
```

**How to interpret the failure signature:**

Fifteen of twenty cases pass. Factual and rule cases remain green, as do the
refusal and write-gate cases. Every failure is a temporal aggregation, making
the suite a **diagnostic** that sharply narrows the investigation.

**What T2 and T5 add:**

Two cases fail on volume only: the peak hour stays fixed while 65% of one
case's detections and roughly half of the other's disappear. The November
daylight-saving change pushes drifted dawn past noon. Checking both hour and
count catches failures that a headline-only assertion would miss.

## Beat 6 — Trace archaeology (2:00)

The trace is included in beat 4 output. Beat 6 prints it again for focused
inspection.

The trace shows the correct intent, tool, SQL, filters, and grouping. The exact
query is preserved as a span attribute. **The agent did nothing wrong.**

**Field that explains the failure:**

`camera_firmware = {'2.1': 1, '2.2': 2}` shows that the queried fleet is mixed.
The agent has no reason to treat that as an error, and the trace reports
**zero errors**.

This is a representative production AI failure mode: not an outage, but a
confident answer accompanied by a clean trace.

## Beat 7 — Data eval (1:00)

```
.\demo.ps1 7
```

The data eval encodes one invariant: because deer are crepuscular, at least 80%
of detections should fall within 100 minutes of civil dawn or dusk. Three
seasons measure 92%; 2025 measures 33%. All eight cameras below the threshold
run firmware 2.2.

No schema is violated, so a structural data contract does not fire. This class
of failure requires assertions about the **shape** of each partition against a
known-good baseline.

## Beat 8 — Approval gate (1:15)

```
.\demo.ps1 8
```

Scout proposes a message to Justin and Kurt, but the write tool queues the
request and raises instead of executing on the agent's authority.

**How to interpret the footer:**

The footer reports zero errors and one policy hold as separate values. Treating
a successful hold as an error would create pressure to weaken the gate merely
to improve an error-rate dashboard.

## Beat 9 — Kill switch (0:45)

```
.\demo.ps1 9
```

Once the eval gate turns red, Scout remains unavailable until a human clears
it. The example includes tool-, agent-, and fleet-level switches implemented
with an `if` statement and a flag file.

The final state demonstrates that operational controls belong alongside the
agent rather than only in its conversational interface.

---

## Troubleshooting a replay

| If | Then |
|---|---|
| Terminal output wraps | Widen the terminal or reduce its font size; each beat is designed for 72 columns. |
| A command errors | Run `.\demo.ps1 reset`, then retry the failing beat. |
| Numbers differ from the guide | Run `.\demo.ps1 reset` to rebuild the known green warehouse. |
| Scout reports that it is offline | Run `.\demo.ps1 release` to clear a remaining kill switch. |
| A shorter replay is needed | Beats 1, 3, 4, 5, and 9 preserve the core argument. |

## Shorter replay options

For a shorter path, omit these beats in order:

1. Beat 6, because beat 7 also identifies the root cause.
2. Beat 2, because beat 1 already establishes the green behavior.
3. Beat 8, because the approval gate is independent of the timestamp failure.

Beats 1, 3, 4, and 5 form the minimum causal sequence: green baseline →
upstream change → wrong answer → red eval suite.
