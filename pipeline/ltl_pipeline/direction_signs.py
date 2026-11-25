"""Generate UK direction signs and street-name plates for Wolverhampton junctions."""

from __future__ import annotations

import heapq
import json
import math
import re
from collections import defaultdict
from pathlib import Path
from typing import Any

from shapely.geometry import LineString, Point

from ltl_pipeline import __version__
from ltl_pipeline.roundabout_furniture import extract_roundabout_clusters
from ltl_pipeline.schema import REPO_ROOT, validate_signs_placed_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"
ROADPACK_PATH = CITY_DIR / "roadpack.json"
POIS_PATH = CITY_DIR / "pois.json"
DIRECTION_SIGNS_PATH = CITY_DIR / "direction_signs.json"
STREET_NAMES_PATH = CITY_DIR / "street_names.json"

GOV_UK_TRAFFIC_SIGNS_URL = "https://www.gov.uk/guidance/the-highway-code/traffic-signs"
GOV_UK_KYTS_URL = "https://www.gov.uk/government/publications/know-your-traffic-signs"

A_ROAD_RE = re.compile(r"\bA\d+\b")
ROUTE_REF_RE = re.compile(r"\b[ABM]\d+\b")

KIND_PRIORITY = {
    "city_centre": 0,
    "railway_station": 1,
    "bus_station": 2,
    "hospital": 3,
    "university": 4,
    "shopping": 5,
    "car_park": 6,
    "school": 7,
    "police": 8,
    "other": 9,
}

LOCAL_KINDS = {"car_park", "university", "police", "shopping"}

GENERIC_TEXT_AND_SYMBOL = {
    "railway_station": ("Railway Station", "rail"),
    "bus_station": ("Bus Station", "bus"),
    "city_centre": ("City Centre", "city_centre"),
    "hospital": ("Hospital", "hospital"),
}


def is_primary_approach(link: dict[str, Any]) -> bool:
    """True for primary/trunk links or any link carrying an A-road ref."""
    if link.get("roadClass") in ("primary", "trunk"):
        return True
    ref = link.get("ref") or ""
    return bool(A_ROAD_RE.search(ref))


def game_lane_half_width(link: dict[str, Any]) -> float:
    """Half-width of the game's 3.25 m lane geometry for a link."""
    if link.get("oneway", False):
        lanes = max(1, int(link.get("lanesForward", 1)))
    else:
        lanes = max(1, int(link.get("lanesForward", 1)) + int(link.get("lanesBackward", 1)))
    return (3.25 * lanes) / 2.0


def _heading_deg(dx: float, dz: float) -> float:
    return round(math.degrees(math.atan2(dx, dz)) % 360.0, 2)


def _signed_turn_deg(in_dx: float, in_dz: float, out_dx: float, out_dz: float) -> float:
    """Clockwise turn angle in degrees (-180, 180] from incoming to outgoing vector."""
    in_h = math.atan2(in_dx, in_dz)
    out_h = math.atan2(out_dx, out_dz)
    diff = math.degrees(out_h - in_h)
    while diff > 180.0:
        diff -= 360.0
    while diff <= -180.0:
        diff += 360.0
    return diff


def _classify_turn(turn_deg: float) -> str:
    if -25.0 <= turn_deg <= 25.0:
        return "ahead"
    if 25.0 < turn_deg <= 65.0:
        return "ahead_right"
    if 65.0 < turn_deg <= 165.0:
        return "right"
    if -65.0 <= turn_deg < -25.0:
        return "ahead_left"
    if -165.0 <= turn_deg < -65.0:
        return "left"
    return "ahead"


def _polyline_length(pts: list[list[float]]) -> float:
    total = 0.0
    for i in range(len(pts) - 1):
        total += math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1])
    return total


def _point_and_dir_from_end(
    pts: list[list[float]], forward: bool, dist_before_end: float
) -> tuple[float, float, float, float] | None:
    """Return (x, z, dx, dz) at dist_before_end metres before the arrival node."""
    ordered = pts if forward else list(reversed(pts))
    total = _polyline_length(ordered)
    if total < dist_before_end:
        return None
    target_along = total - dist_before_end
    acc = 0.0
    for i in range(len(ordered) - 1):
        x0, z0 = ordered[i]
        x1, z1 = ordered[i + 1]
        seg = math.hypot(x1 - x0, z1 - z0)
        if seg < 1e-6:
            continue
        if acc + seg >= target_along:
            t = (target_along - acc) / seg
            x = x0 + t * (x1 - x0)
            z = z0 + t * (z1 - z0)
            return x, z, (x1 - x0) / seg, (z1 - z0) / seg
        acc += seg
    return None


def _arrival_tangent(pts: list[list[float]], forward: bool) -> tuple[float, float]:
    ordered = pts if forward else list(reversed(pts))
    for i in range(len(ordered) - 2, -1, -1):
        dx = ordered[i + 1][0] - ordered[i][0]
        dz = ordered[i + 1][1] - ordered[i][1]
        seg = math.hypot(dx, dz)
        if seg > 1e-4:
            return dx / seg, dz / seg
    return 0.0, 1.0


def _departure_tangent(pts: list[list[float]], forward: bool) -> tuple[float, float]:
    ordered = pts if forward else list(reversed(pts))
    for i in range(len(ordered) - 1):
        dx = ordered[i + 1][0] - ordered[i][0]
        dz = ordered[i + 1][1] - ordered[i][1]
        seg = math.hypot(dx, dz)
        if seg > 1e-4:
            return dx / seg, dz / seg
    return 0.0, 1.0


def _format_row_text_and_symbol(poi: dict[str, Any]) -> tuple[str, str] | None:
    """Return (text, symbol) for a POI, or None to skip unnamed car parks."""
    kind = poi["kind"]
    if kind in GENERIC_TEXT_AND_SYMBOL:
        return GENERIC_TEXT_AND_SYMBOL[kind]
    name = poi["name"].replace("&", "and").strip()
    if kind == "car_park":
        if name.lower() in ("car park", "parking", ""):
            return None
        if len(name) > 36 and "(" in name:
            name = name.split("(")[0].strip()
        return name, "parking"
    if len(name) > 36 and "(" in name:
        name = name.split("(")[0].strip()
    return name, "none"


def _extract_primary_ref(link: dict[str, Any]) -> str | None:
    raw = str(link.get("ref") or "").split(";")[0].strip()
    if raw and ROUTE_REF_RE.fullmatch(raw):
        return raw
    return None


class RoadGraph:
    """Directed traversal graph respecting oneway, restrictions, and laneConnections."""

    def __init__(self, pack: dict[str, Any]) -> None:
        self.nodes: dict[str, dict[str, Any]] = {n["id"]: n for n in pack["nodes"]}
        self.links: dict[str, dict[str, Any]] = {lk["id"]: lk for lk in pack["links"]}
        self.link_lengths: dict[str, float] = {
            lid: _polyline_length(lk["geometry"]) for lid, lk in self.links.items()
        }
        self.link_lines: dict[str, LineString] = {
            lid: LineString([(p[0], p[1]) for p in lk["geometry"]])
            for lid, lk in self.links.items()
            if len(lk["geometry"]) >= 2
        }
        self.island_polys = [poly for _, poly in extract_roundabout_clusters(pack)]

        self.out_edges: dict[str, list[tuple[str, bool, str]]] = defaultdict(list)
        self.in_edges: dict[str, list[tuple[str, bool, str]]] = defaultdict(list)
        for lk in pack["links"]:
            if not lk.get("inMainNetwork", True):
                continue
            lid = lk["id"]
            u, v = lk["from"], lk["to"]
            self.out_edges[u].append((lid, True, v))
            self.in_edges[v].append((lid, True, u))
            if not lk.get("oneway", False):
                self.out_edges[v].append((lid, False, u))
                self.in_edges[u].append((lid, False, v))

        self.forbidden_turns: set[tuple[str, str, str]] = set()
        self.only_turns: dict[tuple[str, str], set[str]] = defaultdict(set)
        for r in pack.get("restrictions", []):
            rtype = r.get("type", "")
            key = (r["fromLink"], r["viaNode"])
            if rtype.startswith("no_"):
                self.forbidden_turns.add((r["fromLink"], r["viaNode"], r["toLink"]))
            elif rtype.startswith("only_"):
                self.only_turns[key].add(r["toLink"])

        self.lane_conns: dict[tuple[str, str], set[str]] = defaultdict(set)
        for lc in pack.get("laneConnections", []):
            self.lane_conns[(lc["fromLink"], lc["nodeId"])].add(lc["toLink"])

    def allowed_next(
        self, from_link: str, via_node: str
    ) -> list[tuple[str, bool, str]]:
        candidates = self.out_edges.get(via_node, [])
        key = (from_link, via_node)
        only = self.only_turns.get(key)
        conns = self.lane_conns.get(key)
        out: list[tuple[str, bool, str]] = []
        for to_link, fwd, next_node in candidates:
            if to_link == from_link:
                continue
            if (from_link, via_node, to_link) in self.forbidden_turns:
                continue
            if only and to_link not in only:
                continue
            if conns and to_link not in conns:
                continue
            out.append((to_link, fwd, next_node))
        return out

    def shortest_first_turns(
        self, approach_link: str, approach_fwd: bool, junction_node: str
    ) -> dict[str, tuple[float, str, frozenset[str]]]:
        """Dijkstra from (approach_link, junction_node) -> {link_id: (dist_m, first_out, refs)}."""
        best_link: dict[str, tuple[float, str, frozenset[str]]] = {}
        visited: set[tuple[str, str]] = set()
        pq: list[tuple[float, int, str, str, str, frozenset[str]]] = []
        seq = 0

        for out_link, _, next_node in self.allowed_next(approach_link, junction_node):
            cost = self.link_lengths.get(out_link, 1.0)
            ref = _extract_primary_ref(self.links[out_link])
            refs = frozenset([ref]) if ref else frozenset()
            seq += 1
            heapq.heappush(pq, (cost, seq, out_link, out_link, next_node, refs))

        while pq:
            dist, _, first_out, cur_link, cur_node, refs = heapq.heappop(pq)
            state = (cur_link, cur_node)
            if state in visited:
                continue
            visited.add(state)
            if cur_link not in best_link or dist < best_link[cur_link][0]:
                best_link[cur_link] = (dist, first_out, refs)
            for nxt_link, _, nxt_node in self.allowed_next(cur_link, cur_node):
                if (nxt_link, nxt_node) not in visited:
                    nxt_ref = _extract_primary_ref(self.links[nxt_link])
                    nxt_refs = refs | ({nxt_ref} if nxt_ref else set())
                    seq += 1
                    heapq.heappush(
                        pq,
                        (
                            dist + self.link_lengths.get(nxt_link, 1.0),
                            seq,
                            first_out,
                            nxt_link,
                            nxt_node,
                            nxt_refs,
                        ),
                    )
        return best_link

    def on_carriageway(self, x: float, z: float, min_clearance_m: float = 0.5) -> bool:
        """Check against both RoadPack widthM and game 3.25 m lane geometry + islands."""
        pt = Point(x, z)
        for poly in self.island_polys:
            if poly.contains(pt) or pt.distance(poly) < 0.5:
                return True
        for lid, line in self.link_lines.items():
            lk = self.links[lid]
            half_w = max(float(lk.get("widthM", 6.5)) / 2.0, game_lane_half_width(lk))
            if pt.distance(line) < (half_w + min_clearance_m):
                return True
        return False

    def clears_game_geometry(
        self, x: float, z: float, lane_clearance_m: float = 2.0
    ) -> bool:
        """True if (x, z) is outside roundabout islands and >= lane_clearance_m from game lanes."""
        pt = Point(x, z)
        for poly in self.island_polys:
            if poly.contains(pt) or pt.distance(poly) < 0.8:
                return False
        for lid, line in self.link_lines.items():
            lk = self.links[lid]
            half_w = max(float(lk.get("widthM", 6.5)) / 2.0, game_lane_half_width(lk))
            req = lane_clearance_m if lk.get("inMainNetwork", True) else 0.5
            if pt.distance(line) < (half_w + req):
                return False
        return True

    def place_upstream(
        self,
        start_link: str,
        start_fwd: bool,
        target_dist_m: float = 40.0,
    ) -> tuple[float, float, float, float, float, str] | None:
        """Walk backwards up to 4 hops to place a sign 30-50 m before the junction."""
        cur_link = start_link
        cur_fwd = start_fwd
        remaining = target_dist_m
        for _ in range(5):
            lk = self.links[cur_link]
            length = self.link_lengths[cur_link]
            if length >= remaining:
                res = _point_and_dir_from_end(lk["geometry"], cur_fwd, remaining)
                if res is not None:
                    px, pz, dx, dz = res
                    return px, pz, dx, dz, target_dist_m, cur_link
            start_node = lk["from"] if cur_fwd else lk["to"]
            remaining -= length
            if remaining <= 0.0:
                remaining = 5.0
            in_dx, in_dz = _departure_tangent(lk["geometry"], cur_fwd)
            best_pred: tuple[float, str, bool] | None = None
            for p_link, p_fwd, _ in self.in_edges.get(start_node, []):
                if p_link == cur_link:
                    continue
                p_dx, p_dz = _arrival_tangent(self.links[p_link]["geometry"], p_fwd)
                dot = p_dx * in_dx + p_dz * in_dz
                if best_pred is None or dot > best_pred[0]:
                    best_pred = (dot, p_link, p_fwd)
            if best_pred is None:
                if start_link == cur_link and length >= 25.0:
                    res = _point_and_dir_from_end(lk["geometry"], cur_fwd, min(30.0, length - 2.0))
                    if res is not None:
                        px, pz, dx, dz = res
                        return px, pz, dx, dz, round(min(30.0, length - 2.0), 1), cur_link
                return None
            cur_link, cur_fwd = best_pred[1], best_pred[2]
        return None


def _is_major_junction(node: dict[str, Any], graph: RoadGraph) -> bool:
    nid = node["id"]
    incident_links = {lid for lid, _, _ in graph.in_edges.get(nid, [])} | {
        lid for lid, _, _ in graph.out_edges.get(nid, [])
    }
    if len(incident_links) < 2:
        return False
    if node.get("trafficSignals"):
        return True
    if node.get("kind") in ("roundabout", "mini_roundabout"):
        return True
    if len(incident_links) >= 3 and node.get("giveWay") and any(
        is_primary_approach(graph.links[lid]) for lid in incident_links
    ):
        return True
    return False


def build_direction_signs_and_street_names(
    roadpack_path: Path = ROADPACK_PATH,
    pois_path: Path = POIS_PATH,
) -> tuple[dict[str, Any], dict[str, Any]]:
    pack = json.loads(roadpack_path.read_text(encoding="utf-8"))
    pois_data = json.loads(pois_path.read_text(encoding="utf-8"))
    pois = pois_data["pois"]
    graph = RoadGraph(pack)

    signs: list[dict[str, Any]] = []
    sign_idx = 0

    for node in sorted(pack["nodes"], key=lambda n: n["id"]):
        if not _is_major_junction(node, graph):
            continue
        nid = node["id"]
        for app_link, app_fwd, _ in sorted(graph.in_edges.get(nid, []), key=lambda e: e[0]):
            lk = graph.links[app_link]
            link_dists = graph.shortest_first_turns(app_link, app_fwd, nid)
            if not link_dists:
                continue

            placed = None
            for target_dist in (40.0, 35.0, 30.0):
                placed = graph.place_upstream(app_link, app_fwd, target_dist)
                if placed is not None:
                    break
            if placed is None:
                continue

            px, pz, dx, dz, dist_before, host_link_id = placed
            host_lk = graph.links[host_link_id]
            sx, sz = px, pz
            placed_on_verge = False
            host_half = max(float(host_lk.get("widthM", 6.5)) / 2.0, game_lane_half_width(host_lk))
            # Prefer >= 3.0 m from lane edge (2.0 m + 1.0 m half panel), fallback >= 2.0 m
            for req_clear in (3.0, 2.0):
                for extra in (3.1, 3.8, 4.8, 6.0, 2.2):
                    if extra < req_clear:
                        continue
                    verge_offset = host_half + extra
                    cand_x = round(px + (-dz) * verge_offset, 3)
                    cand_z = round(pz + dx * verge_offset, 3)
                    if graph.clears_game_geometry(cand_x, cand_z, lane_clearance_m=req_clear):
                        sx, sz = cand_x, cand_z
                        placed_on_verge = True
                        break
                if placed_on_verge:
                    break
            if not placed_on_verge:
                continue

            in_dx, in_dz = _arrival_tangent(lk["geometry"], app_fwd)
            turn_dir_by_out: dict[str, str] = {}
            for out_link, out_fwd, _ in graph.allowed_next(app_link, nid):
                out_dx, out_dz = _departure_tangent(graph.links[out_link]["geometry"], out_fwd)
                turn_dir_by_out[out_link] = _classify_turn(
                    _signed_turn_deg(in_dx, in_dz, out_dx, out_dz)
                )

            _Cand = tuple[int, float, dict[str, Any], str, str, str, str, frozenset[str]]
            turning_candidates: list[_Cand] = []
            ahead_candidates: list[_Cand] = []
            for poi in pois:
                formatted = _format_row_text_and_symbol(poi)
                if formatted is None:
                    continue
                acc = poi["accessLinkId"]
                if acc not in link_dists:
                    continue
                d_m, first_out, route_refs = link_dists[acc]
                direction = turn_dir_by_out.get(first_out)
                if not direction:
                    continue
                row_text, symbol = formatted
                item = (
                    KIND_PRIORITY.get(poi["kind"], 9),
                    d_m,
                    poi,
                    direction,
                    first_out,
                    row_text,
                    symbol,
                    route_refs,
                )
                if direction == "ahead":
                    ahead_candidates.append(item)
                else:
                    turning_candidates.append(item)

            # R12.2: only sign destinations that turn/fork at this junction, plus up to 2 local
            # destinations and at most 1 ahead destination when multiple exits exist.
            turning_candidates.sort(key=lambda it: (it[0], it[1], it[2]["id"]))
            ahead_candidates.sort(key=lambda it: (it[0], it[1], it[2]["id"]))

            chosen_rows: list[dict[str, Any]] = []
            seen_kinds: set[str] = set()
            seen_texts: set[str] = set()
            dir_counts: dict[str, int] = defaultdict(int)
            local_count = 0

            def _try_add(item: _Cand, max_per_dir: int = 2) -> bool:
                nonlocal local_count
                _, _, poi, direction, first_out, row_text, symbol, route_refs = item
                if poi["kind"] in seen_kinds or row_text in seen_texts:
                    return False
                if dir_counts[direction] >= max_per_dir:
                    return False
                if poi["kind"] in LOCAL_KINDS and local_count >= 2:
                    return False
                seen_kinds.add(poi["kind"])
                seen_texts.add(row_text)
                dir_counts[direction] += 1
                if poi["kind"] in LOCAL_KINDS:
                    local_count += 1
                row: dict[str, Any] = {
                    "text": row_text,
                    "poiId": poi["id"],
                    "symbol": symbol,
                    "direction": direction,
                }
                out_ref = _extract_primary_ref(graph.links[first_out])
                if out_ref and out_ref in route_refs:
                    row["ref"] = out_ref
                chosen_rows.append(row)
                return True

            # First pick turning/forking destinations across distinct directions (max 2 per dir)
            for it in turning_candidates:
                if len(chosen_rows) >= 4:
                    break
                _try_add(it, max_per_dir=2)

            # Add at most 1 ahead destination when there are multiple exits or turning rows
            if ahead_candidates and len(chosen_rows) < 4:
                if turning_candidates or len(turn_dir_by_out) >= 2:
                    _try_add(ahead_candidates[0], max_per_dir=1)
                elif not chosen_rows:
                    _try_add(ahead_candidates[0], max_per_dir=1)

            if not chosen_rows:
                continue

            sign_idx += 1
            style = "primary" if is_primary_approach(lk) else "local"
            signs.append(
                {
                    "id": f"ds_{sign_idx:03d}",
                    "nodeId": nid,
                    "approachLinkId": app_link,
                    "x": sx,
                    "z": sz,
                    "heading": _heading_deg(dx, dz),
                    "distanceBeforeJunctionM": dist_before,
                    "style": style,
                    "rows": chosen_rows,
                }
            )

    # Street-name plates at back of footway at junction corners (>= 1.6 m from game lane edge)
    plates: list[dict[str, Any]] = []
    plate_idx = 0
    seen_plate_keys: set[tuple[str, str]] = set()
    for node in sorted(pack["nodes"], key=lambda n: n["id"]):
        if node["kind"] not in ("junction", "roundabout", "mini_roundabout"):
            continue
        nid = node["id"]
        incident = []
        for lk in pack["links"]:
            if lk["from"] == nid:
                incident.append((lk, True))
            elif lk["to"] == nid:
                incident.append((lk, False))
        if len(incident) < 3:
            continue
        for lk, from_node in sorted(incident, key=lambda item: item[0]["id"]):
            name = (lk.get("name") or "").strip()
            if not name or (nid, name) in seen_plate_keys:
                continue
            pts = lk["geometry"]
            if _polyline_length(pts) < 8.0:
                continue
            ordered = pts if from_node else list(reversed(pts))
            res = _point_and_dir_from_end(list(reversed(ordered)), True, 7.0)
            if res is None:
                continue
            px, pz, dx, dz = res
            lane_half = game_lane_half_width(lk)
            sx, sz = px, pz
            placed_plate = False
            for extra in (1.8, 2.4, 3.2):
                verge = lane_half + extra
                cand_x = round(px + (-dz) * verge, 3)
                cand_z = round(pz + dx * verge, 3)
                if graph.clears_game_geometry(cand_x, cand_z, lane_clearance_m=1.6):
                    sx, sz = cand_x, cand_z
                    placed_plate = True
                    break
            if not placed_plate:
                continue
            seen_plate_keys.add((nid, name))
            plate_idx += 1
            plates.append(
                {
                    "id": f"sn_{plate_idx:03d}",
                    "nodeId": nid,
                    "linkId": lk["id"],
                    "x": sx,
                    "z": sz,
                    "heading": _heading_deg(dx, dz),
                    "name": name,
                }
            )

    base_meta = {
        "signsFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "headingConvention": "Degrees clockwise from +z (North): 0=+z, 90=+x, 180=-z, 270=-x",
        "source": {
            "kind": pack["source"]["kind"],
            "licence": pack["source"]["licence"],
            "attribution": pack["source"]["attribution"],
            "generator": f"ltl_pipeline/{__version__}",
            "styleSources": [GOV_UK_TRAFFIC_SIGNS_URL, GOV_UK_KYTS_URL],
        },
    }
    dir_doc = {**base_meta, "signs": signs}
    street_doc = {**base_meta, "plates": plates}
    return dir_doc, street_doc


def write_direction_signs_and_street_names(
    roadpack_path: Path = ROADPACK_PATH,
    pois_path: Path = POIS_PATH,
    direction_signs_path: Path = DIRECTION_SIGNS_PATH,
    street_names_path: Path = STREET_NAMES_PATH,
) -> tuple[Path, Path]:
    dir_doc, street_doc = build_direction_signs_and_street_names(roadpack_path, pois_path)
    direction_signs_path.write_text(json.dumps(dir_doc, indent=2) + "\n", encoding="utf-8")
    street_names_path.write_text(json.dumps(street_doc, indent=2) + "\n", encoding="utf-8")
    validate_signs_placed_file(direction_signs_path)
    validate_signs_placed_file(street_names_path)
    return direction_signs_path, street_names_path


if __name__ == "__main__":
    d_path, s_path = write_direction_signs_and_street_names()
    print(f"Wrote {d_path} and {s_path}")
