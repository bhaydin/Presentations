"""
Data-layer evals. Run these BEFORE the agent evals.

Most agent failures are data failures wearing a costume. Your agent is a
downstream consumer of a pipeline -- instrument it like one.

THE INVARIANT
    Whitetail deer are crepuscular. Detections must cluster around civil dawn
    and civil dusk. If at least 80% of detections are not inside a twilight
    window, something upstream is wrong and no amount of prompt engineering
    will fix it.

This is a DISTRIBUTIONAL assertion, not a row-level one. Data contracts catch
schema violations. Nothing here violates a schema -- every row is a valid
timestamp. You need assertions about the SHAPE of the data, per partition,
compared to a known-good baseline.

    python -m evals.run_data_evals

Exits non-zero on violation so it can gate a pipeline run.
"""

from __future__ import annotations

from datetime import timedelta
from pathlib import Path

import duckdb

from scout.solar import civil_dawn, civil_dusk
from scout.tracing import c

DB_PATH = Path(__file__).resolve().parent.parent / "scout.duckdb"

WINDOW_MINUTES = 100
THRESHOLD = 0.80


def adherence_rows(con):
    return con.execute(
        """
        SELECT d.camera_id,
               c.stand_name,
               c.firmware_version,
               EXTRACT(year FROM d.captured_at) AS season,
               d.captured_at
        FROM detections d
        JOIN cameras c USING (camera_id)
        WHERE d.species = 'whitetail deer'
        """
    ).fetchall()


def in_twilight(ts) -> bool:
    d = ts.date()
    for anchor in (civil_dawn(d), civil_dusk(d)):
        if abs((ts - anchor).total_seconds()) <= WINDOW_MINUTES * 60:
            return True
    return False


def main() -> None:
    if not DB_PATH.exists():
        raise SystemExit("warehouse not built -- run: python -m scout.build_db")

    con = duckdb.connect(str(DB_PATH), read_only=True)
    rows = adherence_rows(con)

    by_season: dict[int, list[int]] = {}
    by_camera: dict[int, list] = {}

    for cam, stand, fw, season, ts in rows:
        hit = 1 if in_twilight(ts) else 0
        by_season.setdefault(int(season), []).append(hit)
        by_camera.setdefault(cam, [stand, fw, 0, 0])
        by_camera[cam][2] += hit
        by_camera[cam][3] += 1

    print()
    print(c("  DATA EVAL  twilight adherence", "bold"))
    print(
        c(
            f"  invariant: >= {THRESHOLD:.0%} of deer detections within "
            f"+/-{WINDOW_MINUTES} min of twilight",
            "dim",
        )
    )
    print(c("  " + "=" * 68, "dim"))

    violations = []
    print(c("  BY SEASON", "bold"))
    for season in sorted(by_season):
        hits = by_season[season]
        rate = sum(hits) / len(hits)
        ok = rate >= THRESHOLD
        bar = "█" * int(rate * 40) + "░" * (40 - int(rate * 40))
        line = f"    {season}  {bar} {rate:6.1%}  n={len(hits):,}"
        print(c(line, "green") if ok else c(line, "red", "bold"))
        if not ok:
            violations.append(season)

    print()
    print(c("  BY CAMERA  (all seasons pooled)", "bold"))
    for cam in sorted(by_camera):
        stand, fw, hits, total = by_camera[cam]
        rate = hits / total
        ok = rate >= THRESHOLD
        flag = "" if ok else c("  <-- SUSPECT", "red", "bold")
        line = (
            f"    cam {cam:>2}  {stand:<13} fw {fw}   "
            f"{rate:6.1%}  n={total:,}"
        )
        print((c(line, "green") if ok else c(line, "red", "bold")) + flag)

    print(c("  " + "=" * 68, "dim"))

    if violations:
        suspects = [
            (cam, v[1]) for cam, v in sorted(by_camera.items())
            if v[2] / v[3] < THRESHOLD
        ]
        print(c(f"  VIOLATION in seasons: {violations}", "red", "bold"))
        print(
            c(
                f"  {len(suspects)} of {len(by_camera)} cameras below threshold: "
                f"{[s[0] for s in suspects]}",
                "red",
                "bold",
            )
        )
        firmware_of_suspects = sorted({s[1] for s in suspects})
        print(
            c(
                f"  every suspect camera is on firmware {firmware_of_suspects}. "
                "Start there.",
                "yellow",
                "bold",
            )
        )
        print()
        raise SystemExit(1)

    print(c("  All partitions within tolerance.", "green", "bold"))
    print()
    raise SystemExit(0)


if __name__ == "__main__":
    main()
