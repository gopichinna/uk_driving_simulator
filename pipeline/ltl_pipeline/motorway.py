"""Build content/cities/m54-j2/ RoadPack v0.7: continuous M54 corridor (>= 4 km) + slip roads."""

from __future__ import annotations

import math
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.build import build_roadpack, write_roadpack
from ltl_pipeline.extract import ExtractedOsm, extract_osm
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.geo import LocalProjection
from ltl_pipeline.schema import REPO_ROOT, validate_roadpack

CITY_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"


def _polyline_len(geom: list[list[float]]) -> float:
    return sum(
        math.hypot(geom[i + 1][0] - geom[i][0], geom[i + 1][1] - geom[i][1])
        for i in range(len(geom) - 1)
    )


def _point_at_dist(geom: list[list[float]], dist_m: float) -> tuple[float, float, float]:
    acc = 0.0
    for i in range(len(geom) - 1):
        x0, z0 = geom[i]
        x1, z1 = geom[i + 1]
        seg = math.hypot(x1 - x0, z1 - z0)
        if acc + seg >= dist_m or i == len(geom) - 2:
            t = min(1.0, max(0.0, (dist_m - acc) / max(seg, 1e-6)))
            x = round(x0 + t * (x1 - x0), 3)
            z = round(z0 + t * (z1 - z0), 3)
            h = round(math.degrees(math.atan2(x1 - x0, z1 - z0)) % 360.0, 2)
            return x, z, h
        acc += seg
    return geom[0][0], geom[0][1], 0.0


def _filter_motorway_corridor(
    extracted: ExtractedOsm, origin_lat: float, origin_lon: float
) -> ExtractedOsm:
    """Keep motorway + motorway_link, and non-service local roads within 300 m of junctions."""
    proj = LocalProjection(origin_lat, origin_lon)
    slip_pts: list[tuple[float, float]] = []
    for w in extracted.ways:
        if w.tags.get("highway") == "motorway_link":
            for nid in w.node_ids:
                n = extracted.nodes.get(nid)
                if n is not None:
                    slip_pts.append(proj.to_local(n.lat, n.lon))

    kept_ways = []
    needed_nodes: set[int] = set()
    for w in extracted.ways:
        hw = w.tags.get("highway", "")
        if hw == "service":
            continue
        if hw in ("motorway", "motorway_link"):
            kept_ways.append(w)
            needed_nodes.update(w.node_ids)
            continue
        # Keep local roads only within 300 m of slip-road nodes
        near_jct = False
        for nid in w.node_ids:
            n = extracted.nodes.get(nid)
            if n is None:
                continue
            x, z = proj.to_local(n.lat, n.lon)
            if any(math.hypot(x - sx, z - sz) <= 300.0 for sx, sz in slip_pts):
                near_jct = True
                break
        if near_jct:
            kept_ways.append(w)
            needed_nodes.update(w.node_ids)

    return ExtractedOsm(
        nodes={nid: n for nid, n in extracted.nodes.items() if nid in needed_nodes},
        ways=kept_ways,
        restrictions=[
            r
            for r in extracted.restrictions
            if r.via_node_id in needed_nodes
        ],
        excluded=extracted.excluded,
    )


def build_m54_pack(city_dir: Path = CITY_DIR) -> dict[str, Any]:
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))
    fetch_res = fetch_extract(config["extract"]["pbf_url"], config["extract"]["md5_url"])
    raw = extract_osm(
        fetch_res.pbf_path,
        bbox=tuple(float(v) for v in config["bbox"]),
        exclude_tram=True,
    )
    filtered = _filter_motorway_corridor(
        raw, float(config["origin"]["lat"]), float(config["origin"]["lon"])
    )
    pack = build_roadpack(filtered, config, fetch_res.extract_timestamp)
    pack["packFormatVersion"] = "0.7"

    gantries: list[dict[str, Any]] = []
    emergency_areas: list[dict[str, Any]] = []
    eb_len_m = 0.0
    wb_len_m = 0.0
    slip_count = 0

    for lk in pack["links"]:
        if lk["roadClass"] != "motorway":
            continue
        is_mainline = bool(
            (lk.get("ref") or "") in ("M54", "M6") and lk.get("lanesForward", 1) >= 2
        )
        if is_mainline:
            lk["hardShoulder"] = True
            length = _polyline_len(lk["geometry"])
            dx_total = lk["geometry"][-1][0] - lk["geometry"][0][0]
            dz_total = lk["geometry"][-1][1] - lk["geometry"][0][1]
            # Group by primary direction (east/south vs west/north)
            if (dx_total - dz_total) >= 0:
                eb_len_m += length
            else:
                wb_len_m += length

            # Place gantries roughly every 500 m along mainline
            d = 250.0
            while d < length:
                gx, gz, gh = _point_at_dist(lk["geometry"], d)
                gantries.append(
                    {
                        "id": f"gantry_{len(gantries) + 1:03d}",
                        "x": gx,
                        "z": gz,
                        "heading": gh,
                        "linkId": lk["id"],
                        "lanes": max(2, int(lk.get("lanesForward", 2))),
                    }
                )
                d += 500.0

            if len(emergency_areas) < 4 and length >= 400.0:
                ex, ez, eh = _point_at_dist(lk["geometry"], length * 0.5)
                rad = math.radians(eh)
                nx, nz = -math.cos(rad), math.sin(rad)
                offset = max(4.0, float(lk.get("widthM", 10.0)) / 2.0 + 2.5)
                emergency_areas.append(
                    {
                        "id": f"ea_{len(emergency_areas) + 1:03d}",
                        "x": round(ex + nx * offset, 3),
                        "z": round(ez + nz * offset, 3),
                        "heading": eh,
                        "linkId": lk["id"],
                    }
                )
        else:
            lk["hardShoulder"] = False
            lk["junctionNumber"] = "2" if lk["geometry"][0][0] < 0 else "1"
            slip_count += 1

    pack["gantries"] = gantries
    pack["emergencyAreas"] = emergency_areas
    validate_roadpack(pack)

    out_path = city_dir / "roadpack.json"
    write_roadpack(pack, out_path)

    qa = f"""# M54 (J1–J2 Coven Heath) — RoadPack QA

- **Pack ID:** `{pack['packId']}` (`packFormatVersion` `0.7`)
- **Section:** M54 J1 (Essington / M6 J10A) through J2 (Coven Heath Interchange)
- **Eastbound carriageway motorway length:** {eb_len_m:.1f} m ({eb_len_m / 1000.0:.2f} km)
- **Westbound carriageway motorway length:** {wb_len_m:.1f} m ({wb_len_m / 1000.0:.2f} km)
- **Slip-road links:** {slip_count}
- **Service roads:** 0 (excluded)
- **Gantries:** {len(gantries)}
- **Emergency areas:** {len(emergency_areas)}
- **Total nodes:** {len(pack['nodes'])}, **Total links:** {len(pack['links'])}

## Attribution
© OpenStreetMap contributors (ODbL-1.0).
"""
    (city_dir / "QA.md").write_text(qa, encoding="utf-8")
    return pack


if __name__ == "__main__":
    p = build_m54_pack()
    print(
        f"Built m54-j2: {len(p['links'])} links, "
        f"{len(p['gantries'])} gantries, {len(p['emergencyAreas'])} EAs"
    )
