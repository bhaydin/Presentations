"""
Apply trail camera firmware 2.2 to part of the fleet.

Run this after establishing the green baseline. It mutates the existing
warehouse in place to model an upstream change rather than regenerating the
dataset in its broken state.

    python -m scout.apply_firmware_rollout

WHAT FIRMWARE 2.2 CHANGED
    Release notes say: "Improved timestamp reliability. Capture times are now
    recorded in UTC."

    That is a true statement. It is also a breaking change to every downstream
    consumer, and it shipped as a patch release with no schema change, so:
      - no column was added or removed
      - no data contract fires
      - no ingestion validation fails
      - every row still loads
      - every query still returns

    Eight of twelve cameras took the over-the-air update before the 2025
    season. Four were out of cell range and stayed on 2.1. Nobody reconciled
    the fleet.

WHY PARTIAL MATTERS
    A clean cutover shifts the whole distribution five hours and any human
    eyeballing a chart would catch it. A PARTIAL rollout leaves the real dawn
    peak in place at reduced amplitude and grows a second peak at dawn+5.
    That looks like a finding. It looks like the deer changed their pattern.

    Plausible failures survive. That is why you need evals.

To undo: python -m scout.build_db
"""

from __future__ import annotations

from datetime import date
from pathlib import Path

import duckdb

from scout.solar import utc_offset_hours

DB_PATH = Path(__file__).resolve().parent.parent / "scout.duckdb"

# Eight of twelve cameras took the OTA update. Note that every multi-camera
# stand ends up MIXED -- Bean Field has 1 and 2 drifted but 3 clean.
DRIFTED_CAMERAS = [1, 2, 4, 5, 7, 9, 11, 12]
CLEAN_CAMERAS = [3, 6, 8, 10]

ROLLOUT_DATE = date(2025, 9, 3)  # pushed just before the 2025 season opener

RELEASE_NOTES = "Improved timestamp reliability. Capture times now recorded in UTC."


def main() -> None:
    if not DB_PATH.exists():
        raise SystemExit("scout.duckdb not found. Run: python -m scout.build_db")

    con = duckdb.connect(str(DB_PATH))

    affected = con.execute(
        """
        SELECT COUNT(*) FROM detections
        WHERE camera_id IN ?
          AND captured_at >= ?
        """,
        [DRIFTED_CAMERAS, ROLLOUT_DATE],
    ).fetchone()[0]

    # CDT is -5, CST is -6. Local -> UTC means ADDING the offset back.
    # The firmware does this correctly. Nothing downstream knows it happened.
    #
    # This MUST be a single statement with a CASE. Two sequential UPDATEs would
    # double-shift rows that cross the DST boundary when the first one moves
    # them -- a real implementation bug encountered while building this example.
    con.execute(
        """
        UPDATE detections
        SET captured_at = captured_at + (
            CASE
                WHEN captured_at < TIMESTAMP '2025-11-02 02:00:00'
                    THEN INTERVAL 5 HOUR
                ELSE INTERVAL 6 HOUR
            END
        )
        WHERE camera_id IN ?
          AND captured_at >= ?
        """,
        [DRIFTED_CAMERAS, ROLLOUT_DATE],
    )

    con.execute(
        "UPDATE cameras SET firmware_version = '2.2' WHERE camera_id IN ?",
        [DRIFTED_CAMERAS],
    )

    # The audit trail. The information WAS recorded. It was simply never
    # joined to anything that mattered.
    con.executemany(
        "INSERT INTO firmware_events VALUES (?, ?, ?, ?, ?, ?)",
        [
            (i + 1, cam, ROLLOUT_DATE, "2.1", "2.2", RELEASE_NOTES)
            for i, cam in enumerate(DRIFTED_CAMERAS)
        ],
    )

    con.close()

    print("Firmware 2.2 rollout applied.")
    # Split across two lines so the full release note remains easy to scan in
    # terminal output.
    print('  release notes:   "Improved timestamp reliability.')
    print('                    Capture times now recorded in UTC."')
    print(f"  updated cameras: {DRIFTED_CAMERAS}")
    print(f"  still on 2.1:    {CLEAN_CAMERAS}")
    print(f"  rows shifted:    {affected:,}")
    print()
    print("Rows loaded: all of them.  Schema changes: none.  Errors: none.")
    print("Every query below will still return a confident answer.")


if __name__ == "__main__":
    main()
