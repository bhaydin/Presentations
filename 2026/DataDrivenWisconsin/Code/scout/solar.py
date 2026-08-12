"""
Civil twilight math for Milwaukee, WI (43.0389 N, -87.9065 W).

Dependency-free NOAA solar position approximation. This exists so the synthetic
detection data peaks at *real* dawn and dusk for each calendar date, which is
what makes the firmware break look like a genuine finding instead of noise.

If the numbers here are off by two minutes, nobody in the room will care.
What matters is that dawn drifts ~90 minutes across a Wisconsin season, so a
naive "deer move at 6am" assumption cannot substitute for the real curve.
"""

from __future__ import annotations

import math
from datetime import date, datetime, timedelta

LATITUDE = 43.0389
LONGITUDE = -87.9065

# Civil twilight = sun 6 degrees below horizon.
CIVIL_ZENITH = 96.0


def _day_of_year(d: date) -> int:
    return d.timetuple().tm_yday


def _solar_event_utc_hours(d: date, rising: bool, zenith: float) -> float | None:
    """
    Return UTC hours (float) of the solar event, or None if it never occurs
    (polar cases — irrelevant here, but handled).

    Standard NOAA sunrise/sunset algorithm.
    """
    n = _day_of_year(d)
    lng_hour = LONGITUDE / 15.0

    t = n + ((6.0 if rising else 18.0) - lng_hour) / 24.0

    # Sun's mean anomaly
    m = (0.9856 * t) - 3.289

    # Sun's true longitude
    l = m + (1.916 * math.sin(math.radians(m))) + (0.020 * math.sin(math.radians(2 * m))) + 282.634
    l %= 360.0

    # Right ascension
    ra = math.degrees(math.atan(0.91764 * math.tan(math.radians(l)))) % 360.0
    l_quadrant = math.floor(l / 90.0) * 90.0
    ra_quadrant = math.floor(ra / 90.0) * 90.0
    ra = (ra + (l_quadrant - ra_quadrant)) / 15.0

    # Declination
    sin_dec = 0.39782 * math.sin(math.radians(l))
    cos_dec = math.cos(math.asin(sin_dec))

    cos_h = (
        math.cos(math.radians(zenith)) - (sin_dec * math.sin(math.radians(LATITUDE)))
    ) / (cos_dec * math.cos(math.radians(LATITUDE)))

    if cos_h > 1 or cos_h < -1:
        return None

    h = math.degrees(math.acos(cos_h))
    if rising:
        h = 360.0 - h
    h /= 15.0

    mean_time = h + ra - (0.06571 * t) - 6.622
    return (mean_time - lng_hour) % 24.0


def is_central_daylight(d: date) -> bool:
    """
    US DST: second Sunday in March through first Sunday in November.
    Wisconsin observes Central Time.
    """
    year = d.year

    march = date(year, 3, 1)
    # weekday(): Monday=0 .. Sunday=6
    first_sunday_march = march + timedelta(days=(6 - march.weekday()) % 7)
    dst_start = first_sunday_march + timedelta(days=7)

    november = date(year, 11, 1)
    dst_end = november + timedelta(days=(6 - november.weekday()) % 7)

    return dst_start <= d < dst_end


def utc_offset_hours(d: date) -> int:
    """-5 for CDT, -6 for CST."""
    return -5 if is_central_daylight(d) else -6


def civil_dawn(d: date) -> datetime:
    """Local (Central) datetime of civil dawn."""
    utc_hours = _solar_event_utc_hours(d, rising=True, zenith=CIVIL_ZENITH)
    if utc_hours is None:
        return datetime(d.year, d.month, d.day, 6, 0)
    local_hours = (utc_hours + utc_offset_hours(d)) % 24.0
    return _to_datetime(d, local_hours)


def civil_dusk(d: date) -> datetime:
    """Local (Central) datetime of civil dusk."""
    utc_hours = _solar_event_utc_hours(d, rising=False, zenith=CIVIL_ZENITH)
    if utc_hours is None:
        return datetime(d.year, d.month, d.day, 18, 0)
    local_hours = (utc_hours + utc_offset_hours(d)) % 24.0
    return _to_datetime(d, local_hours)


def _to_datetime(d: date, local_hours: float) -> datetime:
    hour = int(local_hours)
    minute = int(round((local_hours - hour) * 60))
    if minute == 60:
        hour, minute = hour + 1, 0
    hour = min(hour, 23)
    return datetime(d.year, d.month, d.day, hour, minute)


if __name__ == "__main__":
    # Sanity check: dawn should walk later as the season progresses.
    for d in [
        date(2025, 9, 15),
        date(2025, 10, 1),
        date(2025, 10, 15),
        date(2025, 11, 1),
        date(2025, 11, 15),
        date(2025, 12, 1),
    ]:
        print(
            f"{d}  dawn {civil_dawn(d):%H:%M}  dusk {civil_dusk(d):%H:%M}  "
            f"offset {utc_offset_hours(d)}"
        )
