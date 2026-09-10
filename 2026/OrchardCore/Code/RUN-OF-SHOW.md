# retag — run of show

For the recording desk tonight and the stage tomorrow. Three sessions, one per
demo slide.

---

## Recording setup

Do this once, before the first take.

**Terminal**

- Width **72 columns exactly.** Every line in this app is built to fit 72. If
  anything wraps, the terminal is narrower than 72, not wider.
- Font large enough that it looks absurd on your own screen. If it looks
  comfortable locally it is too small for the room.
- Dark background. The app uses green for pass, amber for the one thing to look
  at, default white for everything else.
- Clear scrollback between takes so the frame starts empty.

**The `retag` command**

The slides show `$ retag run ...`. Build Release once and alias it so the prompt
on screen matches:

```powershell
dotnet build src/Retag -c Release
function retag { & "$PWD\src\Retag\bin\Release\net10.0\Retag.exe" @args }
```

Bash equivalent:

```bash
dotnet build src/Retag -c Release
retag() { ./src/Retag/bin/Release/net10.0/Retag.exe "$@"; }
```

**Before every take**

```
retag reset
```

Half a second, and it puts the flags, the output files and the corpus back to
the green baseline. If you take nothing else from this page, take this line.

**Before the first take of the day**

```
./scripts/validate.ps1
```

Runs the suite and diffs every command against the golden files. Roughly 30
seconds. If it says `VALIDATE OK`, the numbers on screen match the numbers on
the slides.

---

## Demo 1 — the spine (target 2:00)

Everything after this demo depends on the audience believing the run was clean.
Do not rush the cold open.

| # | Command | Runtime | Point at | Say |
|---|---|---|---|---|
| 1 | `retag run --taxonomy consolidated-2026 --workers 12` | ~1.5s | `ERRORS:    0` | "Four thousand items, twelve tenants, forty-one minutes, zero errors. This is the run that caused the incident." |
| 2 | `retag propose --item north-shore-council/emergency-services --live` | 2–5s | `source ... live model call` then `decision.chosen` | "That is a real model call, right now. Given the old guidance it makes the same choice it made that night." |
| 3 | `retag trace show --item north-shore-council/emergency-services --attrs` | <1s | the amber `taxonomy.version ... 41` | "Every decision carries the version of the taxonomy it read. That attribute is the whole talk." |
| 4 | `retag traces group-by taxonomy.version` | <1s | `41   3,082   SUPERSEDED` | "Three thousand and eighty-two decisions against a taxonomy that no longer existed. Nothing errored, so nothing told us." |
| 5 | `retag replay --from replay-v41.txt --taxonomy 42` | <1s | `proposals differ ... 380` | "Replay them against the current version. Two thousand seven hundred were fine. Three hundred and eighty need compensating. That is a work item, not an incident." |

**Beat 4 must run before beat 5.** Group-by writes `replay-v41.txt`; replay reads
it. If you skip 4, beat 5 tells you so instead of printing numbers.

**What has gone wrong if the output differs**

- Beat 1 shows anything other than `4,182` / `ERRORS: 0` → `retag reset`.
- Beat 2 errors or hangs → drop `--live`. The seeded output is identical in shape
  and still shows `decision.chosen ... community-events`. Nobody can tell.
- Beat 3 shows `taxonomy.version ... 42` → you are on the wrong item. The slide
  item is `north-shore-council/emergency-services` and it is always v41.
- Beat 4 shows anything but `1,100` / `3,082` / `4,182` → `retag reset`, then
  `retag seed`.
- Beat 5 shows anything but `2,702` / `380` → same.

---

## Demo 2 — the canary (target 2:00)

The load-bearing moment is the *absence* of a commit. Give the last two lines
room.

| # | Command | Runtime | Point at | Say |
|---|---|---|---|---|
| 1 | `retag run --lot tenant --canary 40 --release 17` | <1s | twelve `COMMITTED` lines | "Release seventeen. Every tenant opens with a forty-item canary, twenty of them known answers. Twenty out of twenty, twelve times, and it commits." |
| 2 | *(no command — slide showing the release 18 diff)* | — | "Prefer broader terms. Editors find deep hierarchies confusing." | "One sentence added to the instructions. It is not wrong. It is a reasonable thing for a human to ask for." |
| 3 | `retag run --lot tenant --canary 40 --release 18` | <1s | the three category bars | "Obvious cases: fine. Human-verified: fine. Argued: two out of eight." |
| 4 | *(same frame)* | — | `14/20 — every failure chose the parent term` | "Every single failure picked the parent instead of the specific term. That is not a bug. That is the instruction working." |
| 5 | *(same frame)* | — | `0 items committed for this tenant.` | "Zero. Not committed and rolled back — never committed. The proposals are still drafts, and three tenants of good work are still committed behind us." |

Beat 3 lands on tenant 4 because release 18 was picked up part-way through the
rollout. That is deliberate: it shows the blast radius stopping at a lot
boundary, not at the start of the job.

**What has gone wrong if the output differs**

- It holds at tenant 1 instead of tenant 4 → you passed `--live` and a real model
  is answering the known-answer cases. Drop `--live`. See the note below.
- Any number other than `6/6`, `6/6`, `2/8`, `14/20` → `retag reset`.
- `see fixtures/known-answers.json` instead of `every failure chose the parent
  term` → a failure did not choose the parent, which only happens under `--live`.

**On `--live` in demo 2.** The canary is seeded by default, on purpose. A live
canary is cheap and it works, but it does not reproduce 14/20 — the fixture
models release 18 broadening exactly the argued cases, and a real model does its
own thing (measured: 18/20, held at tenant 1, 57 seconds). The slides say 14/20.
Leave `--live` off for this demo. Beat 2 of demo 1 is where the live call earns
its place.

---

## Demo 3 — hold and stop (target 1:30)

| # | Command | Runtime | Point at | Say |
|---|---|---|---|---|
| 1 | `retag run --lot tenant --gate tenant-boundary` | <1s | `[HELD BY POLICY]` | "It finished a lot, saw the next one crosses into another tenant, and stopped to ask." |
| 2 | `echo $LASTEXITCODE` | instant | `0` | "Exit code zero. A hold is a success. If your platform treats a hold as a failure, your on-call rota will teach people to approve everything." |
| 3 | `retag stop --job retag-2026 --reason "argued cases red"` | <1s | `flag written: .run/retag-2026.stopped` | "This is a file. That is the entire mechanism." |
| 4 | *(same frame)* | — | `seo-audit  RUNNING` / `link-check  RUNNING` | "Scoped to one job. The other two are still running. Three levels: tool, job, tenant." |
| 5 | `retag run --taxonomy consolidated-2026 --workers 12` | <1s | `❯ STOPPED BY FLAG.` | "A flag file, and an `if` at the top of run. The thing that stops the agent should be simpler than the agent." |
| 6 | `retag release --job retag-2026` | <1s | `retag-2026  RUNNING` | "And back." |

Beat 2 only works in PowerShell. In bash it is `echo $?`, and it must be the very
next command after beat 1.

**What has gone wrong if the output differs**

- Beat 1 prints the job output instead of the hold → you dropped
  `--gate tenant-boundary`.
- Beat 2 prints anything but `0` → stop the take. This is the one number in the
  talk that the tests exist to protect.
- Beat 5 prints the job output → beat 3 did not write the flag. Check `.run/`.

---

## If a recording runs long

Drop in this order. Demo 1's minimum is beats 4 and 5 — the group-by and the
replay. Everything else in demo 1 is setup for those two frames.

| Drop | Cost |
|---|---|
| Demo 1 beat 2 (`--live`) | Loses the "this is a real model" proof. Cheapest cut, and the riskiest beat anyway. |
| Demo 1 beat 3 (`trace show`) | Acceptable — beat 4's `SUPERSEDED` column carries the attribute story on its own. |
| Demo 1 beat 1 (the cold open) | Only if desperate. Open on the group-by and describe the clean run in a sentence. |
| Demo 2 beat 1 (release 17) | Fine. Say "under the previous release this was twenty out of twenty" over beat 3. |
| Demo 3 beats 3–6 (stop) | Keep 1 and 2. The hold and the exit code are the argument; the stop flag is the punchline. |

**Never cut:** demo 1 beat 4, demo 1 beat 5, demo 2 beat 5, demo 3 beat 2.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Output wraps | Terminal narrower than 72 columns, or font too large | Widen to 72 or shrink the font. Every line is built to fit 72. |
| Numbers differ from the slides | Fixtures mutated by a previous take | `retag reset`, then `retag seed` |
| A command refuses to run | A stop flag is still set from an earlier take | `retag release --job retag-2026`, or `retag reset` |
| `❯ STOPPED BY FLAG` when you did not expect it | Same | Same |
| Live call fails or hangs | Endpoint, key or deployment | Drop `--live`. Seeded output is identical in shape. |
| `live call unavailable - using seeded canary` | Credentials present but the call failed | Harmless — it already fell back. Drop `--live` for the retake. |
| `fixtures missing` | Fresh clone, nothing seeded | `retag seed` |
| Canary holds at tenant 1 | `--live` on demo 2 | Drop `--live` |
| Colours missing | `NO_COLOR` is set, or output is piped | `unset NO_COLOR`, and do not pipe |
| Numbers right, formatting looks off | Something changed since the goldens | `./scripts/validate.ps1` will name the command |

---

## Stills checklist

Four frames to screenshot for the A1 fallback slides. Take them at final font
size, dark background, 72 columns, after a `retag reset`.

- [ ] **The version attribute.** `retag trace show --item north-shore-council/emergency-services --attrs` — the amber `taxonomy.version ... 41` line, with the full span tree above it.
- [ ] **The group-by.** `retag traces group-by taxonomy.version` — the whole table including the `41   3,082   SUPERSEDED` row and the `→ 3,082 item ids written` line.
- [ ] **The held canary.** `retag run --lot tenant --canary 40 --release 18` — the full frame: three `COMMITTED` lines, the three category bars, `14/20`, and `0 items committed for this tenant.`
- [ ] **The two-counter footer.** `retag run --lot tenant --gate tenant-boundary` — the `[HELD BY POLICY]` block with `ERRORS: 0  |  HELD BY POLICY: 1` underneath.

If you only get one: the group-by. It is the frame the whole talk turns on.
