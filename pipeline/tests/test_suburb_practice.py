"""Tests for Round 25 suburb_practice OSM pack and its 3 scripted scenarios."""

from __future__ import annotations

import json

from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_pois_file,
    validate_roadpack_file,
    validate_scenario_file,
)

CITY_DIR = REPO_ROOT / "content" / "cities" / "suburb_practice"
SCENARIOS_DIR = REPO_ROOT / "content" / "scenarios" / "gb"


def test_suburb_practice_pack_and_furniture_validate() -> None:
    pack = validate_roadpack_file(CITY_DIR / "roadpack.json")
    assert pack["packId"] == "suburb_practice"
    assert len(pack["nodes"]) > 100
    assert len(pack["links"]) > 100
    anchors = json.loads((CITY_DIR / "anchors.json").read_text(encoding="utf-8"))["anchors"]
    anchor_ids = {a["id"] for a in anchors}
    assert {"start", "join"} <= anchor_ids
    speeds = {lk.get("speedLimitMph") for lk in pack["links"]}
    assert 30 in speeds and 40 in speeds
    assert (CITY_DIR / "SOURCE.md").is_file()
    assert (CITY_DIR / "pedestrian_routes.json").is_file()
    pois = validate_pois_file(CITY_DIR / "pois.json")
    assert len(pois["pois"]) > 0
    for name in (
        "buildings.json",
        "direction_signs.json",
        "street_names.json",
        "speed_signs.json",
        "bus_stops.json",
        "warning_signs.json",
    ):
        assert (CITY_DIR / name).is_file(), f"Missing {name}"


def test_suburb_practice_scenarios_validate_and_not_in_progression() -> None:
    prog = json.loads(
        (REPO_ROOT / "content" / "lessons" / "gb" / "progression.json").read_text(encoding="utf-8")
    )
    prog_text = json.dumps(prog)
    for sc_id in ("s2_suburb_30", "s3_suburb_minirbt", "s4_suburb_school"):
        data = validate_scenario_file(SCENARIOS_DIR / f"{sc_id}.json")
        assert data["course"] == "suburb_practice"
        assert data["anchor"] in ("start", "join")
        assert sc_id not in prog_text
