"""Generate plain-English sat-nav instructions and mock-test route for Wolverhampton."""

from __future__ import annotations

import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Any

from ltl_pipeline.schema import REPO_ROOT, validate_routes_file

ROADPACK_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "roadpack.json"
ROUTES_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "routes.json"


def _link_length(lk: dict[str, Any]) -> float:
    g = lk["geometry"]
    return sum(math.hypot(g[i + 1][0] - g[i][0], g[i + 1][1] - g[i][1]) for i in range(len(g) - 1))


def _step_headings(lk: dict[str, Any], direction: str) -> tuple[float, float]:
    g = lk["geometry"] if direction == "forward" else list(reversed(lk["geometry"]))
    h_start = math.atan2(g[1][1] - g[0][1], g[1][0] - g[0][0])
    h_end = math.atan2(g[-1][1] - g[-2][1], g[-1][0] - g[-2][0])
    return (h_start, h_end)


def _signed_turn_deg(h_prev: float, h_next: float) -> float:
    """Return signed turn angle in degrees: positive = left, negative = right."""
    diff = (h_next - h_prev + math.pi) % (2 * math.pi) - math.pi
    return math.degrees(diff)


def generate_instructions(
    route: dict[str, Any],
    pack: dict[str, Any],
) -> list[dict[str, Any]]:
    """Generate plain-English sat-nav instructions from route geometry and junctionPriority."""
    links = {lk["id"]: lk for lk in pack["links"]}
    jp_by_node = {jp["nodeId"]: jp for jp in pack.get("junctionPriority", [])}

    steps = route["links"]
    instructions: list[dict[str, Any]] = []
    in_roundabout = False
    rbt_arcs = 0

    for idx in range(len(steps) - 1):
        cur_step = steps[idx]
        nxt_step = steps[idx + 1]
        cur_lk = links[cur_step["linkId"]]
        nxt_lk = links[nxt_step["linkId"]]
        via_node = cur_lk["to"] if cur_step["direction"] == "forward" else cur_lk["from"]

        _, h_out = _step_headings(cur_lk, cur_step["direction"])
        h_in, _ = _step_headings(nxt_lk, nxt_step["direction"])
        angle = _signed_turn_deg(h_out, h_in)

        dist_before = round(min(100.0, _link_length(cur_lk)), 1)
        street = nxt_lk.get("name")
        onto = f" onto {street}" if street else ""
        ctrl = jp_by_node.get(via_node, {}).get("controlledBy", "uncontrolled")

        if nxt_lk.get("junctionRoundabout"):
            if not in_roundabout:
                in_roundabout = True
                rbt_arcs = 1
            else:
                rbt_arcs += 1
            continue

        if in_roundabout and not nxt_lk.get("junctionRoundabout"):
            exit_n = max(1, min(4, rbt_arcs))
            ordinal = {1: "first", 2: "second", 3: "third", 4: "fourth"}[exit_n]
            instructions.append(
                {
                    "atLinkIndex": idx + 1,
                    "distanceBeforeM": dist_before,
                    "text": f"At the roundabout, take the {ordinal} exit{onto}.",
                    "maneuver": f"roundabout_exit_{exit_n}",
                }
            )
            in_roundabout = False
            rbt_arcs = 0
            continue

        if abs(angle) < 25.0:
            continue

        if angle >= 45.0:
            maneuver, verb = "turn_left", "turn left"
        elif angle >= 25.0:
            maneuver, verb = "bear_left", "bear left"
        elif angle <= -45.0:
            maneuver, verb = "turn_right", "turn right"
        else:
            maneuver, verb = "bear_right", "bear right"

        if ctrl == "signals":
            prefix = "At the traffic lights, "
        elif ctrl in ("give_way", "stop"):
            prefix = "At the give way, "
        else:
            prefix = "At the junction, "

        instructions.append(
            {
                "atLinkIndex": idx + 1,
                "distanceBeforeM": dist_before,
                "text": f"{prefix}{verb}{onto}.",
                "maneuver": maneuver,
            }
        )

    last_idx = len(steps) - 1
    last_lk = links[steps[last_idx]["linkId"]]
    instructions.append(
        {
            "atLinkIndex": last_idx,
            "distanceBeforeM": round(min(50.0, _link_length(last_lk)), 1),
            "text": "You have arrived at your destination.",
            "maneuver": "arrive",
        }
    )
    return instructions


def _build_mock_test_route(pack: dict[str, Any]) -> dict[str, Any]:
    """Find a ~10-minute (~4,200 m) legal connected route covering signals, roundabout, crossing."""
    nodes = {n["id"]: n for n in pack["nodes"]}
    links = {lk["id"]: lk for lk in pack["links"]}
    crossings_by_link = defaultdict(list)
    for c in pack["crossings"]:
        crossings_by_link[c["linkId"]].append(c["type"])

    forbidden: dict[tuple[str, str], set[str]] = defaultdict(set)
    only: dict[tuple[str, str], set[str]] = defaultdict(set)
    for r in pack.get("restrictions", []):
        key = (r["fromLink"], r["viaNode"])
        if r["type"].startswith("no_"):
            forbidden[key].add(r["toLink"])
        elif r["type"].startswith("only_"):
            only[key].add(r["toLink"])

    adj: dict[str, list[tuple[str, str, str]]] = defaultdict(list)
    for lid, lk in links.items():
        adj[lk["from"]].append((lk["to"], lid, "forward"))
        if not lk["oneway"]:
            adj[lk["to"]].append((lk["from"], lid, "backward"))

    def legal_next(prev_lid: str, cur_node: str) -> list[tuple[str, str, str]]:
        res = []
        for nxt_node, lid, direction in adj[cur_node]:
            if lid == prev_lid:
                continue
            key = (prev_lid, cur_node)
            if key in forbidden and lid in forbidden[key]:
                continue
            if key in only and lid not in only[key]:
                continue
            res.append((nxt_node, lid, direction))
        return res

    rbt_links = [lk["id"] for lk in pack["links"] if lk.get("junctionRoundabout")]
    start_lid = rbt_links[0]
    lk0 = links[start_lid]

    best_steps: list[dict[str, str]] = []

    def dfs(
        cur_node: str,
        prev_lid: str,
        steps: list[dict[str, str]],
        used: set[tuple[str, str]],
        total_m: float,
    ) -> bool:
        nonlocal best_steps
        if total_m >= 4100.0:
            best_steps = list(steps)
            return True
        if len(steps) >= 180:
            return False
        cands = legal_next(prev_lid, cur_node)
        cands.sort(
            key=lambda it: (
                0 if (it[1], it[2]) not in used else 1,
                0 if links[it[1]]["roadClass"] in ("primary", "secondary", "tertiary") else 1,
                -len(legal_next(it[1], it[0])),
            )
        )
        for nxt_n, lid, d in cands:
            if (lid, d) in used:
                continue
            steps.append({"linkId": lid, "direction": d})
            used.add((lid, d))
            if dfs(nxt_n, lid, steps, used, total_m + _link_length(links[lid])):
                return True
            steps.pop()
            used.discard((lid, d))
        return False

    dfs(
        lk0["to"],
        start_lid,
        [{"linkId": start_lid, "direction": "forward"}],
        {(start_lid, "forward")},
        _link_length(lk0),
    )

    feats: set[str] = set()
    for st in best_steps:
        lk = links[st["linkId"]]
        if nodes[lk["from"]].get("trafficSignals") or nodes[lk["to"]].get("trafficSignals"):
            feats.add("signals")
        if lk.get("junctionRoundabout") or nodes[lk["to"]]["kind"] == "roundabout":
            feats.add("roundabout")
        if lk.get("busLane"):
            feats.add("bus_lane")
        if lk.get("cycleLane"):
            feats.add("cycle_lane")
        for ct in crossings_by_link.get(st["linkId"], []):
            if ct in ("zebra", "pelican", "puffin", "toucan"):
                feats.add(ct)

    route = {
        "id": "wolverhampton-mock-test",
        "name": "Wolverhampton 10-Minute Mock Test",
        "description": (
            "Ten-minute city-centre mock test covering roundabouts, signals, and crossings."
        ),
        "estimatedMinutes": 10.0,
        "start": {"linkId": best_steps[0]["linkId"], "offsetM": 0},
        "links": best_steps,
        "features": sorted(feats),
    }
    route["instructions"] = generate_instructions(route, pack)
    return route


def update_routes_with_instructions(
    roadpack_path: Path = ROADPACK_PATH,
    routes_path: Path = ROUTES_PATH,
) -> Path:
    """Add instructions[] to existing routes and ensure wolverhampton-mock-test exists."""
    pack = json.loads(roadpack_path.read_text(encoding="utf-8"))
    routes_doc = json.loads(routes_path.read_text(encoding="utf-8"))

    existing = [r for r in routes_doc["routes"] if r["id"] != "wolverhampton-mock-test"]
    for r in existing:
        r["instructions"] = generate_instructions(r, pack)
    existing.append(_build_mock_test_route(pack))
    routes_doc["routes"] = existing

    routes_path.write_text(json.dumps(routes_doc, indent=2) + "\n", encoding="utf-8")
    validate_routes_file(routes_path)
    return routes_path


if __name__ == "__main__":
    written = update_routes_with_instructions()
    print(f"Updated {written}")
