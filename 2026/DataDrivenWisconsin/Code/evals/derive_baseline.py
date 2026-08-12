"""
Re-derive the expected numbers in golden_cases.yaml from the GREEN warehouse.

    python -m evals.derive_baseline

You need this exactly once per lifetime of the demo: when you change the seed,
the season list, or the volume constants in build_db.py, every expected integer
in golden_cases.yaml goes stale. Rather than hand-editing twenty cases, run
this and paste the numbers it prints.

WHY IT ONLY PRINTS, AND NEVER REWRITES THE FILE
golden_cases.yaml is more comment than code. The comments are the teaching --
why the years are split, why volume is asserted alongside the peak hour. A
YAML round-trip would silently eat all of it. So this prints a patch for you
to apply by eye.

SAFETY RAIL
Deriving a baseline from a DRIFTED warehouse would bake the bug into the
suite as the expected answer -- the single worst thing you can do to an eval
suite, and an easy mistake to make at 11pm the night before. So this refuses
to run unless all twelve cameras are on firmware 2.1.
"""

from __future__ import annotations

from pathlib import Path

import duckdb
import yaml

from scout.agent import ask
from scout.tracing import c

CASES_PATH = Path(__file__).resolve().parent / "golden_cases.yaml"
DB_PATH = Path(__file__).resolve().parent.parent / "scout.duckdb"

# Assertion keys whose values come out of the agent's structured result.
DERIVED_KEYS = ["detections", "animals", "peak_hour", "verdict", "degrees_off"]


def assert_green() -> None:
    """Refuse to derive a baseline from a warehouse that has already drifted."""
    if not DB_PATH.exists():
        raise SystemExit("warehouse not built -- run: python -m scout.build_db")

    con = duckdb.connect(str(DB_PATH), read_only=True)
    try:
        fleet = dict(
            con.execute(
                "SELECT firmware_version, COUNT(*) FROM cameras GROUP BY 1"
            ).fetchall()
        )
    finally:
        con.close()

    if sorted(fleet) != ["2.1"]:
        print()
        print(c("  REFUSING TO DERIVE A BASELINE", "red", "bold"))
        print(c(f"  fleet firmware: {fleet}", "red"))
        print(
            c(
                "  This warehouse has already drifted. Deriving now would pin\n"
                "  the BUG as the expected answer and your suite would go\n"
                "  permanently green on broken data.",
                "red",
            )
        )
        print()
        print(c("  Rebuild green first:  python -m scout.build_db", "yellow", "bold"))
        print()
        raise SystemExit(1)


def main() -> None:
    assert_green()

    spec = yaml.safe_load(CASES_PATH.read_text())

    print()
    print(c(f"  DERIVED BASELINE  {spec['suite']}", "bold"))
    print(c("  fleet: all twelve cameras on firmware 2.1 (green)", "dim"))
    print(c("  " + "=" * 68, "dim"))

    changed = 0
    for case in spec["cases"]:
        expect = case["expect"]
        result = ask(case["question"])
        observed = result.structured

        deltas = []
        for key in DERIVED_KEYS:
            if key not in expect:
                continue
            was, now = expect[key], observed.get(key)
            if now is None:
                continue
            if was != now:
                deltas.append(f"{key}: {was} -> {now}")
                changed += 1

        if deltas:
            print(f"  {c(case['id'], 'yellow', 'bold'):<4} {case['question'][:52]}")
            for d in deltas:
                print(c(f"         {d}", "yellow"))
        else:
            print(f"  {c(case['id'], 'green'):<4} {case['question'][:52]}")

    print(c("  " + "=" * 68, "dim"))
    if changed:
        print(c(f"  {changed} expected values are stale.", "yellow", "bold"))
        print(c("  Edit evals/golden_cases.yaml by hand using the arrows above.", "dim"))
    else:
        print(c("  golden_cases.yaml matches the green warehouse exactly.", "green", "bold"))
    print()


if __name__ == "__main__":
    main()
