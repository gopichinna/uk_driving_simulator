"""Tests for Round 29 rural_practice and ring_road_practice packs and scenarios."""

from __future__ import annotations

import json

from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_pois_file,
    validate_roadpack_file,
    validate_scenario_file,
)

CITIES = REPO_ROOT / "content" / "cities"
SCENARIOS = REPO_ROOT / "content" / "scenarios" / "gb"


def test_rural_and_ring_road_packs_validate() -> None:
    for pid in ("rural_practice", "ring_road_practice"):
        cdir = CITIES / pid
        pack = validate_roadpack_file(cdir / "roadpack.json")
        assert pack["packId"] == pid
        assert len(pack["nodes"]) > 50 and len(pack["links"]) > 50
        anchors = json.loads((cdir / "anchors.json").read_text(encoding="utf-8"))["anchors"]
        assert {"start", "join"} <= {a["id"] for a in anchors}
        assert (cdir / "SOURCE.md").is_file()
        assert (cdir / "pedestrian_routes.json").is_file()
        validate_pois_file(cdir / "pois.json")


def test_rural_and_ring_road_scenarios_validate_and_are_optional_practice() -> None:
    prog = json.loads(
        (REPO_ROOT / "content" / "lessons" / "gb" / "progression.json").read_text(encoding="utf-8")
    )
    opt_by_id = {item["id"]: item for item in prog.get("optionalPractice", [])}
    assert {"rural_practice", "ring_road_practice"} <= set(opt_by_id.keys())
    expected = {
        "s5_rural_bends": "rural_practice",
        "s5_rural_narrow": "rural_practice",
        "s5_rural_village": "rural_practice",
        "s4_ring_lanes": "ring_road_practice",
        "s4_ring_rbt_multilane": "ring_road_practice",
        "s4_ring_lights": "ring_road_practice",
    }
    for sc_id, course in expected.items():
        data = validate_scenario_file(SCENARIOS / f"{sc_id}.json")
        assert data["course"] == course
        assert sc_id in opt_by_id[course]["scenarioIds"]
