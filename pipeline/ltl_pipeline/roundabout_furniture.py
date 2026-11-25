"""Generate roundabout_furniture.json (islands, blue signs, chevrons, give-way lines)."""

from __future__ import annotations

import json
import math
from collections import defaultdict, deque
from pathlib import Path
from typing import Any

from shapely.geometry import MultiPoint, Point, Polygon

from ltl_pipeline import __version__
from ltl_pipeline.schema import REPO_ROOT, validate_roundabout_furniture_file

GOV_UK_TRAFFIC_SIGNS_URL = "https://www.gov.uk/guidance/the-highway-code/traffic-signs"
GOV_UK_KYTS_URL = "https://www.gov.uk/government/publications/know-your-traffic-signs"

DIAGRAM_REFS = {
    "roundabout_warning": "Diagram 510 (Roundabout ahead warning)",
    "roundabout_blue": "Diagram 610 / 611.1 (Keep left / Mini-roundabout)",
    "chevron": "Diagram 515 (Sharp deviation of route chevron on central island)",
    "give_way_line": "Diagram 1003.1 / 1003.3 (Give way line at roundabout entry)",
}


def _heading_deg(dx: float, dz: float) -> float:
    return round(math.degrees(math.atan2(dx, dz)) % 360.0, 2)


def _game_lane_half_width(lk: dict[str, Any]) -> float:
    if lk.get("oneway", False):
        lanes = max(1, int(lk.get("lanesForward", 1)))
    else:
        lanes = max(1, int(lk.get("lanesForward", 1)) + int(lk.get("lanesBackward", 1)))
    return (3.25 * lanes) / 2.0


def extract_roundabout_clusters(pack: dict[str, Any]) -> list[tuple[list[str], Polygon]]:
    """Return [(node_ids, island_polygon)] for every roundabout node cluster in pack."""
    rbt_nodes = {
        n["id"]: n for n in pack["nodes"] if n.get("kind") in ("roundabout", "mini_roundabout")
    }
    if not rbt_nodes:
        return []

    adj: dict[str, set[str]] = defaultdict(set)
    for lk in pack["links"]:
        u, v = lk["from"], lk["to"]
        if u in rbt_nodes and v in rbt_nodes:
            adj[u].add(v)
            adj[v].add(u)

    visited: set[str] = set()
    clusters: list[tuple[list[str], Polygon]] = []
    for nid in sorted(rbt_nodes):
        if nid in visited:
            continue
        comp: list[str] = []
        q = deque([nid])
        visited.add(nid)
        while q:
            cur = q.popleft()
            comp.append(cur)
            for nb in adj.get(cur, ()):
                if nb not in visited:
                    visited.add(nb)
                    q.append(nb)
        comp.sort()
        pts = [(rbt_nodes[c]["x"], rbt_nodes[c]["z"]) for c in comp]
        if len(pts) >= 3:
            hull = MultiPoint(pts).convex_hull
            if isinstance(hull, Polygon):
                poly = hull.buffer(-2.5)
                if poly.is_empty or not isinstance(poly, Polygon):
                    poly = hull
            else:
                cx = sum(p[0] for p in pts) / len(pts)
                cz = sum(p[1] for p in pts) / len(pts)
                poly = Point(cx, cz).buffer(3.0, quad_segs=4)
        else:
            cx = sum(p[0] for p in pts) / len(pts)
            cz = sum(p[1] for p in pts) / len(pts)
            poly = Point(cx, cz).buffer(3.0, quad_segs=4)
        clusters.append((comp, poly))
    return clusters


def build_roundabout_furniture(pack: dict[str, Any]) -> dict[str, Any]:
    clusters = extract_roundabout_clusters(pack)
    nodes_by_id = {n["id"]: n for n in pack["nodes"]}
    links_by_id = {lk["id"]: lk for lk in pack["links"]}
    clusters_by_node = {nid: idx for idx, (nids, _) in enumerate(clusters) for nid in nids}

    roundabouts_out: list[dict[str, Any]] = []
    for r_idx, (node_ids, poly) in enumerate(clusters, start=1):
        node_set = set(node_ids)
        cx, cz = poly.centroid.x, poly.centroid.y
        coords = [[round(x, 3), round(z, 3)] for x, z in list(poly.exterior.coords)[:-1]]

        signs: list[dict[str, Any]] = []
        chevrons: list[dict[str, Any]] = []
        give_ways: list[dict[str, Any]] = []

        # Every non-ring link arriving at this roundabout cluster (oneway into cluster, or two-way)
        for lk in sorted(pack["links"], key=lambda item: item["id"]):
            if not lk.get("inMainNetwork", True):
                continue
            if lk["to"] in node_set and lk["from"] not in node_set:
                pts = lk["geometry"]
                app_node = nodes_by_id[lk["to"]]
            elif (
                not lk.get("oneway", False)
                and lk["from"] in node_set
                and lk["to"] not in node_set
            ):
                pts = list(reversed(lk["geometry"]))
                app_node = nodes_by_id[lk["from"]]
            else:
                continue
            if len(pts) < 2:
                continue
            dx = pts[-1][0] - pts[-2][0]
            dz = pts[-1][1] - pts[-2][1]
            seg = math.hypot(dx, dz)
            if seg < 1e-4:
                continue
            dx /= seg
            dz /= seg
            heading = _heading_deg(dx, dz)
            lane_half = _game_lane_half_width(lk)

            # Give-way line at entry (1.5 m before node along approach)
            gw_x = round(app_node["x"] - dx * 1.5, 3)
            gw_z = round(app_node["z"] - dz * 1.5, 3)
            give_ways.append(
                {
                    "id": f"gw_{r_idx}_{len(give_ways) + 1}",
                    "approachLinkId": lk["id"],
                    "x": gw_x,
                    "z": gw_z,
                    "heading": heading,
                    "widthM": round(lane_half * 2.0, 2),
                    "diagramRef": DIAGRAM_REFS["give_way_line"],
                }
            )

            # Blue roundabout sign 18 m before entry on nearside verge (>= 2.0 m from lane edge)
            dist_back = min(18.0, max(6.0, seg * 0.5))
            bx = pts[-1][0] - dx * dist_back
            bz = pts[-1][1] - dz * dist_back
            verge = lane_half + 2.2
            sx = round(bx + (-dz) * verge, 3)
            sz = round(bz + dx * verge, 3)
            if not poly.contains(Point(sx, sz)):
                signs.append(
                    {
                        "id": f"rs_{r_idx}_{len(signs) + 1}",
                        "approachLinkId": lk["id"],
                        "x": sx,
                        "z": sz,
                        "heading": heading,
                        "diagramRef": DIAGRAM_REFS["roundabout_blue"],
                    }
                )

            # Chevron board on central island facing the entry
            vx, vz = app_node["x"] - cx, app_node["z"] - cz
            vlen = math.hypot(vx, vz) or 1.0
            vx /= vlen
            vz /= vlen
            ch_x = round(cx + vx * 2.2, 3)
            ch_z = round(cz + vz * 2.2, 3)
            chevrons.append(
                {
                    "id": f"ch_{r_idx}_{len(chevrons) + 1}",
                    "approachLinkId": lk["id"],
                    "x": ch_x,
                    "z": ch_z,
                    "heading": heading,
                    "diagramRef": DIAGRAM_REFS["chevron"],
                }
            )

        if not give_ways and not signs:
            continue
        _ = clusters_by_node, links_by_id
        roundabouts_out.append(
            {
                "id": f"rbt_{r_idx:03d}",
                "nodeIds": node_ids,
                "islandPolygon": coords,
                "roundaboutSigns": signs,
                "chevronBoards": chevrons,
                "giveWayLines": give_ways,
            }
        )

    return {
        "roundaboutFurnitureFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "source": {
            "kind": pack["source"]["kind"],
            "licence": pack["source"]["licence"],
            "attribution": pack["source"]["attribution"],
            "generator": f"ltl_pipeline/{__version__}",
            "styleSources": [GOV_UK_TRAFFIC_SIGNS_URL, GOV_UK_KYTS_URL],
            "diagramRefs": DIAGRAM_REFS,
        },
        "roundabouts": roundabouts_out,
    }


def write_roundabout_furniture(city_dir: Path) -> Path:
    pack = json.loads((city_dir / "roadpack.json").read_text(encoding="utf-8"))
    doc = build_roundabout_furniture(pack)
    out_path = city_dir / "roundabout_furniture.json"
    out_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    validate_roundabout_furniture_file(out_path)
    return out_path


if __name__ == "__main__":
    for name in ("wolverhampton", "wolverhampton-test-area"):
        p = write_roundabout_furniture(REPO_ROOT / "content" / "cities" / name)
        print(f"Wrote {p}")
