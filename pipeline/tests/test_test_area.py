"""Tests for wolverhampton-test-area RoadPack, practice routes, QA, and attribution."""

from __future__ import annotations

from ltl_pipeline.direction_signs import RoadGraph
from ltl_pipeline.schema import REPO_ROOT, validate_roadpack_file, validate_routes_file

AREA_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton-test-area"


def test_test_area_pack_routes_qa_and_attribution() -> None:
    pack = validate_roadpack_file(AREA_DIR / "roadpack.json")
    assert pack["packId"] == "gb-wolverhampton-test-area"
    assert pack["source"]["licence"] == "ODbL-1.0"
    assert "© OpenStreetMap contributors" in pack["source"]["attribution"]
    assert (AREA_DIR / "QA.md").is_file()

    routes_doc = validate_routes_file(AREA_DIR / "routes.json")
    assert len(routes_doc["routes"]) == 3

    graph = RoadGraph(pack)
    for route in routes_doc["routes"]:
        assert 15 <= route["estimatedMinutes"] <= 25
        assert "not an official" in route["description"].lower()
        steps = route["links"]
        assert len(steps) >= 4
        assert steps[0]["linkId"] == steps[-1]["linkId"]
        for i in range(len(steps) - 1):
            cur_id = steps[i]["linkId"]
            cur_fwd = steps[i]["direction"] == "forward"
            nxt_id = steps[i + 1]["linkId"]
            via_node = graph.links[cur_id]["to"] if cur_fwd else graph.links[cur_id]["from"]
            allowed_next_ids = {nl for nl, _, _ in graph.allowed_next(cur_id, via_node)}
            assert nxt_id in allowed_next_ids, (
                f"Route {route['id']} step {i}: {cur_id} -> {nxt_id} not legal/connected"
            )
