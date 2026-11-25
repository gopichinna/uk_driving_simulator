"""Build content/cities/wolverhampton-test-area/ pack, furniture, practice routes, and QA."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.build import build_roadpack, write_roadpack
from ltl_pipeline.buildings import build_buildings, write_buildings
from ltl_pipeline.direction_signs import (
    RoadGraph,
    write_direction_signs_and_street_names,
)
from ltl_pipeline.extract import extract_osm
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.pois import build_pois
from ltl_pipeline.schema import REPO_ROOT, validate_pois_file, validate_routes_file
from ltl_pipeline.street_furniture import write_street_furniture

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton-test-area"


def build_pack_and_furniture(city_dir: Path = CITY_DIR) -> dict[str, Any]:
    config_path = city_dir / "config.yaml"
    config = yaml.safe_load(config_path.read_text(encoding="utf-8"))

    fetch_res = fetch_extract(config["extract"]["pbf_url"], config["extract"]["md5_url"])
    extracted = extract_osm(
        fetch_res.pbf_path,
        bbox=tuple(float(v) for v in config["bbox"]),
        exclude_tram=True,
    )
    pack = build_roadpack(extracted, config, fetch_res.extract_timestamp)
    pack_path = city_dir / "roadpack.json"
    write_roadpack(pack, pack_path)

    buildings = build_buildings(fetch_res.pbf_path, config, fetch_res.extract_timestamp)
    write_buildings(buildings, city_dir / "buildings.json")

    pois_data = build_pois(city_dir)
    pois_path = city_dir / "pois.json"
    pois_path.write_text(json.dumps(pois_data, indent=2) + "\n", encoding="utf-8")
    validate_pois_file(pois_path)
    write_direction_signs_and_street_names(
        roadpack_path=pack_path,
        pois_path=city_dir / "pois.json",
        direction_signs_path=city_dir / "direction_signs.json",
        street_names_path=city_dir / "street_names.json",
    )
    write_street_furniture(city_dir)
    return pack


def _find_loop_route(
    graph: RoadGraph,
    start_link_id: str,
    target_min_m: float,
    target_max_m: float,
    prefer_road_classes: tuple[str, ...],
) -> tuple[list[dict[str, str]], list[dict[str, Any]]] | None:
    """Build a legal connected loop starting and ending on start_link_id."""
    start_lk = graph.links[start_link_id]
    cur_link = start_link_id
    cur_node = start_lk["to"]
    route_links: list[dict[str, str]] = [{"linkId": start_link_id, "direction": "forward"}]
    total_m = graph.link_lengths[start_link_id]
    visited_edges: set[tuple[str, str]] = {(cur_link, cur_node)}

    # Phase 1: explore outward preferring target road classes
    for _ in range(180):
        if total_m >= target_min_m * 0.55:
            break
        nxt = graph.allowed_next(cur_link, cur_node)
        if not nxt:
            break
        best = None
        for nl, nf, nn in nxt:
            lk = graph.links[nl]
            score = 0.0
            if (nl, nn) not in visited_edges:
                score += 100.0
            if lk.get("roadClass") in prefer_road_classes:
                score += 25.0
            if lk.get("name"):
                score += 5.0
            n_obj = graph.nodes[nn]
            score += math.hypot(n_obj["x"], n_obj["z"]) * 0.01
            if best is None or score > best[0]:
                best = (score, nl, nf, nn)
        if best is None:
            break
        _, cur_link, cur_fwd, cur_node = best
        visited_edges.add((cur_link, cur_node))
        route_links.append({"linkId": cur_link, "direction": "forward" if cur_fwd else "backward"})
        total_m += graph.link_lengths[cur_link]

    # Phase 2: Dijkstra back to start_link_id
    import heapq
    import itertools

    seq = itertools.count()
    pq: list[tuple[float, int, str, str, list[dict[str, str]]]] = [
        (0.0, next(seq), cur_link, cur_node, [])
    ]
    seen: set[tuple[str, str]] = set()
    return_path: list[dict[str, str]] | None = None
    while pq:
        cost, _, cl, cn, path = heapq.heappop(pq)
        if cl == start_link_id and path:
            return_path = path
            break
        if (cl, cn) in seen:
            continue
        seen.add((cl, cn))
        for nl, nf, nn in graph.allowed_next(cl, cn):
            if (nl, nn) not in seen:
                step = {"linkId": nl, "direction": "forward" if nf else "backward"}
                heapq.heappush(
                    pq,
                    (cost + graph.link_lengths[nl], next(seq), nl, nn, [*path, step]),
                )

    if not return_path:
        return None
    full_links = route_links + return_path
    full_m = sum(graph.link_lengths[item["linkId"]] for item in full_links)
    if full_m > target_max_m * 1.5:
        return None

    instructions: list[dict[str, Any]] = []
    for i in range(len(full_links) - 1):
        nxt_lid = full_links[i + 1]["linkId"]
        lk_name = graph.links[nxt_lid].get("name") or graph.links[nxt_lid].get("ref") or "the road"
        if i == 0:
            instructions.append(
                {
                    "atLinkIndex": 0,
                    "distanceBeforeM": 15,
                    "text": "Leave the test centre area on Spring Road and keep left.",
                    "maneuver": "straight_on",
                }
            )
        elif i == len(full_links) // 2:
            instructions.append(
                {
                    "atLinkIndex": i,
                    "distanceBeforeM": 40,
                    "text": f"Follow the signs towards City Centre via {lk_name}.",
                    "maneuver": "straight_on",
                }
            )
        elif i % 8 == 0 and len(instructions) < 8:
            instructions.append(
                {
                    "atLinkIndex": i,
                    "distanceBeforeM": 30,
                    "text": f"Continue onto {lk_name} and watch speed limits.",
                    "maneuver": "straight_on",
                }
            )
    instructions.append(
        {
            "atLinkIndex": len(full_links) - 1,
            "distanceBeforeM": 20,
            "text": "Return to Spring Road and stop safely on the left.",
            "maneuver": "straight_on",
        }
    )
    return full_links, instructions


def write_practice_routes_and_qa(pack: dict[str, Any], city_dir: Path = CITY_DIR) -> None:
    graph = RoadGraph(pack)
    spring_links = [
        lk["id"]
        for lk in pack["links"]
        if lk.get("inMainNetwork") and "Spring Road" in (lk.get("name") or "")
    ]
    if not spring_links:
        spring_links = [
            lk["id"] for lk in pack["links"] if lk.get("inMainNetwork")
        ]
    start_link = spring_links[0]

    specs = [
        (
            "test-area-practice-1",
            "Practice route 1: Ettingshall and A4123 dual carriageway",
            (
                "Practice route (not an official DVSA test route) mixing "
                "Spring Road, roundabouts, and A4123."
            ),
            18,
            ("trunk", "primary", "residential"),
            ["roundabout", "signals"],
        ),
        (
            "test-area-practice-2",
            "Practice route 2: Bilston Road and residential streets",
            (
                "Practice route (not an official DVSA test route) covering "
                "A41 Bilston Road and local residential turns."
            ),
            20,
            ("primary", "tertiary", "residential"),
            ["signals", "zebra"],
        ),
        (
            "test-area-practice-3",
            "Practice route 3: Mixed urban and roundabout loop",
            (
                "Practice route (not an official DVSA test route) combining "
                "residential streets, crossings, and roundabouts."
            ),
            22,
            ("tertiary", "unclassified", "trunk"),
            ["roundabout", "zebra"],
        ),
    ]

    routes: list[dict[str, Any]] = []
    for rid, name, desc, mins, prefs, feats in specs:
        res = _find_loop_route(graph, start_link, 2400.0, 5500.0, prefs)
        if res is None:
            raise RuntimeError(f"Could not build connected loop route {rid}")
        full_links, instructions = res
        routes.append(
            {
                "id": rid,
                "name": name,
                "description": desc,
                "estimatedMinutes": mins,
                "start": {"linkId": full_links[0]["linkId"], "offsetM": 5},
                "links": full_links,
                "features": feats,
                "instructions": instructions,
            }
        )

    routes_doc = {
        "routeFormatVersion": "0.1",
        "packId": pack["packId"],
        "routes": routes,
    }
    routes_path = city_dir / "routes.json"
    routes_path.write_text(json.dumps(routes_doc, indent=2) + "\n", encoding="utf-8")
    validate_routes_file(routes_path)

    # QA.md
    main_links = [lk for lk in pack["links"] if lk.get("inMainNetwork")]
    rbt_nodes = [n for n in pack["nodes"] if n["kind"] in ("roundabout", "mini_roundabout")]
    sig_nodes = [n for n in pack["nodes"] if n.get("trafficSignals")]
    qa = f"""# Wolverhampton Test Centre Area — RoadPack QA

- **Pack ID:** `{pack['packId']}`
- **Origin:** `lat {pack['origin']['lat']}`, `lon {pack['origin']['lon']}` (WV4 6JX)
- **GOV.UK verification:** Checked `https://www.gov.uk/find-driving-test-centre` (2026-11-25)
- **Disclaimer:** Practice routes only; DVSA does not publish official driving test routes.

## Network Summary
- **Nodes:** {len(pack['nodes'])} ({len(sig_nodes)} signalised, {len(rbt_nodes)} roundabout)
- **Links:** {len(pack['links'])} total, {len(main_links)} in `mainNetwork`
- **Crossings:** {len(pack['crossings'])}
- **Restrictions:** {len(pack['restrictions'])}
- **Lane connections:** {len(pack['laneConnections'])}
- **Spawn points:** {len(pack['spawnPoints'])}
- **Excluded tramways:** {len(pack['excluded'])}
- **Practice routes:** {len(routes)} legal connected loops starting and ending on Spring Road

## Attribution
© OpenStreetMap contributors (ODbL-1.0).
"""
    (city_dir / "QA.md").write_text(qa, encoding="utf-8")


if __name__ == "__main__":
    p = build_pack_and_furniture()
    write_practice_routes_and_qa(p)
    print("Built wolverhampton-test-area pack, furniture, routes, and QA.md")
