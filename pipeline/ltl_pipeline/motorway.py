"""Build content/cities/m54-j2/ RoadPack v0.7 with motorway slip roads, gantries, and EAs."""

from __future__ import annotations

import math
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.build import build_roadpack, write_roadpack
from ltl_pipeline.extract import extract_osm
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.schema import REPO_ROOT, validate_roadpack

CITY_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"


def _heading_at_mid(geom: list[list[float]]) -> tuple[float, float, float]:
    mid_i = len(geom) // 2
    i0 = max(0, mid_i - 1)
    i1 = min(len(geom) - 1, i0 + 1)
    x0, z0 = geom[i0]
    x1, z1 = geom[i1]
    dx, dz = x1 - x0, z1 - z0
    h = round(math.degrees(math.atan2(dx, dz)) % 360.0, 2)
    return round((x0 + x1) / 2.0, 3), round((z0 + z1) / 2.0, 3), h


def build_m54_pack(city_dir: Path = CITY_DIR) -> dict[str, Any]:
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))
    fetch_res = fetch_extract(config["extract"]["pbf_url"], config["extract"]["md5_url"])
    extracted = extract_osm(
        fetch_res.pbf_path,
        bbox=tuple(float(v) for v in config["bbox"]),
        exclude_tram=True,
    )
    pack = build_roadpack(extracted, config, fetch_res.extract_timestamp)
    pack["packFormatVersion"] = "0.7"

    # Identify slip-road links (motorway links without ref or connecting motorway to A449)
    # and annotate hardShoulder, junctionNumber, gantries[], and emergencyAreas[]
    gantries: list[dict[str, Any]] = []
    emergency_areas: list[dict[str, Any]] = []
    mwy_mainline_count = 0
    slip_count = 0

    for lk in pack["links"]:
        if lk["roadClass"] != "motorway":
            continue
        is_mainline = bool(lk.get("ref") == "M54" and lk.get("lanesForward", 1) >= 2)
        if is_mainline:
            lk["hardShoulder"] = True
            mwy_mainline_count += 1
            mx, mz, mh = _heading_at_mid(lk["geometry"])
            if mwy_mainline_count % 2 == 1:
                gantries.append(
                    {
                        "id": f"gantry_{len(gantries) + 1:03d}",
                        "x": mx,
                        "z": mz,
                        "heading": mh,
                        "linkId": lk["id"],
                        "lanes": max(2, int(lk.get("lanesForward", 2))),
                    }
                )
            if len(emergency_areas) < 2 and mwy_mainline_count in (2, 4):
                # Place emergency area on nearside (left) of M54 mainline
                rad = math.radians(mh)
                dx, dz = math.sin(rad), math.cos(rad)
                nx, nz = -dz, dx
                offset = max(4.0, float(lk.get("widthM", 10.0)) / 2.0 + 2.5)
                emergency_areas.append(
                    {
                        "id": f"ea_{len(emergency_areas) + 1:03d}",
                        "x": round(mx + nx * offset, 3),
                        "z": round(mz + nz * offset, 3),
                        "heading": mh,
                        "linkId": lk["id"],
                    }
                )
        else:
            lk["hardShoulder"] = False
            lk["junctionNumber"] = "2"
            slip_count += 1

    pack["gantries"] = gantries
    pack["emergencyAreas"] = emergency_areas
    validate_roadpack(pack)

    out_path = city_dir / "roadpack.json"
    write_roadpack(pack, out_path)

    qa = f"""# M54 Junction 2 (Coven Heath Interchange) — RoadPack QA

- **Pack ID:** `{pack['packId']}` (`packFormatVersion` `0.7`)
- **Junction:** M54 Junction 2 (`highway=motorway_junction`, `ref=2`, Coven Heath Interchange)
- **Reason chosen:** Closest motorway junction north of Wolverhampton (A449 interchange).
- **Mainline links (`hardShoulder: true`):** {mwy_mainline_count}
- **Slip-road links (`junctionNumber: "2"`):** {slip_count}
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
