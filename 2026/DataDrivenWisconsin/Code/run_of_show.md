# Demo run of show — 9 minutes

**Slot:** Session 3, Track 2, Direct Supply ITC Great Hall. Demo starts ~minute 20
of a 39-minute talk. Hard stop at minute 29 regardless of where you are.

**Before you walk on:** `.\demo.ps1 reset`. Terminal at 20pt minimum. Notifications
off. Second terminal open on the same directory for the kill switch beat.

**First time on a machine:** `.\setup.ps1` once, then `.\demo.ps1 all` to rehearse.
Plain-language tour of the code is in [WALKTHROUGH.md](WALKTHROUGH.md).

---

## Beat 1 — Scout, working (0:45)

```
.\demo.ps1 1
```

> "This is Scout. Twelve trail cameras, four seasons, a weather feed. One job:
> tell me where and when to sit. Right now it says be in the Bean Field stand at
> six in the morning. Anybody who hunts knows that's the right answer — that's
> civil dawn in October."

## Beat 2 — Green suite (1:00)

```
.\demo.ps1 2
```

> "Twenty golden cases. Eight factual, five temporal, three rules, two refusals,
> two write gates. All green, gate clear. Notice what I am **not** asserting
> anywhere in this file — I am not asserting how the answer is worded. Every
> assertion is a number or a verdict. The day you assert on prose is the day
> your suite fails on a prompt edit, and the week after that is the week
> somebody turns it off."

## Beat 3 — The change (0:45)

```
.\demo.ps1 3
```

> "Vendor pushed firmware 2.2 before last season. Release notes say: capture
> times now recorded in UTC. That's a *true* statement and it's a breaking
> change to every consumer downstream. Eight of twelve cameras took the update.
> Four were out of cell range. Nobody reconciled the fleet."

**Land this:** *No schema change. Every row loads. Nothing errors.*

## Beat 4 — Ask again (1:00)

```
.\demo.ps1 4
```

> "Same question. Same agent. Same code. Be in the stand at **eleven in the
> morning.**"

**Let the silence sit.** Then:

> "Everybody in this room who hunts just flinched, because eleven AM is absurd.
> Hold onto that feeling — because when your agent tells you which accounts are
> warm this week, **nobody in your building has that instinct.** There's no
> absurdity check on a plausible number."

## Beat 5 — Red suite (1:30)

```
.\demo.ps1 5
```

Read the failure signature out loud:

> "Fifteen of twenty. And look at the *shape*. Factual: eight for eight. Rules:
> three for three. Refusals and write gates: clean. Every single failure is a
> temporal aggregation. That's not an alarm, that's a **diagnostic** — it just
> turned a two-day investigation into a twenty-minute one."

Point at T2 and T5:

> "Two of these failed on volume only — the peak hour didn't move at all, but
> sixty-five percent of the detections vanished out of one and half out of the
> other. That's the November daylight saving change pushing drifted dawn past
> noon. If I'd only asserted the headline number, those two would have slid
> through. **Assert the shape, not just the answer.**"

## Beat 6 — Trace archaeology (2:00)

Already on screen from beat 4 — scroll up, or rerun `.\demo.ps1 6`.

> "Here's the whole turn. Planner picked the right intent. Right tool. Right SQL —
> I can read the query that hit the warehouse, it's a span attribute. Correct
> filters, correct grouping. **The agent did nothing wrong.**"

Point at the yellow line:

> "`camera_firmware = {'2.1': 1, '2.2': 2}`. The fleet is mixed and the agent had
> no reason to care. And read the bottom line: **zero errors.**"

> "This is what production AI failure actually looks like. Not an outage. A
> confident answer with a clean trace."

## Beat 7 — Data eval (1:00)

```
.\demo.ps1 7
```

> "One invariant: deer are crepuscular, so at least eighty percent of detections
> should land within a hundred minutes of civil dawn or dusk. Three seasons at
> ninety-two percent. 2025 at thirty-three. Eight cameras below threshold, and
> every one of them is on firmware 2.2."

> "Nothing here violates a schema, so a data contract never fires. You need
> assertions about the **shape** of each partition against a known-good baseline.
> Most agent failures are data failures wearing a costume."

## Beat 8 — Approval gate (1:15)

```
.\demo.ps1 8
```

> "Scout wants to text Justin and Kurt. It doesn't get to. Write tools queue and
> raise — never execute on the agent's own authority."

Note the footer:

> "Zero errors, one held by policy. Those are different columns on purpose. The
> day held writes show up in your error rate is the day somebody 'fixes' the gate
> to clean up a dashboard."

## Beat 9 — Kill switch (0:45)

```
.\demo.ps1 9
```

> "Eval gate went red, so Scout is done until a human clears it. Three levels —
> one tool, one agent, whole fleet. This is an `if` statement and a flag file.
> It is not hard. It's just that nobody asks for it in the demo."

**Close on the terminal, not a chat window.** Last frame should look like DevOps.

---

## Contingencies

| If | Then |
|---|---|
| Terminal font too small | `.\demo.ps1` beats are all under 72 columns — bump to 24pt, don't resize the window |
| Any command errors | `.\demo.ps1 reset` and skip to the recording. Do not debug on stage. |
| Running long at beat 5 | Skip beat 6 (trace) and go straight to beat 7 — the data eval carries the root cause anyway |
| Running very long | Beats 1, 3, 4, 5, 9. That's the whole argument in five minutes. |
| Projector dies | Narrate the recording. The story works as audio. |

## Cut order under time pressure

1. Beat 6 (trace) — beat 7 covers root cause
2. Beat 2 (green suite) — you can assert 20/20 verbally
3. Beat 8 (approval gate) — moves to a slide

**Never cut:** 1, 3, 4, 5. Green → change → wrong → red. That's the talk.
