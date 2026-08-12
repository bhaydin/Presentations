"""
Scout's toolbelt. Five tools:

    query_detections        read path over the event stream
    peak_activity_hour      the temporal aggregation -- where the break lives
    get_weather             external enrichment feed
    check_wind_compatibility deterministic rule engine, must NEVER be wrong
    notify_crew             irreversible external comms -- gated

Every tool emits a span. The SQL it runs is a span attribute, so the trace
shows you exactly what hit the warehouse, not a summary of what the agent
thinks it did.
"""

from __future__ import annotations

from datetime import date
from pathlib import Path
from typing import Any

import duckdb

from scout import controls
from scout.tracing import Tracer

DB_PATH = Path(__file__).resolve().parent.parent / "scout.duckdb"

CARDINAL_DEG = {
    "N": 0, "NNE": 22, "NE": 45, "ENE": 67, "E": 90, "ESE": 112,
    "SE": 135, "SSE": 157, "S": 180, "SSW": 202, "SW": 225, "WSW": 247,
    "W": 270, "WNW": 292, "NW": 315, "NNW": 337,
}

WINDOWS = {
    "morning": (0, 12),
    "evening": (12, 24),
    "all": (0, 24),
}


class ToolError(Exception):
    """Raised for bad inputs. The agent must surface these, never paper over them."""


def _connect():
    if not DB_PATH.exists():
        raise ToolError("warehouse not built -- run: python -m scout.build_db")
    return duckdb.connect(str(DB_PATH), read_only=True)


def _known_cameras(con) -> list[int]:
    return [r[0] for r in con.execute("SELECT camera_id FROM cameras ORDER BY 1").fetchall()]


def _known_stands(con) -> list[str]:
    return [r[0] for r in con.execute("SELECT stand_name FROM stands ORDER BY 1").fetchall()]


# ----------------------------------------------------------------------

def query_detections(
    tracer: Tracer,
    stand: str | None = None,
    camera_id: int | None = None,
    species: str | None = "whitetail deer",
    antlered_only: bool = False,
    year: int | None = None,
    month: int | None = None,
) -> dict[str, Any]:
    """Count detections matching a filter. The plain read path."""
    with tracer.span("query_detections", kind="tool") as span:
        controls.check_kill_switches("query_detections")
        con = _connect()
        try:
            if camera_id is not None and camera_id not in _known_cameras(con):
                raise ToolError(
                    f"camera_id {camera_id} does not exist. "
                    f"Known cameras: {_known_cameras(con)}"
                )
            if stand is not None and stand not in _known_stands(con):
                raise ToolError(
                    f"stand '{stand}' does not exist. Known stands: {_known_stands(con)}"
                )

            where, params = ["1=1"], []
            if stand:
                where.append("c.stand_name = ?")
                params.append(stand)
            if camera_id is not None:
                where.append("d.camera_id = ?")
                params.append(camera_id)
            if species:
                where.append("d.species = ?")
                params.append(species)
            if antlered_only:
                where.append("d.antlered = TRUE")
            if year:
                where.append("EXTRACT(year FROM d.captured_at) = ?")
                params.append(year)
            if month:
                where.append("EXTRACT(month FROM d.captured_at) = ?")
                params.append(month)

            sql = (
                "SELECT COUNT(*) AS detections, COALESCE(SUM(d.count), 0) AS animals "
                "FROM detections d JOIN cameras c USING (camera_id) "
                f"WHERE {' AND '.join(where)}"
            )
            span.attributes["sql"] = " ".join(sql.split())
            span.attributes["params"] = params

            with tracer.span("duckdb.execute", kind="sql", table="detections"):
                row = con.execute(sql, params).fetchone()

            result = {"detections": int(row[0]), "animals": int(row[1])}
            span.attributes["result"] = result
            return result
        finally:
            con.close()


def peak_activity_hour(
    tracer: Tracer,
    stand: str,
    year: int,
    month: int,
    window: str = "morning",
    species: str = "whitetail deer",
) -> dict[str, Any]:
    """
    The hour of day with the most detections, inside a window.

    This is the tool the recommendation depends on, and it is the tool that
    silently changes answer when upstream timestamps shift. Note that it
    reads captured_at directly -- no offset column exists to join against.
    """
    with tracer.span("peak_activity_hour", kind="tool") as span:
        controls.check_kill_switches("peak_activity_hour")
        con = _connect()
        try:
            if stand not in _known_stands(con):
                raise ToolError(
                    f"stand '{stand}' does not exist. Known stands: {_known_stands(con)}"
                )
            if window not in WINDOWS:
                raise ToolError(f"window must be one of {sorted(WINDOWS)}")

            lo, hi = WINDOWS[window]
            sql = """
                SELECT EXTRACT(hour FROM d.captured_at) AS hour_of_day,
                       COUNT(*) AS detections
                FROM detections d
                JOIN cameras c USING (camera_id)
                WHERE c.stand_name = ?
                  AND d.species = ?
                  AND EXTRACT(year FROM d.captured_at) = ?
                  AND EXTRACT(month FROM d.captured_at) = ?
                  AND EXTRACT(hour FROM d.captured_at) >= ?
                  AND EXTRACT(hour FROM d.captured_at) < ?
                GROUP BY 1
                ORDER BY detections DESC, hour_of_day ASC
            """
            params = [stand, species, year, month, lo, hi]
            span.attributes["sql"] = " ".join(sql.split())
            span.attributes["params"] = params

            with tracer.span("duckdb.execute", kind="sql", table="detections"):
                rows = con.execute(sql, params).fetchall()

            if not rows:
                result = {"peak_hour": None, "detections": 0, "histogram": []}
            else:
                result = {
                    "peak_hour": int(rows[0][0]),
                    "detections": int(rows[0][1]),
                    "histogram": [(int(h), int(n)) for h, n in sorted(rows)],
                }

            span.attributes["peak_hour"] = result["peak_hour"]
            span.attributes["rows_returned"] = len(rows)
            # Deliberately surfaced: the fleet is mixed. The agent has no
            # reason to look at this, which is exactly the problem.
            fleet = con.execute(
                "SELECT firmware_version, COUNT(*) FROM cameras "
                "WHERE stand_name = ? GROUP BY 1 ORDER BY 1",
                [stand],
            ).fetchall()
            span.attributes["camera_firmware"] = dict(fleet)
            return result
        finally:
            con.close()


def get_weather(tracer: Tracer, on_date: str, hour: int | None = None) -> dict[str, Any]:
    """Hourly weather for a date. External feed, someone else's SLA."""
    with tracer.span("get_weather", kind="tool") as span:
        controls.check_kill_switches("get_weather")
        con = _connect()
        try:
            sql = """
                SELECT observed_at, temp_f, wind_dir_cardinal, wind_mph, pressure_inhg
                FROM weather_hourly
                WHERE CAST(observed_at AS DATE) = CAST(? AS DATE)
            """
            params: list[Any] = [on_date]
            if hour is not None:
                sql += " AND EXTRACT(hour FROM observed_at) = ?"
                params.append(hour)
            sql += " ORDER BY observed_at"

            span.attributes["sql"] = " ".join(sql.split())
            span.attributes["params"] = params

            with tracer.span("duckdb.execute", kind="sql", table="weather_hourly"):
                rows = con.execute(sql, params).fetchall()

            if not rows:
                raise ToolError(
                    f"no weather on file for {on_date} "
                    "(feed covers Sep 15 - Dec 31, 2022-2025)"
                )

            observations = [
                {
                    "at": str(r[0]),
                    "temp_f": r[1],
                    "wind": r[2],
                    "wind_mph": r[3],
                    "pressure_inhg": r[4],
                }
                for r in rows
            ]
            span.attributes["observations"] = len(observations)
            span.attributes["prevailing_wind"] = max(
                {o["wind"] for o in observations},
                key=lambda w: sum(1 for o in observations if o["wind"] == w),
            )
            return {"observations": observations}
        finally:
            con.close()


def check_wind_compatibility(
    tracer: Tracer, stand: str, wind_cardinal: str
) -> dict[str, Any]:
    """
    Deterministic rule engine. No model in the loop, ever.

    A wrong answer here means you walk your scent into the bedding area and
    burn the stand for a week. This is the class of decision you do NOT hand
    to a probabilistic system -- and the eval suite asserts it exactly.
    """
    with tracer.span("check_wind_compatibility", kind="tool") as span:
        controls.check_kill_switches("check_wind_compatibility")
        con = _connect()
        try:
            wind_cardinal = wind_cardinal.upper().strip()
            if wind_cardinal not in CARDINAL_DEG:
                raise ToolError(
                    f"'{wind_cardinal}' is not a compass point. "
                    f"Expected one of {sorted(CARDINAL_DEG)}"
                )
            row = con.execute(
                "SELECT ideal_wind_cardinal, access_route FROM stands WHERE stand_name = ?",
                [stand],
            ).fetchone()
            if not row:
                raise ToolError(
                    f"stand '{stand}' does not exist. Known stands: {_known_stands(con)}"
                )

            ideal, route = row
            diff = abs(CARDINAL_DEG[wind_cardinal] - CARDINAL_DEG[ideal])
            diff = min(diff, 360 - diff)

            if diff <= 45:
                verdict, detail = "GOOD", "wind holds your scent off the approach"
            elif diff <= 90:
                verdict, detail = "MARGINAL", "crosswind -- sit it, but expect swirl"
            else:
                verdict, detail = (
                    "NO",
                    "wind carries scent straight into the approach -- do not sit this stand",
                )

            result = {
                "stand": stand,
                "ideal_wind": ideal,
                "actual_wind": wind_cardinal,
                "degrees_off": diff,
                "verdict": verdict,
                "detail": detail,
                "access_route": route,
            }
            span.attributes["verdict"] = verdict
            span.attributes["degrees_off"] = diff
            span.attributes["rule"] = "deterministic; no model in path"
            return result
        finally:
            con.close()


def notify_crew(tracer: Tracer, message: str, recipients: list[str] | None = None) -> dict:
    """
    Text the crew. Irreversible, external, addressed to humans.

    This tool NEVER executes on the agent's own authority. It queues and
    raises. That is the entire pattern.
    """
    recipients = recipients or ["Justin", "Kurt"]
    with tracer.span("notify_crew", kind="tool") as span:
        controls.check_kill_switches("notify_crew")
        span.attributes["recipients"] = recipients
        span.attributes["message"] = message

        with tracer.span("policy.write_gate", kind="policy") as pspan:
            approval_id = controls.queue_approval(
                "notify_crew", {"message": message, "recipients": recipients}
            )
            pspan.attributes["decision"] = "HELD FOR APPROVAL"
            pspan.attributes["approval_id"] = approval_id
            pspan.attributes["reason"] = "write tool: irreversible external comms"

        raise controls.ApprovalRequired("notify_crew", {"message": message}, approval_id)


TOOL_REGISTRY = {
    "query_detections": query_detections,
    "peak_activity_hour": peak_activity_hour,
    "get_weather": get_weather,
    "check_wind_compatibility": check_wind_compatibility,
    "notify_crew": notify_crew,
}
