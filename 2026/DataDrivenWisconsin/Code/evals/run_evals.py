"""
Run the golden suite. Exits non-zero on failure so it can be a CI gate.

    python -m evals.run_evals
    python -m evals.run_evals --verbose
    python -m evals.run_evals --category temporal

Output is tuned for a projector. The category rollup at the bottom is the
part that matters on stage: it turns "something is broken" into "the temporal
aggregations are broken and nothing else is," which is a twenty minute
investigation instead of a two day one.
"""

from __future__ import annotations

import sys
from pathlib import Path

import yaml

from scout import controls
from scout.agent import ask
from scout.tracing import c

CASES_PATH = Path(__file__).resolve().parent / "golden_cases.yaml"

CATEGORY_ORDER = ["factual", "temporal", "rule", "refusal", "write_gate"]


def within(actual: float, expected: float, tolerance_pct: float) -> bool:
    if tolerance_pct <= 0:
        return actual == expected
    return abs(actual - expected) <= abs(expected) * (tolerance_pct / 100.0)


def check(case: dict, planner: str = "stub") -> tuple[bool, list[str]]:
    """Return (passed, failure_reasons)."""
    expect = case["expect"]
    tol = float(expect.get("tolerance_pct", 0))
    reasons: list[str] = []

    result = ask(case["question"], planner=planner)
    s = result.structured

    if "intent" in expect and result.intent != expect["intent"]:
        reasons.append(f"intent {result.intent} != {expect['intent']}")

    if "deferred" in expect and bool(result.deferred) != bool(expect["deferred"]):
        reasons.append(f"deferred={result.deferred}, expected {expect['deferred']}")

    if "approval_required" in expect and bool(result.approval_required) != bool(
        expect["approval_required"]
    ):
        reasons.append(
            f"approval_required={result.approval_required}, "
            f"expected {expect['approval_required']}"
        )

    if "error_contains" in expect:
        if not result.error or expect["error_contains"] not in result.error:
            reasons.append(f"error missing substring {expect['error_contains']!r}")

    for phrase in expect.get("answer_must_not_contain", []):
        if phrase.lower() in result.answer.lower():
            reasons.append(f"answer leaked forbidden phrase {phrase!r}")

    if "detections" in expect:
        actual = s.get("detections")
        if actual is None:
            reasons.append("no detections value returned")
        elif not within(actual, expect["detections"], tol):
            delta = actual - expect["detections"]
            pct = (delta / expect["detections"] * 100) if expect["detections"] else 0
            reasons.append(
                f"detections {actual} != {expect['detections']} "
                f"({delta:+d}, {pct:+.0f}%)"
            )

    if "animals" in expect:
        actual = s.get("animals")
        if actual is None:
            reasons.append("no animals value returned")
        elif not within(actual, expect["animals"], tol):
            reasons.append(f"animals {actual} != {expect['animals']}")

    if "peak_hour" in expect:
        actual = s.get("peak_hour")
        if actual != expect["peak_hour"]:
            reasons.append(f"peak_hour {actual} != {expect['peak_hour']}")

    if "peak_hour_window" in expect:
        lo, hi = expect["peak_hour_window"]
        actual = s.get("peak_hour")
        if actual is None or not (lo <= actual <= hi):
            reasons.append(
                f"peak_hour {actual} outside plausible twilight window [{lo},{hi}]"
            )

    if "verdict" in expect and s.get("verdict") != expect["verdict"]:
        reasons.append(f"verdict {s.get('verdict')!r} != {expect['verdict']!r}")

    if "degrees_off" in expect and s.get("degrees_off") != expect["degrees_off"]:
        reasons.append(f"degrees_off {s.get('degrees_off')} != {expect['degrees_off']}")

    return (not reasons), reasons


def main() -> None:
    verbose = "--verbose" in sys.argv
    only = None
    if "--category" in sys.argv:
        only = sys.argv[sys.argv.index("--category") + 1]

    # The SAME twenty cases run against either planner. Not one assertion in
    # golden_cases.yaml changes -- which is the point of asserting on numbers
    # and verdicts instead of on prose.
    planner = "stub"
    if "--planner" in sys.argv:
        planner = sys.argv[sys.argv.index("--planner") + 1]

    controls.reset()
    controls.ensure_state()

    spec = yaml.safe_load(CASES_PATH.read_text())
    cases = [k for k in spec["cases"] if only is None or k["category"] == only]

    print()
    print(c(f"  {spec['suite']}   {len(cases)} cases   planner: {planner}", "bold"))
    print(c("  baseline: " + spec["baseline_state"], "dim"))
    print(c("  " + "=" * 68, "dim"))

    results = []
    for case in cases:
        passed, reasons = check(case, planner=planner)
        results.append((case, passed, reasons))

        mark = c(" PASS ", "green", "bold") if passed else c(" FAIL ", "red", "bold")
        # Pad FIRST, colour SECOND. Colouring first puts invisible escape
        # codes inside the field width, so ":<24" was really padding to about
        # eleven visible characters and the whole row ran three columns long.
        category = c(f"{case['category']:<11}", "dim")
        print(f"  {mark} {case['id']:<4} {category} {case['question'][:42]}")
        if not passed:
            for r in reasons:
                print(c(f"           -> {r}", "red"))
        elif verbose:
            print(c(f"           {case['expect']}", "dim"))

    passed_n = sum(1 for _, p, _ in results if p)
    failed_n = len(results) - passed_n

    print(c("  " + "=" * 68, "dim"))

    # The diagnostic rollup. This is the slide.
    print(c("  FAILURE SIGNATURE BY CATEGORY", "bold"))
    for cat in CATEGORY_ORDER:
        cat_results = [(cs, p) for cs, p, _ in results if cs["category"] == cat]
        if not cat_results:
            continue
        n_pass = sum(1 for _, p in cat_results if p)
        n = len(cat_results)
        bar = ("█" * n_pass) + ("░" * (n - n_pass))
        line = f"    {cat:<12} {bar}  {n_pass}/{n}"
        print(c(line, "green") if n_pass == n else c(line, "red", "bold"))

    print()
    if failed_n:
        print(c(f"  {passed_n} passed, {failed_n} FAILED", "red", "bold"))
        print(c("  Gate: BLOCKED. This build does not ship.", "red", "bold"))
    else:
        print(c(f"  {passed_n} passed, 0 failed", "green", "bold"))
        print(c("  Gate: CLEAR.", "green", "bold"))
    print()

    raise SystemExit(1 if failed_n else 0)


if __name__ == "__main__":
    main()
