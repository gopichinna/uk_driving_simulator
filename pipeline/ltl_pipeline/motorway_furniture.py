"""Generate motorway_signs.json and gantry_messages.json for content/cities/m54-j2/."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from ltl_pipeline import __version__
from ltl_pipeline.direction_signs import (
    GOV_UK_KYTS_URL,
    RoadGraph,
    _heading_deg,
    _point_and_dir_from_end,
    game_lane_half_width,
)
from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_gantry_messages_file,
    validate_signs_placed_file,
)

CITY_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"
GOV_UK_MOTORWAY_URL = "https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273"


def _walk_back_mainline(
    graph: RoadGraph,
    start_link: str,
    dist_m: float,
) -> tuple[float, float, float, float, str] | None:
    """Walk backwards along motorway mainline links by dist_m."""
    cur_link = start_link
    remaining = dist_m
    for _ in range(64):
        lk = graph.links[cur_link]
        length = graph.link_lengths[cur_link]
        if length >= remaining:
            res = _point_and_dir_from_end(lk["geometry"], True, remaining)
            if res is not None:
                px, pz, dx, dz = res
                return px, pz, dx, dz, cur_link
        remaining -= length
        preds = [
            p_link
            for p_link, p_fwd, _ in graph.in_edges.get(lk["from"], [])
            if (
                p_fwd
                and graph.links[p_link]["roadClass"] == "motorway"
                and graph.links[p_link].get("hardShoulder")
            )
        ]
        if not preds:
            return None
        cur_link = preds[0]
    return None


def _max_back_mainline_m(graph: RoadGraph, start_link: str) -> float:
    """Return total walkable distance backwards along motorway mainline from start_link."""
    cur_link = start_link
    total = 0.0
    for _ in range(64):
        lk = graph.links[cur_link]
        total += graph.link_lengths[cur_link]
        preds = [
            p_link
            for p_link, p_fwd, _ in graph.in_edges.get(lk["from"], [])
            if (
                p_fwd
                and graph.links[p_link]["roadClass"] == "motorway"
                and graph.links[p_link].get("hardShoulder")
            )
        ]
        if not preds:
            break
        cur_link = preds[0]
    return total


def build_motorway_signs_and_gantries(
    city_dir: Path = CITY_DIR,
) -> tuple[dict[str, Any], dict[str, Any]]:
    pack = json.loads((city_dir / "roadpack.json").read_text(encoding="utf-8"))
    graph = RoadGraph(pack)

    signs: list[dict[str, Any]] = []
    idx = 0

    # Find mainline links where a slip road diverges (exit)
    exit_nodes_seen: set[str] = set()
    for lk in sorted(pack["links"], key=lambda item: item["id"]):
        if lk["roadClass"] != "motorway" or not lk.get("hardShoulder"):
            continue
        nid = lk["to"]
        if nid in exit_nodes_seen:
            continue
        slips = [
            out_l
            for out_l, out_f, _ in graph.out_edges.get(nid, [])
            if out_f
            and graph.links[out_l]["roadClass"] == "motorway"
            and not graph.links[out_l].get("hardShoulder")
        ]
        if not slips:
            continue
        exit_nodes_seen.add(nid)
        jnum = graph.links[slips[0]].get("junctionNumber", "2")
        route_ref = lk.get("ref") or "M54"
        slip_dest = graph.links[slips[0]].get("destination")
        dest_list = [d.strip() for d in slip_dest.split(";") if d.strip()] if slip_dest else [
            "Wolverhampton",
            "M54",
        ]
        dest_conf = "high" if slip_dest else "low"

        max_back = _max_back_mainline_m(graph, lk["id"])
        dist_1m = min(1609.0, max_back - 20.0) if max_back >= 1000.0 else 1609.0
        conf_1m = "low" if dist_1m < 1609.0 or not slip_dest else "high"

        specs = [
            ("advance_1m", dist_1m, None, f"J{jnum} 1 mile", conf_1m),
            ("advance_half_m", 804.0, None, f"J{jnum} 1/2 mile", dest_conf),
            ("countdown_3", 274.0, 3, None, None),
            ("countdown_2", 183.0, 2, None, None),
            ("countdown_1", 91.0, 1, None, None),
            ("final_exit", 15.0, None, f"Exit J{jnum}", dest_conf),
            ("junction_number", 5.0, None, f"J{jnum}", None),
        ]
        for kind, dist_m, bars, text, conf in specs:
            placed = _walk_back_mainline(graph, lk["id"], dist_m)
            if placed is None:
                continue
            px, pz, dx, dz, host_id = placed
            host_lk = graph.links[host_id]
            verge = game_lane_half_width(host_lk) + 3.8  # incl. hard shoulder + verge
            sx = round(px + (-dz) * verge, 3)
            sz = round(pz + dx * verge, 3)
            idx += 1
            entry: dict[str, Any] = {
                "id": f"ms_{idx:03d}",
                "kind": kind,
                "x": sx,
                "z": sz,
                "heading": _heading_deg(dx, dz),
                "style": "motorway",
                "junctionNumber": jnum,
                "routeNumber": route_ref,
                "linkId": host_id,
            }
            if kind in ("advance_1m", "advance_half_m", "final_exit"):
                entry["destinations"] = dest_list
            if conf is not None:
                entry["confidence"] = conf
            if bars is not None:
                entry["bars"] = bars
            if text is not None:
                entry["text"] = text
            signs.append(entry)

    # Emergency area signs
    for ea in pack.get("emergencyAreas", []):
        idx += 1
        signs.append(
            {
                "id": f"ms_{idx:03d}",
                "kind": "emergency_area",
                "x": ea["x"],
                "z": ea["z"],
                "heading": ea["heading"],
                "style": "emergency_area",
                "text": "Emergency Area SOS",
                "linkId": ea["linkId"],
            }
        )

    # Gantry messages plan
    gantries_out: list[dict[str, Any]] = []
    for g in pack.get("gantries", []):
        lanes = int(g["lanes"])
        g_lk = graph.links.get(g["linkId"], {})
        jnum = g_lk.get("junctionNumber") or "2"
        gantries_out.append(
            {
                "id": g["id"],
                "linkId": g["linkId"],
                "x": g["x"],
                "z": g["z"],
                "heading": g["heading"],
                "lanes": lanes,
                "laneIndices": list(range(lanes)),
                "supportsRedX": True,
                "supportsVariableLimit": True,
                "defaultLimitMph": 70,
                "junctionNumber": jnum,
            }
        )

    base_source = {
        "kind": pack["source"]["kind"],
        "licence": pack["source"]["licence"],
        "attribution": pack["source"]["attribution"],
        "generator": f"ltl_pipeline/{__version__}",
        "styleSources": [GOV_UK_MOTORWAY_URL, GOV_UK_KYTS_URL],
    }
    signs_doc = {
        "signsFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "headingConvention": "Degrees clockwise from +z (North): 0=+z, 90=+x, 180=-z, 270=-x",
        "source": base_source,
        "motorwaySigns": signs,
    }
    gantries_doc = {
        "gantryMessagesFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "source": base_source,
        "gantries": gantries_out,
    }
    return signs_doc, gantries_doc


def write_motorway_signs_and_gantries(city_dir: Path = CITY_DIR) -> tuple[Path, Path]:
    signs_doc, gantries_doc = build_motorway_signs_and_gantries(city_dir)
    s_path = city_dir / "motorway_signs.json"
    g_path = city_dir / "gantry_messages.json"
    s_path.write_text(json.dumps(signs_doc, indent=2) + "\n", encoding="utf-8")
    g_path.write_text(json.dumps(gantries_doc, indent=2) + "\n", encoding="utf-8")
    validate_signs_placed_file(s_path)
    validate_gantry_messages_file(g_path)
    return s_path, g_path


if __name__ == "__main__":
    sp, gp = write_motorway_signs_and_gantries()
    print(f"Wrote {sp} and {gp}")
