# retag

A console app that demonstrates AgentOps patterns for bulk content operations:
tracing, aggregation, replay, known-answer canaries, policy holds and stop
levels.

It is not an Orchard module and not a product. There is no `OrchardCore.*`
reference anywhere in it, no database and no web UI — a JSON fixture on disk and
a terminal. The patterns are framework-neutral on purpose; the span attributes
happen to be named `orchard.*` because that is the story they came from.

From *When Workflows Meet Agents*, Orchard Harvest 2026.

## The story

An agent re-classified 4,182 content items across 12 tenants overnight. A
taxonomy was republished mid-run, so 3,082 of those decisions were made against
a version that no longer existed by morning. Nothing errored — the term ids were
identical in both versions, only the classification guidance moved. Three weeks
later somebody noticed that preparedness content was filed under community
events.

## Run it in two minutes

```
dotnet run --project src/Retag -- reset
dotnet run --project src/Retag -- run --taxonomy consolidated-2026 --workers 12
```

**No Azure credentials are needed.** Every command runs fully without them. The
only exceptions are the `--live` ones, which make a real model call and need
`AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT` (plus `AZURE_OPENAI_KEY`
for key auth, or a Managed Identity for token auth). A `.env` file at the repo
root works too.

Requires the .NET 10 SDK. `dotnet run --project src/Retag -- --help` lists
everything.

## What each command demonstrates

| Command | AgentOps concept | Slide |
|---|---|---|
| `run --taxonomy consolidated-2026 --workers 12` | The clean run. Zero errors, and that is the problem | Demo 1, cold open |
| `propose --item <id> --live` | A real model call, with the version it decided under | Demo 1, beat 2 |
| `trace show --item <id> --attrs` | Decision-level attributes on every span | Demo 1, beat 3 |
| `traces group-by taxonomy.version` | Aggregating over the attribute that mattered | Demo 1, beat 4 |
| `replay --from replay-v41.txt --taxonomy 42` | Deterministic re-evaluation into a compensation set | Demo 1, beat 5 |
| `run --lot tenant --canary 40 --release 18` | Known-answer canary gating each lot | Demo 2 |
| `run --lot tenant --gate tenant-boundary` | A policy hold at a blast-radius boundary | Demo 3, beat 1 |
| `stop --job retag-2026 --reason "..."` | A stop that is a flag file and an if-statement | Demo 3, beat 2 |
| `release --job retag-2026` | Clearing the flag | Demo 3, beat 3 |
| `reset` | Back to the green baseline | Between takes |

## The five rungs

| Rung | What it is | Command |
|---|---|---|
| 0 | **Stop.** You can halt it, at three levels | `stop --tool` / `--job` / `--tenant` |
| 1 | **Known answers.** A small set where the answer is agreed | `fixtures/known-answers.json` |
| 2 | **Traces.** Per-decision attributes you can aggregate on | `trace show`, `traces group-by` |
| 3 | **Canary.** The lot does not commit unless the canary passes | `run --lot tenant --canary 40` |
| 4 | **Assertion.** The version is an attribute, so drift is queryable | `traces group-by taxonomy.version` |

Rung 0 is the one people skip. It is deliberately the least sophisticated thing
in this repo: a file in `.run/`, and an `if` at the top of `run`. The thing that
stops the agent should be simpler than the agent.

## What is real and what is a fixture

Be clear about this, because the distinction is the honest framing:

**Fixtures — generated, committed, deterministic.**
The 12 tenants, the 312-term taxonomy in both versions, the 4,182 content items,
and the whole 4,182-decision corpus in `decisions.json`. `retag seed` regenerates
them byte-identically from a fixed seed. No workers ever run; the concurrency is
simulated so the v41/v42 interleaving is visible in the timestamps.

**Real — an actual model call.**
The classification judgment in `propose --item <id> --live`, and in the canary
when you pass `--live`. That is a live call to a real deployment, and under v41
guidance it reproduces the original mistake, which is the part worth watching.

The canary is seeded by default even when credentials are present. A live canary
is cheap, but it does not reproduce the scripted 14/20 — the fixture models
release 18 broadening exactly the argued cases, and a real model does its own
thing. Pass `--live` to see the real thing; leave it off to see the story.

## Steal this

MIT. Fork it, rename the tools to whatever your platform calls them, throw away
the console formatting. The patterns are the point, not the code.

The parts worth keeping: put the version of every input your agent read into a
span attribute; make replay deterministic so a compensation set is trustworthy;
gate lots on known answers; and make a hold a first-class outcome that is not an
error — note that every command here prints `ERRORS` and `HELD BY POLICY` as
separate counters, and a policy hold exits 0.

## Contact

Brian Haydin — [linkedin.com/in/brianhaydin](https://www.linkedin.com/in/brianhaydin/)

If you have your own version of this failure — an agent that did exactly what
you asked against inputs that had quietly moved — I would genuinely like to hear
it. The interesting ones are always the runs that did not error.
