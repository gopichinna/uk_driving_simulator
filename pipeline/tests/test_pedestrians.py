"""Tests for Wolverhampton pedestrian footway routes."""

from __future__ import annotations

from shapely.geometry import LineString, Point, Polygon
from shapely.strtree import STRtree

from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_buildings_file,
    validate_pedroutes_file,
    validate_roadpack_file,
)

ROADPACK_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "roadpack.json"
BUILDINGS_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "buildings.json"
PED_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "pedestrians.json"


def test_pedestrian_routes_valid_near_footways_and_avoid_buildings() -> None:
    pack = validate_roadpack_file(ROADPACK_PATH)
    bldgs = validate_buildings_file(BUILDINGS_PATH)
    ped = validate_pedroutes_file(PED_PATH)

    assert 20 <= len(ped["routes"]) <= 40
    crossing_ids = {c["id"] for c in pack["crossings"]}
    node_ids = {n["id"] for n in pack["nodes"]}

    b_polys = [Polygon(b["footprint"]) for b in bldgs["buildings"]]
    b_tree = STRtree(b_polys)

    # Build carriageway lines to check points are within 6m of a road/footway edge
    road_lines = [LineString(lk["geometry"]) for lk in pack["links"]]
    road_tree = STRtree(road_lines)

    has_mouth = False
    for r in ped["routes"]:
        crossing_indices: set[int] = set()
        for cx in r["crossings"]:
            assert cx["crossingId"] in crossing_ids
            crossing_indices.update(range(cx["fromIndex"], cx["toIndex"] + 1))
        for mx in r.get("mouthCrossings", []):
            assert mx["nodeId"] in node_ids
            has_mouth = True
            crossing_indices.update(range(mx["fromIndex"], mx["toIndex"] + 1))

        for idx, pt in enumerate(r["points"]):
            p = Point(pt)
            for b_idx in b_tree.query(p):
                assert not b_polys[b_idx].contains(p), (
                    f"{r['id']}: point {pt} inside building"
                )
            # Distance to nearest link carriageway + half-width+1.5m <= 6m away from footway edge
            nearest_idx = road_tree.nearest(p)
            dist = p.distance(road_lines[nearest_idx])
            assert dist <= 12.0, f"{r['id']}: point {pt} too far from footway ({dist:.1f} m)"

    assert has_mouth, "Expected at least one H2 mouthCrossing route"
