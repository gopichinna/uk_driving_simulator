"""Tests for speed_signs.json, bus_stops.json, and warning_signs.json."""

from __future__ import annotations

import json

from ltl_pipeline.direction_signs import RoadGraph
from ltl_pipeline.schema import REPO_ROOT, validate_signs_placed_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"


def test_street_furniture_validates_and_clears_carriageway() -> None:
    pack = json.loads((CITY_DIR / "roadpack.json").read_text(encoding="utf-8"))
    graph = RoadGraph(pack)

    speed_doc = validate_signs_placed_file(CITY_DIR / "speed_signs.json")
    bus_doc = validate_signs_placed_file(CITY_DIR / "bus_stops.json")
    warn_doc = validate_signs_placed_file(CITY_DIR / "warning_signs.json")

    assert len(speed_doc["speedSigns"]) >= 4
    assert len(bus_doc["busStops"]) >= 10
    assert len(warn_doc["warningSigns"]) >= 5
    assert "diagramRefs" in warn_doc["source"]

    for item in speed_doc["speedSigns"]:
        assert not graph.on_carriageway(item["x"], item["z"], min_clearance_m=0.5), (
            f"Speed sign {item['id']} sits within 0.5 m of carriageway"
        )
    for item in bus_doc["busStops"]:
        assert not graph.on_carriageway(item["x"], item["z"], min_clearance_m=0.5), (
            f"Bus stop {item['id']} sits within 0.5 m of carriageway"
        )
    for item in warn_doc["warningSigns"]:
        assert not graph.on_carriageway(item["x"], item["z"], min_clearance_m=0.5), (
            f"Warning sign {item['id']} sits within 0.5 m of carriageway"
        )
