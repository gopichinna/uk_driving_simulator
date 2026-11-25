"""Generate 20-40 footway pedestrian routes avoiding buildings and using mapped crossings."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

from shapely.geometry import Point, Polygon
from shapely.strtree import STRtree

from ltl_pipeline.schema import REPO_ROOT, validate_pedroutes_file

ROADPACK_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "roadpack.json"
BUILDINGS_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "buildings.json"
OUT_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "pedestrians.json"


def _sidewalk_offset_points(
    geom: list[list[float]],
    offset_m: float,
    side: int,
) -> list[tuple[float, float]]:
    """Return points offset perpendicular to geom by offset_m (* side in {+1, -1})."""
    pts: list[tuple[float, float]] = []
    for i, p in enumerate(geom):
        if i == 0:
            dx, dz = geom[1][0] - p[0], geom[1][1] - p[1]
        elif i == len(geom) - 1:
            dx, dz = p[0] - geom[-2][0], p[1] - geom[-2][1]
        else:
            dx, dz = geom[i + 1][0] - geom[i - 1][0], geom[i + 1][1] - geom[i - 1][1]
        length = math.hypot(dx, dz) or 1.0
        # Left normal (-dz, dx)
        nx, nz = (-dz / length) * side, (dx / length) * side
        pts.append((round(p[0] + nx * offset_m, 2), round(p[1] + nz * offset_m, 2)))
    return pts


def _nudge_out_of_buildings(
    pt: tuple[float, float],
    b_tree: STRtree,
    b_polys: list[Polygon],
    road_ref: tuple[float, float],
) -> tuple[float, float]:
    """If pt falls inside a building polygon, pull it halfway toward road_ref."""
    p = Point(pt)
    for idx in b_tree.query(p):
        if b_polys[idx].contains(p):
            for t in (0.5, 0.75, 0.9):
                cand = (
                    round(pt[0] * (1 - t) + road_ref[0] * t, 2),
                    round(pt[1] * (1 - t) + road_ref[1] * t, 2),
                )
                if not b_polys[idx].contains(Point(cand)):
                    return cand
    return pt


def build_pedestrian_routes(
    roadpack_path: Path = ROADPACK_PATH,
    buildings_path: Path = BUILDINGS_PATH,
    target_count: int = 28,
) -> dict[str, Any]:
    """Build 20-40 pedestrian routes along footways using mapped crossings and H2 mouths."""
    pack = json.loads(roadpack_path.read_text(encoding="utf-8"))
    bldgs_doc = json.loads(buildings_path.read_text(encoding="utf-8"))
    b_polys = [Polygon(b["footprint"]) for b in bldgs_doc["buildings"]]
    b_tree = STRtree(b_polys)

    links_by_id = {lk["id"]: lk for lk in pack["links"]}
    nodes_by_id = {n["id"]: n for n in pack["nodes"]}
    jp_by_node = {jp["nodeId"]: jp for jp in pack.get("junctionPriority", [])}

    allowed_crossings = [
        c
        for c in pack["crossings"]
        if c["type"] in ("zebra", "pelican", "puffin", "toucan")
        or (c["type"] == "uncontrolled" and c["confidence"] >= 0.7)
    ]

    routes: list[dict[str, Any]] = []

    # 1. Crossing-based footway routes (walk along link left footway -> cross -> right footway)
    for c in allowed_crossings:
        if len(routes) >= 22:
            break
        lk = links_by_id.get(c["linkId"])
        if not lk or lk.get("sidewalk", "both") == "none":
            continue
        geom = lk["geometry"]
        if len(geom) < 2:
            continue
        offset = round(lk.get("widthM", 6.5) / 2.0 + 1.5, 2)
        left_pts = _sidewalk_offset_points(geom, offset, +1)
        right_pts = _sidewalk_offset_points(geom, offset, -1)

        # Nudge any point inside a building footprint toward the link geometry
        p_start = _nudge_out_of_buildings(left_pts[0], b_tree, b_polys, tuple(geom[0]))
        p_cross_a = _nudge_out_of_buildings(left_pts[-1], b_tree, b_polys, tuple(geom[-1]))
        p_cross_b = _nudge_out_of_buildings(right_pts[-1], b_tree, b_polys, tuple(geom[-1]))
        p_end = _nudge_out_of_buildings(right_pts[0], b_tree, b_polys, tuple(geom[0]))

        pts = [
            [p_start[0], p_start[1]],
            [p_cross_a[0], p_cross_a[1]],
            [p_cross_b[0], p_cross_b[1]],
            [p_end[0], p_end[1]],
        ]
        if any(
            any(b_polys[idx].contains(Point(pt)) for idx in b_tree.query(Point(pt)))
            for pt in pts
        ):
            continue

        routes.append(
            {
                "id": f"ped_x_{len(routes) + 1:02d}",
                "points": pts,
                "crossings": [{"crossingId": c["id"], "fromIndex": 1, "toIndex": 2}],
                "mouthCrossings": [],
            }
        )

    # 2. Side-road mouth crossings (rule H2) at uncontrolled/give-way junctions
    for nid, jp in sorted(jp_by_node.items()):
        if len(routes) >= target_count:
            break
        if jp["controlledBy"] not in ("give_way", "uncontrolled"):
            continue
        node = nodes_by_id.get(nid)
        if not node:
            continue
        # Find a minor link incident to this node
        inc_links = [
            lk
            for lk in pack["links"]
            if (lk["from"] == nid or lk["to"] == nid)
            and lk["id"] not in jp["majorLinks"]
            and lk.get("sidewalk", "both") != "none"
        ]
        if not inc_links:
            continue
        minor = inc_links[0]
        g = minor["geometry"]
        offset = round(minor.get("widthM", 6.5) / 2.0 + 1.5, 2)
        if minor["to"] == nid:
            p_a = _sidewalk_offset_points(g, offset, +1)[-1]
            p_b = _sidewalk_offset_points(g, offset, -1)[-1]
            ref = tuple(g[-1])
        else:
            p_a = _sidewalk_offset_points(g, offset, +1)[0]
            p_b = _sidewalk_offset_points(g, offset, -1)[0]
            ref = tuple(g[0])

        p_a = _nudge_out_of_buildings(p_a, b_tree, b_polys, ref)
        p_b = _nudge_out_of_buildings(p_b, b_tree, b_polys, ref)
        pts = [[p_a[0], p_a[1]], [p_b[0], p_b[1]]]
        if any(
            any(b_polys[idx].contains(Point(pt)) for idx in b_tree.query(Point(pt)))
            for pt in pts
        ):
            continue

        routes.append(
            {
                "id": f"ped_h2_{len(routes) + 1:02d}",
                "points": pts,
                "crossings": [],
                "mouthCrossings": [{"nodeId": nid, "fromIndex": 0, "toIndex": 1}],
            }
        )

    return {
        "pedRoutesFormatVersion": "0.1",
        "packId": pack["packId"],
        "routes": routes,
    }


def write_pedestrian_routes(out_path: Path = OUT_PATH) -> Path:
    """Generate and validate content/cities/wolverhampton/pedestrians.json."""
    data = build_pedestrian_routes()
    out_path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    validate_pedroutes_file(out_path)
    return out_path


if __name__ == "__main__":
    written = write_pedestrian_routes()
    print(f"Wrote {written}")
