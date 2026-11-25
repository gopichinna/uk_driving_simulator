"""Build a RoadPack v0 JSON structure from extracted OSM data."""

from __future__ import annotations

import json
import math
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline import __version__
from ltl_pipeline.extract import ExtractedOsm, OsmNode, OsmWay
from ltl_pipeline.geo import LocalProjection
from ltl_pipeline.schema import REPO_ROOT, validate_roadpack

DEFAULT_SPEED_DEFAULTS_PATH = REPO_ROOT / "content" / "countries" / "gb" / "speed_defaults.yaml"

CROSSING_TYPE_MAP = {
    "zebra": "zebra",
    "parallel": "parallel",
    "pelican": "pelican",
    "puffin": "puffin",
    "toucan": "toucan",
    "equestrian": "equestrian",
    "pegasus": "equestrian",
    "traffic_signals": "signals_unknown",
    "uncontrolled": "uncontrolled",
    "marked": "uncontrolled",
}


def load_speed_defaults(path: Path = DEFAULT_SPEED_DEFAULTS_PATH) -> dict[str, Any]:
    """Load country speed-limit defaults YAML."""
    with path.open("r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def parse_speed_limit_mph(
    way: OsmWay,
    region: str,
    speed_defaults: dict[str, Any],
) -> tuple[int, str]:
    """Return (speedLimitMph, speedLimitSource) for an OSM way."""
    raw = way.tags.get("maxspeed", "").strip().lower()
    region_cfg = speed_defaults["regions"][region]

    if raw:
        if raw in ("national", "gb:nsl_single"):
            return (int(region_cfg["single_carriageway_mph"]), "tagged")
        if raw == "gb:nsl_dual":
            return (int(region_cfg["dual_carriageway_mph"]), "tagged")
        if raw.startswith("gb:"):
            suffix = raw.split(":", 1)[1]
            if suffix.isdigit():
                return (_clamp_speed(int(suffix)), "tagged")

        m = re.match(r"^(\d+)\s*(mph)?$", raw)
        if m:
            return (_clamp_speed(int(m.group(1))), "tagged")

        m_kph = re.match(r"^(\d+)\s*(km/h|kph)$", raw)
        if m_kph:
            mph = round(int(m_kph.group(1)) * 0.621371)
            return (_clamp_speed(mph), "tagged")

    # Fallback to country defaults
    if way.tags.get("zone:maxspeed") == "GB:20":
        return (20, "default")

    class_map = speed_defaults["road_class_defaults"]
    default_key = class_map.get(way.road_class, "restricted_road_mph")
    if isinstance(default_key, int):
        return (_clamp_speed(default_key), "default")
    return (_clamp_speed(int(region_cfg[default_key])), "default")


def _clamp_speed(mph: int) -> int:
    return max(5, min(70, mph))


def _is_oneway(way: OsmWay) -> bool:
    ow = way.tags.get("oneway", "").strip().lower()
    if ow in ("yes", "true", "1"):
        return True
    if ow in ("no", "false", "0"):
        return False
    return way.tags.get("junction") == "roundabout" or way.road_class == "motorway"


def _parse_int(val: str | None) -> int | None:
    if not val:
        return None
    m = re.match(r"^\d+", val.strip())
    return int(m.group(0)) if m else None


def _parse_lanes(way: OsmWay, oneway: bool) -> tuple[int, int, bool]:
    """Return (lanesForward, lanesBackward, lanes_explicit)."""
    lf = _parse_int(way.tags.get("lanes:forward"))
    lb = _parse_int(way.tags.get("lanes:backward"))
    total = _parse_int(way.tags.get("lanes"))
    explicit = any(x is not None for x in (lf, lb, total))

    if oneway:
        if lf is not None:
            fwd = lf
        elif total is not None:
            fwd = total
        elif way.road_class in ("motorway", "trunk"):
            fwd = 2
        else:
            fwd = 1
        return (max(1, min(6, fwd)), 0, explicit)

    if lf is not None or lb is not None:
        fwd = lf if lf is not None else max(1, (total - lb) if total is not None else 1)
        bwd = lb if lb is not None else max(1, (total - lf) if total is not None else 1)
        return (max(0, min(6, fwd)), max(0, min(6, bwd)), explicit)

    if total is not None:
        if total <= 1:
            return (1, 1, explicit)
        fwd = total // 2
        bwd = total - fwd
        return (max(0, min(6, fwd)), max(0, min(6, bwd)), explicit)

    return (1, 1, False)


def _parse_turn_lanes(raw: str | None) -> list[str] | None:
    if not raw:
        return None
    parts = [p.strip() if p.strip() else "none" for p in raw.split("|")]
    return parts if parts else None


def _parse_bus_lane(way: OsmWay, oneway: bool) -> dict[str, Any] | None:
    busway = way.tags.get("busway")
    busway_left = way.tags.get("busway:left")
    busway_right = way.tags.get("busway:right")
    busway_both = way.tags.get("busway:both")
    bus_lanes = way.tags.get("bus:lanes")
    bus_fwd = way.tags.get("bus:lanes:forward")
    bus_bwd = way.tags.get("bus:lanes:backward")
    cond = (
        way.tags.get("bus:lanes:conditional")
        or way.tags.get("bus:lanes:forward:conditional")
        or way.tags.get("bus:lanes:backward:conditional")
    )

    has_bus = any(
        [
            busway in ("lane", "opposite_lane"),
            busway_left == "lane",
            busway_right == "lane",
            busway_both == "lane",
            bus_lanes and "designated" in bus_lanes,
            bus_fwd and "designated" in bus_fwd,
            bus_bwd and "designated" in bus_bwd,
        ]
    )
    if not has_bus:
        return None

    fwd = bool(
        busway == "lane"
        or busway_left == "lane"
        or busway_both == "lane"
        or (bus_fwd and "designated" in bus_fwd)
        or (bus_lanes and "designated" in bus_lanes)
    )
    bwd = bool(
        not oneway
        and (
            busway == "opposite_lane"
            or busway_right == "lane"
            or busway_both == "lane"
            or (bus_bwd and "designated" in bus_bwd)
        )
    )
    result: dict[str, Any] = {"forward": fwd, "backward": bwd}
    if cond and "@" in cond:
        hours = cond.split("@", 1)[1].strip().strip("()")
        if hours:
            result["hours"] = hours
    return result


def _map_cycleway_tag(val: str | None) -> str:
    if not val or val in ("no", "none"):
        return "none"
    if val in ("track", "separate"):
        return "track"
    if val == "share_busway":
        return "shared_busway"
    if val in ("lane", "opposite_lane"):
        return "lane_broken"
    return "none"


def _parse_cycle_lane(way: OsmWay, oneway: bool) -> dict[str, str] | None:
    c_both = way.tags.get("cycleway:both") or way.tags.get("cycleway")
    c_left = way.tags.get("cycleway:left")
    c_right = way.tags.get("cycleway:right")
    fwd = _map_cycleway_tag(c_left or c_both)
    bwd = "none" if oneway else _map_cycleway_tag(c_right or c_both)
    if fwd == "none" and bwd == "none":
        return None
    return {"forward": fwd, "backward": bwd}


def _classify_crossing(node: OsmNode) -> tuple[str, float] | None:
    """Return (crossing_type, confidence) if node is a pedestrian/cycle crossing."""
    cref = (node.tags.get("crossing_ref") or "").strip().lower()
    ctype = (node.tags.get("crossing") or "").strip().lower()
    hw = (node.tags.get("highway") or "").strip().lower()

    if cref in CROSSING_TYPE_MAP:
        return (CROSSING_TYPE_MAP[cref], 0.95)
    if ctype in CROSSING_TYPE_MAP:
        conf = 0.85 if ctype in ("zebra", "traffic_signals", "parallel") else 0.65
        return (CROSSING_TYPE_MAP[ctype], conf)
    if hw == "crossing" or ctype or cref:
        return ("signals_unknown" if node.tags.get("traffic_signals") else "uncontrolled", 0.4)
    return None


def _score_link_confidence(
    lanes_explicit: bool,
    has_turn_lanes: bool,
    speed_source: str,
) -> tuple[float, str]:
    score = 0.45
    notes: list[str] = []
    if lanes_explicit:
        score += 0.25
        notes.append("explicit lanes")
    else:
        notes.append("lanes inferred from roadClass")
    if has_turn_lanes:
        score += 0.15
        notes.append("explicit turn:lanes")
    if speed_source == "tagged":
        score += 0.15
        notes.append("tagged maxspeed")
    else:
        notes.append("default maxspeed")
    return (round(min(1.0, max(0.0, score)), 2), "; ".join(notes))


ROAD_CLASS_RANK = {
    "motorway": 9,
    "trunk": 8,
    "primary": 7,
    "secondary": 6,
    "tertiary": 5,
    "unclassified": 4,
    "residential": 3,
    "living_street": 2,
    "service": 1,
}


def _approach_angle(link: dict[str, Any], node_id: str) -> float:
    geom = link["geometry"]
    if link["to"] == node_id:
        p1, p2 = geom[-2], geom[-1]
    else:
        p1, p2 = geom[1], geom[0]
    return math.atan2(p2[1] - p1[1], p2[0] - p1[0])


def _infer_junction_priority_and_signals(
    nodes: list[dict[str, Any]],
    links: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    incident: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for lk in links:
        incident[lk["from"]].append(lk)
        incident[lk["to"]].append(lk)

    priorities: list[dict[str, Any]] = []
    signal_plans: list[dict[str, Any]] = []

    for node in nodes:
        nid = node["id"]
        inc = incident.get(nid, [])
        if len(inc) < 2:
            continue
        is_junction = node["kind"] in ("junction", "roundabout", "mini_roundabout") or node.get(
            "trafficSignals"
        )
        if not is_junction:
            continue

        # Determine majorLinks via roadClass / name / ref / roundabout
        rbt_links = [lk["id"] for lk in inc if lk.get("junctionRoundabout")]
        if node["kind"] in ("roundabout", "mini_roundabout") or rbt_links:
            controlled_by = "roundabout"
            major_links = sorted(rbt_links) if rbt_links else [lk["id"] for lk in inc]
            conf = 0.9
            notes = "roundabout circulating priority"
        else:
            max_rank = max(ROAD_CLASS_RANK.get(lk["roadClass"], 1) for lk in inc)
            top_links = [lk for lk in inc if ROAD_CLASS_RANK.get(lk["roadClass"], 1) == max_rank]
            # Prefer name/ref continuity when multiple classes or >=3 links meet
            by_name: dict[str, list[dict[str, Any]]] = defaultdict(list)
            for lk in inc:
                key = lk.get("ref") or lk.get("name")
                if key:
                    by_name[key].append(lk)
            best_named = max(by_name.values(), key=len, default=[])

            if len(top_links) < len(inc):
                major_links = sorted(lk["id"] for lk in top_links)
                class_conf, class_note = 0.65, "inferred from roadClass hierarchy"
            elif len(best_named) >= 2 and len(best_named) < len(inc):
                major_links = sorted(lk["id"] for lk in best_named)
                class_conf, class_note = 0.7, "inferred from name/ref continuity"
            else:
                major_links = sorted(lk["id"] for lk in inc)
                class_conf, class_note = 0.4, "equal roadClass; no clear major street"

            if node.get("trafficSignals"):
                controlled_by = "signals"
                conf = max(0.75, class_conf)
                notes = f"traffic_signals node; {class_note}"
            elif node.get("stop"):
                controlled_by = "stop"
                conf = 0.85
                notes = f"stop node; {class_note}"
            elif node.get("giveWay"):
                controlled_by = "give_way"
                conf = 0.85
                notes = f"give_way node; {class_note}"
            else:
                controlled_by = "uncontrolled"
                conf = class_conf
                notes = class_note

        priorities.append(
            {
                "nodeId": nid,
                "majorLinks": major_links,
                "controlledBy": controlled_by,
                "confidence": conf,
                "confidenceNotes": notes,
            }
        )

        if node.get("trafficSignals") and len(inc) >= 2:
            # Pair opposite approaches by approach angle (mod pi)
            sorted_inc = sorted(inc, key=lambda lk: _approach_angle(lk, nid))
            stage1: list[str] = []
            stage2: list[str] = []
            ref_angle = _approach_angle(sorted_inc[0], nid)
            for lk in sorted_inc:
                diff = abs((_approach_angle(lk, nid) - ref_angle + math.pi) % math.pi)
                if diff < math.pi / 4 or diff > 3 * math.pi / 4:
                    stage1.append(lk["id"])
                else:
                    stage2.append(lk["id"])
            stages = [sorted(stage1)]
            green_s: list[float] = [18]
            if stage2:
                stages.append(sorted(stage2))
                green_s.append(14)
            signal_plans.append(
                {
                    "nodeId": nid,
                    "stages": stages,
                    "greenS": green_s,
                    "amberS": 3,
                    "allRedS": 2,
                    "redAmberS": 2,
                    "confidence": 0.45,
                    "confidenceNotes": "inferred opposing-approach stages and default timings",
                }
            )

    priorities.sort(key=lambda p: p["nodeId"])
    signal_plans.sort(key=lambda s: s["nodeId"])
    return (priorities, signal_plans)


def build_roadpack(
    extracted: ExtractedOsm,
    config: dict[str, Any],
    extract_timestamp: str,
    speed_defaults: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Convert ExtractedOsm + city config into a validated RoadPack v0 dict."""
    defaults = speed_defaults or load_speed_defaults()
    origin_lat = float(config["origin"]["lat"])
    origin_lon = float(config["origin"]["lon"])
    proj = LocalProjection(origin_lat, origin_lon)

    # Count how many kept ways reference each node
    way_refs: Counter[int] = Counter()
    roundabout_nodes: set[int] = set()
    for w in extracted.ways:
        # Count unique nodes per way (except closed loops where endpoints meet)
        for nid in set(w.node_ids):
            way_refs[nid] += 1
        if w.tags.get("junction") == "roundabout":
            roundabout_nodes.update(w.node_ids)

    # Determine split nodes: endpoints, junctions, crossings, signals, mini_roundabouts
    split_nodes: set[int] = set()
    for w in extracted.ways:
        split_nodes.add(w.node_ids[0])
        split_nodes.add(w.node_ids[-1])
        for nid in w.node_ids[1:-1]:
            if way_refs[nid] >= 2:
                split_nodes.add(nid)
                continue
            osm_node = extracted.nodes.get(nid)
            if osm_node and (
                _classify_crossing(osm_node) is not None
                or osm_node.tags.get("highway")
                in ("traffic_signals", "mini_roundabout", "give_way", "stop")
            ):
                split_nodes.add(nid)

    links: list[dict[str, Any]] = []
    used_node_ids: set[int] = set()
    node_degree: Counter[int] = Counter()

    for w in extracted.ways:
        valid_nids = [nid for nid in w.node_ids if nid in extracted.nodes]
        if len(valid_nids) < 2:
            continue

        # Split valid_nids at internal split_nodes
        segments: list[list[int]] = []
        current: list[int] = [valid_nids[0]]
        for nid in valid_nids[1:-1]:
            current.append(nid)
            if nid in split_nodes:
                segments.append(current)
                current = [nid]
        current.append(valid_nids[-1])
        segments.append(current)

        oneway = _is_oneway(w)
        speed_mph, speed_source = parse_speed_limit_mph(w, config["region"], defaults)
        lanes_fwd, lanes_bwd, lanes_explicit = _parse_lanes(w, oneway)
        raw_tl_fwd = w.tags.get("turn:lanes:forward") or (
            w.tags.get("turn:lanes") if oneway else None
        )
        tl_fwd = _parse_turn_lanes(raw_tl_fwd)
        tl_bwd = _parse_turn_lanes(w.tags.get("turn:lanes:backward")) if not oneway else None
        bus_lane = _parse_bus_lane(w, oneway)
        cycle_lane = _parse_cycle_lane(w, oneway)
        conf, conf_notes = _score_link_confidence(
            lanes_explicit, bool(tl_fwd or tl_bwd), speed_source
        )

        for idx, seg in enumerate(segments):
            if len(seg) < 2:
                continue
            from_id = seg[0]
            to_id = seg[-1]
            geom: list[list[float]] = []
            for nid in seg:
                n = extracted.nodes[nid]
                x, z = proj.to_local(n.lat, n.lon)
                if not geom or geom[-1] != [x, z]:
                    geom.append([x, z])
            if len(geom) < 2:
                # Preserve distinct endpoints if projection rounded to same point
                n_end = extracted.nodes[to_id]
                x_e, z_e = proj.to_local(n_end.lat, n_end.lon)
                geom.append([x_e, z_e])

            link_id = f"l_{w.osm_id}_{idx}"
            used_node_ids.add(from_id)
            used_node_ids.add(to_id)
            node_degree[from_id] += 1
            node_degree[to_id] += 1

            link_obj: dict[str, Any] = {
                "id": link_id,
                "from": f"n_{from_id}",
                "to": f"n_{to_id}",
                "roadClass": w.road_class,
                "oneway": oneway,
                "geometry": geom,
                "speedLimitMph": speed_mph,
                "speedLimitSource": speed_source,
                "lanesForward": lanes_fwd,
                "lanesBackward": lanes_bwd,
                "confidence": conf,
                "confidenceNotes": conf_notes,
                "osmWayId": w.osm_id,
            }
            if w.tags.get("name"):
                link_obj["name"] = w.tags["name"]
            if w.tags.get("ref"):
                link_obj["ref"] = w.tags["ref"]
            if w.tags.get("junction") == "roundabout":
                link_obj["junctionRoundabout"] = True
            if tl_fwd and len(tl_fwd) == lanes_fwd:
                link_obj["turnLanesForward"] = tl_fwd
            if tl_bwd and len(tl_bwd) == lanes_bwd:
                link_obj["turnLanesBackward"] = tl_bwd
            if bus_lane:
                link_obj["busLane"] = bus_lane
            if cycle_lane:
                link_obj["cycleLane"] = cycle_lane
            links.append(link_obj)

    # Build nodes and crossings
    node_to_link: dict[int, str] = {}
    for link in links:
        f_nid = int(link["from"].removeprefix("n_"))
        t_nid = int(link["to"].removeprefix("n_"))
        node_to_link.setdefault(f_nid, link["id"])
        node_to_link.setdefault(t_nid, link["id"])

    nodes: list[dict[str, Any]] = []
    crossings: list[dict[str, Any]] = []

    for nid in sorted(used_node_ids):
        osm_node = extracted.nodes[nid]
        x, z = proj.to_local(osm_node.lat, osm_node.lon)
        hw_tag = osm_node.tags.get("highway", "")
        crossing_info = _classify_crossing(osm_node)

        if hw_tag == "mini_roundabout":
            kind = "mini_roundabout"
        elif nid in roundabout_nodes:
            kind = "roundabout"
        elif way_refs[nid] >= 2 or node_degree[nid] >= 3:
            kind = "junction"
        elif crossing_info is not None:
            kind = "crossing"
        else:
            kind = "end"

        node_obj: dict[str, Any] = {
            "id": f"n_{nid}",
            "x": x,
            "z": z,
            "kind": kind,
            "osmId": nid,
        }
        if hw_tag == "traffic_signals" or osm_node.tags.get("traffic_signals"):
            node_obj["trafficSignals"] = True
        if hw_tag == "give_way" or osm_node.tags.get("give_way"):
            node_obj["giveWay"] = True
        if hw_tag == "stop" or osm_node.tags.get("stop"):
            node_obj["stop"] = True
        nodes.append(node_obj)

        if crossing_info is not None and nid in node_to_link:
            ctype, cconf = crossing_info
            crossings.append(
                {
                    "id": f"x_{nid}",
                    "x": x,
                    "z": z,
                    "type": ctype,
                    "linkId": node_to_link[nid],
                    "confidence": cconf,
                    "osmId": nid,
                }
            )

    links.sort(key=lambda item: item["id"])
    crossings.sort(key=lambda item: item["id"])

    # Resolve turn restrictions to (fromLink, viaNode, toLink)
    restrictions: list[dict[str, Any]] = []
    for r in extracted.restrictions:
        via_id = f"n_{r.via_node_id}"
        from_candidates = [
            lk["id"]
            for lk in links
            if lk.get("osmWayId") == r.from_way_id and (lk["to"] == via_id or lk["from"] == via_id)
        ]
        to_candidates = [
            lk["id"]
            for lk in links
            if lk.get("osmWayId") == r.to_way_id and (lk["from"] == via_id or lk["to"] == via_id)
        ]
        if from_candidates and to_candidates:
            restrictions.append(
                {
                    "id": f"r_{r.osm_id}",
                    "type": r.restriction_type,
                    "fromLink": from_candidates[0],
                    "viaNode": via_id,
                    "toLink": to_candidates[0],
                    "confidence": 0.9,
                    "osmRelationId": r.osm_id,
                }
            )
    restrictions.sort(key=lambda item: item["id"])
    junction_priority, signal_plans = _infer_junction_priority_and_signals(nodes, links)

    excluded_list = [
        {
            "osmWayId": e.osm_way_id,
            **({"name": e.name} if e.name else {}),
            "reason": e.reason,
        }
        for e in sorted(extracted.excluded, key=lambda e: e.osm_way_id)
    ]

    pack: dict[str, Any] = {
        "packFormatVersion": "0.2",
        "packId": config["pack_id"],
        "displayName": config["display_name"],
        "country": config["country"],
        "region": config["region"],
        "drivingSide": config["driving_side"],
        "origin": {"lat": origin_lat, "lon": origin_lon},
        "source": {
            "kind": "osm",
            "extract": config["extract"]["name"],
            "extractTimestamp": extract_timestamp,
            "bbox": [float(v) for v in config["bbox"]],
            "licence": "ODbL-1.0",
            "attribution": "© OpenStreetMap contributors",
            "generator": f"ltl_pipeline/{__version__}",
        },
        "nodes": nodes,
        "links": links,
        "crossings": crossings,
        "zones": [],
        "restrictions": restrictions,
        "junctionPriority": junction_priority,
        "signalPlans": signal_plans,
        "excluded": excluded_list,
    }
    validate_roadpack(pack)
    return pack


def write_roadpack(pack: dict[str, Any], out_path: Path) -> None:
    """Write a RoadPack dict as deterministic pretty-printed JSON."""
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(pack, indent=2) + "\n", encoding="utf-8")
