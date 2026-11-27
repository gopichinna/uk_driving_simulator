"""Tests for Round 36 queslett_roundabout OSM pack and 3 practice routes."""

from __future__ import annotations

from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_buildings_file,
    validate_roadpack_file,
    validate_roundabout_furniture_file,
    validate_routes_file,
)

CITY_DIR = REPO_ROOT / "content" / "cities" / "queslett_roundabout"


def test_queslett_roundabout_pack_and_routes_validate() -> None:
    pack = validate_roadpack_file(CITY_DIR / "roadpack.json")
    assert pack["packId"] == "queslett_roundabout"
    assert len(pack["laneConnections"]) > 0
    validate_buildings_file(CITY_DIR / "buildings.json")
    validate_roundabout_furniture_file(CITY_DIR / "roundabout_furniture.json")
    routes = validate_routes_file(CITY_DIR / "routes.json")
    assert len(routes["routes"]) == 3

    links_by_id = {lk["id"]: lk for lk in pack["links"]}
    for r in routes["routes"]:
        lids = [item["linkId"] for item in r["links"]]
        for i in range(len(lids) - 1):
            a, b = links_by_id[lids[i]], links_by_id[lids[i + 1]]
            assert a["to"] == b["from"], f"Route {r['id']} disconnected at {lids[i]}->{lids[i+1]}"

    total_bytes = sum(p.stat().st_size for p in CITY_DIR.rglob("*") if p.is_file())
    assert total_bytes < 5 * 1024 * 1024
    assert "TwoLaneRoundabout" in (CITY_DIR / "QA.md").read_text(encoding="utf-8")
