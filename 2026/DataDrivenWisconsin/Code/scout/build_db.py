"""
Build the Scout warehouse from scratch. Deterministic: same seed, same data,
every run. That matters because your golden eval cases assert exact integers.

    python -m scout.build_db

Produces scout.duckdb in the repo root, with all twelve cameras on firmware
2.1 and clean local timestamps. This is the GREEN state.

To create the break, run apply_firmware_rollout.py afterwards.
"""

from __future__ import annotations

import math
import random
from datetime import date, datetime, timedelta
from pathlib import Path

import duckdb

from scout.solar import civil_dawn, civil_dusk

SEED = 20260812
DB_PATH = Path(__file__).resolve().parent.parent / "scout.duckdb"
SCHEMA_PATH = Path(__file__).resolve().parent / "schema.sql"

SEASONS = [2022, 2023, 2024, 2025]
SEASON_START = (9, 15)
SEASON_END = (12, 31)

CARDINALS = [
    ("N", 0), ("NNE", 22), ("NE", 45), ("ENE", 67),
    ("E", 90), ("ESE", 112), ("SE", 135), ("SSE", 157),
    ("S", 180), ("SSW", 202), ("SW", 225), ("WSW", 247),
    ("W", 270), ("WNW", 292), ("NW", 315), ("NNW", 337),
]

STANDS = [
    # name,          type,           ideal wind, access route,            notes
    ("Bean Field",   "ladder",       "NW",  "north fence line",      "Ag edge. Highest volume camera set."),
    ("Ridge",        "saddle",       "W",   "west logging two-track", "Thermals swirl after 9am."),
    ("Creek Bottom", "ground blind", "NE",  "creek crossing east",   "Wet approach. Loud in dry leaves."),
    ("Oak Flat",     "ladder",       "SW",  "south gate",            "White oaks. Boom or bust by acorn year."),
    ("Cedar Swamp",  "saddle",       "N",   "swamp edge trail",      "Late season bedding. Sit all day."),
    ("Power Line",   "ground blind", "SE",  "utility easement",      "Travel corridor, not a destination."),
]

# 12 cameras across 6 stands. Bean Field and Ridge get three each -- the
# high-value sets -- which is exactly why a partial rollout hurts there most.
CAMERAS = [
    (1,  "Bean Field",   "north over the field edge"),
    (2,  "Bean Field",   "west along the fence"),
    (3,  "Bean Field",   "south on the entry trail"),
    (4,  "Ridge",        "east on the bench"),
    (5,  "Ridge",        "north over the saddle"),
    (6,  "Ridge",        "west on the scrape line"),
    (7,  "Creek Bottom", "north at the crossing"),
    (8,  "Creek Bottom", "east on the bank trail"),
    (9,  "Oak Flat",     "west under the big oak"),
    (10, "Oak Flat",     "south on the flat"),
    (11, "Cedar Swamp",  "east on the bedding edge"),
    (12, "Power Line",   "north up the easement"),
]

# Relative detection volume by stand. Drives how much data each camera produces.
STAND_VOLUME = {
    "Bean Field": 1.00,
    "Ridge": 0.62,
    "Creek Bottom": 0.45,
    "Oak Flat": 0.70,
    "Cedar Swamp": 0.38,
    "Power Line": 0.30,
}

SPECIES_MIX = [
    ("whitetail deer", 0.68),
    ("turkey", 0.11),
    ("raccoon", 0.07),
    ("coyote", 0.05),
    ("squirrel", 0.04),
    ("black bear", 0.01),
    ("human", 0.04),
]


def daterange(start: date, end: date):
    d = start
    while d <= end:
        yield d
        d += timedelta(days=1)


def season_dates(year: int):
    return daterange(date(year, *SEASON_START), date(year, *SEASON_END))


def pick_species(rng: random.Random) -> str:
    roll = rng.random()
    cum = 0.0
    for name, weight in SPECIES_MIX:
        cum += weight
        if roll <= cum:
            return name
    return "whitetail deer"


def rut_multiplier(d: date) -> float:
    """
    Wisconsin rut peaks in early-to-mid November. Movement volume follows a
    bell around Nov 10, with a secondary late-season food bump in December.
    """
    peak = date(d.year, 11, 10)
    days_off = abs((d - peak).days)
    rut = math.exp(-(days_off ** 2) / (2 * 14.0 ** 2))

    late = 0.0
    if d.month == 12:
        late = 0.35 * ((d.day) / 31.0)

    return 0.35 + 1.25 * rut + late


def sample_twilight_offset(rng: random.Random) -> float:
    """
    Minutes offset from a twilight anchor. Deer movement clusters tightly
    around civil twilight -- a narrow normal, clipped to +/- 100 minutes.
    """
    while True:
        m = rng.gauss(0, 38)
        if -100 <= m <= 100:
            return m


def generate_detections(rng: random.Random):
    rows = []
    det_id = 1

    for year in SEASONS:
        for d in season_dates(year):
            dawn = civil_dawn(d)
            dusk = civil_dusk(d)
            rut = rut_multiplier(d)

            for cam_id, stand, _orientation in CAMERAS:
                base = 4.2 * STAND_VOLUME[stand] * rut
                n_events = max(0, int(rng.gauss(base, base * 0.45)))

                for _ in range(n_events):
                    # 46% dawn, 44% dusk, 10% overnight/midday wanderers.
                    roll = rng.random()
                    if roll < 0.46:
                        anchor = dawn
                    elif roll < 0.90:
                        anchor = dusk
                    else:
                        anchor = datetime(d.year, d.month, d.day, rng.randint(0, 23), rng.randint(0, 59))

                    if roll < 0.90:
                        ts = anchor + timedelta(minutes=sample_twilight_offset(rng))
                    else:
                        ts = anchor

                    # Keep every detection inside its own calendar day so the
                    # golden month/year aggregations stay clean.
                    if ts.date() != d:
                        ts = datetime(d.year, d.month, d.day, 12, 0)

                    species = pick_species(rng)
                    if species == "whitetail deer":
                        antlered = rng.random() < 0.31
                        count = rng.choice([1, 1, 1, 2, 2, 3, 4])
                    else:
                        antlered = False
                        count = rng.choice([1, 1, 1, 2])

                    confidence = round(min(0.99, max(0.41, rng.gauss(0.87, 0.09))), 3)

                    rows.append(
                        (det_id, cam_id, ts, species, antlered, count, confidence)
                    )
                    det_id += 1

    return rows


def generate_weather(rng: random.Random):
    rows = []
    for year in SEASONS:
        for d in season_dates(year):
            # A prevailing wind for the day, with hourly wobble.
            base_idx = rng.randrange(len(CARDINALS))
            base_temp = 62 - (d - date(year, 9, 15)).days * 0.36
            base_pressure = rng.gauss(30.05, 0.22)

            for hour in range(24):
                idx = (base_idx + rng.choice([-1, 0, 0, 0, 1])) % len(CARDINALS)
                cardinal, deg = CARDINALS[idx]
                diurnal = -7 * math.cos((hour - 15) / 24 * 2 * math.pi)
                temp = round(base_temp + diurnal + rng.gauss(0, 3.2), 1)
                rows.append(
                    (
                        datetime(d.year, d.month, d.day, hour),
                        temp,
                        cardinal,
                        deg,
                        round(max(0.0, rng.gauss(8.5, 4.4)), 1),
                        round(base_pressure + rng.gauss(0, 0.05), 2),
                    )
                )
    return rows


def generate_sits(rng: random.Random):
    """
    Sparse outcome data -- roughly 22 sits a season, which is realistic for
    someone with a job. Sparse on purpose: it is not enough to detect a bad
    recommendation statistically. That is the point.
    """
    rows = []
    sit_id = 1
    stand_names = [s[0] for s in STANDS]

    for year in SEASONS:
        days = list(season_dates(year))
        chosen = rng.sample(days, 22)
        for d in sorted(chosen):
            stand = rng.choice(stand_names)
            morning = rng.random() < 0.6
            anchor = civil_dawn(d) if morning else civil_dusk(d)
            start = anchor - timedelta(minutes=30 if morning else 180)
            hours = round(rng.uniform(2.0, 5.5), 1)
            expected = 2.6 * rut_multiplier(d) * STAND_VOLUME[stand]
            observed = max(0, int(rng.gauss(expected, expected * 0.7)))
            rows.append(
                (sit_id, d, stand, start, hours, observed, None, None)
            )
            sit_id += 1
    return rows


def main() -> None:
    rng = random.Random(SEED)

    if DB_PATH.exists():
        DB_PATH.unlink()

    con = duckdb.connect(str(DB_PATH))
    con.execute(SCHEMA_PATH.read_text())

    con.executemany(
        "INSERT INTO stands VALUES (?, ?, ?, ?, ?)",
        [(n, t, w, a, notes) for n, t, w, a, notes in STANDS],
    )

    con.executemany(
        "INSERT INTO cameras VALUES (?, ?, ?, ?, ?)",
        [
            (cid, stand, orient, date(2022, 9, 1), "2.1")
            for cid, stand, orient in CAMERAS
        ],
    )

    detections = generate_detections(rng)
    con.executemany(
        "INSERT INTO detections VALUES (?, ?, ?, ?, ?, ?, ?)", detections
    )

    weather = generate_weather(rng)
    con.executemany(
        "INSERT INTO weather_hourly VALUES (?, ?, ?, ?, ?, ?)", weather
    )

    sits = generate_sits(rng)
    con.executemany("INSERT INTO sits VALUES (?, ?, ?, ?, ?, ?, ?, ?)", sits)

    con.close()

    print(f"Built {DB_PATH.name}")
    print(f"  stands          {len(STANDS):>8,}")
    print(f"  cameras         {len(CAMERAS):>8,}  (all on firmware 2.1)")
    print(f"  detections      {len(detections):>8,}")
    print(f"  weather_hourly  {len(weather):>8,}")
    print(f"  sits            {len(sits):>8,}")
    print()
    print("State: GREEN. Timestamps are clean local Central time.")


if __name__ == "__main__":
    main()
