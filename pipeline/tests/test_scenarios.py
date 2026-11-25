"""Tests for scripted lesson scenarios (all 18 lessons)."""

from __future__ import annotations

import json

from ltl_pipeline.schema import REPO_ROOT, validate_scenario_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
SCENARIOS_DIR = REPO_ROOT / "content" / "scenarios" / "gb"

ALLOWED_COURSE_ANCHORS: dict[str, set[str]] = {
    "town": {
        "crossroads_signals",
        "tjunction_north",
        "tjunction_south",
        "zebra",
        "box_junction",
        "school_street",
        "start",
    },
    "roundabout": {"start"},
    "two_lane_roundabout": {"start"},
    "dual_carriageway": {"start"},
    "wolverhampton": {"start"},
}


def test_every_lesson_scenario_has_valid_file() -> None:
    expected: dict[str, tuple[str, str]] = {}
    for lf in sorted(LESSONS_DIR.glob("[01][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        for sc in lesson["scenarios"]:
            expected[sc["id"]] = (sc["course"], sc["anchor"])

    assert len(expected) == 38
    for sc_id, (course, anchor) in expected.items():
        path = SCENARIOS_DIR / f"{sc_id}.json"
        assert path.is_file(), f"Missing scenario file: {path}"
        data = validate_scenario_file(path)
        assert data["scenarioId"] == sc_id
        assert data["course"] == course
        assert data["anchor"] == anchor
        assert anchor in ALLOWED_COURSE_ANCHORS[course]

        event_types = [ev["type"] for ev in data["events"]]
        assert any(t in event_types for t in ("finish_at", "take_exit", "finish_after_s"))
        for ev in data["events"]:
            if ev["type"] in ("parked_car", "cyclist_ahead", "finish_at"):
                assert ev["anchor"] in ALLOWED_COURSE_ANCHORS[course]
            if ev["type"] in ("pedestrian_waits", "pedestrian_steps_out"):
                assert course == "town"
