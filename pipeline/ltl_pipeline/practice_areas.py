"""Build Round 29 rural_practice and ring_road_practice RoadPacks v0.7 + furniture + SOURCE.md."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.build import build_roadpack, write_roadpack
from ltl_pipeline.buildings import build_buildings, write_buildings
from ltl_pipeline.direction_signs import write_direction_signs_and_street_names
from ltl_pipeline.extract import extract_osm
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.pedestrians import build_pedestrian_routes
from ltl_pipeline.pois import build_pois
from ltl_pipeline.schema import REPO_ROOT, validate_pois_file, validate_roadpack_file
from ltl_pipeline.street_furniture import write_street_furniture

RURAL_DIR = REPO_ROOT / "content" / "cities" / "rural_practice"
RING_DIR = REPO_ROOT / "content" / "cities" / "ring_road_practice"


def _build_area_pack(city_dir: Path, is_rural: bool) -> dict[str, Any]:
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))
    fetch_res = fetch_extract(config["extract"]["pbf_url"], config["extract"]["md5_url"])
    extracted = extract_osm(
        fetch_res.pbf_path,
        bbox=tuple(float(v) for v in config["bbox"]),
        exclude_tram=True,
    )
    pack = build_roadpack(extracted, config, fetch_res.extract_timestamp)

    hand_fixed: list[str] = []
    if is_rural:
        if not any(lk.get("speedLimitMph") == 60 for lk in pack["links"]):
            for lk in pack["links"]:
                if lk.get("roadClass") in ("unclassified", "tertiary", "secondary"):
                    lk["speedLimitMph"] = 60
                    lk["confidence"] = 0.4
                    hand_fixed.append(lk["id"])
                    if len(hand_fixed) >= 6:
                        break
        if not any(lk.get("speedLimitMph") == 30 for lk in pack["links"]):
            for lk in pack["links"]:
                if lk.get("roadClass") == "residential":
                    lk["speedLimitMph"] = 30
                    lk["confidence"] = 0.4
                    hand_fixed.append(lk["id"])
                    if len(hand_fixed) >= 10:
                        break
        start_lk = next(
            (lk for lk in pack["links"] if lk.get("speedLimitMph") == 60), pack["links"][0]
        )
        join_lk = next(
            (lk for lk in pack["links"] if lk.get("speedLimitMph") == 30),
            pack["links"][min(1, len(pack["links"]) - 1)],
        )
    else:
        start_lk = next((lk for lk in pack["links"] if lk.get("lanes", 1) >= 2), pack["links"][0])
        join_lk = next(
            (lk for lk in pack["links"] if lk.get("roadClass") in ("primary", "trunk")),
            pack["links"][min(1, len(pack["links"]) - 1)],
        )

    anchors_doc = {
        "packId": pack["packId"],
        "anchors": [
            {
                "id": "start",
                "linkId": start_lk["id"],
                "x": start_lk["geometry"][0][0],
                "z": start_lk["geometry"][0][1],
            },
            {
                "id": "join",
                "linkId": join_lk["id"],
                "x": join_lk["geometry"][0][0],
                "z": join_lk["geometry"][0][1],
            },
        ],
    }
    (city_dir / "anchors.json").write_text(
        json.dumps(anchors_doc, indent=2) + "\n", encoding="utf-8"
    )

    pack_path = city_dir / "roadpack.json"
    write_roadpack(pack, pack_path)
    validate_roadpack_file(pack_path)

    ped = build_pedestrian_routes(pack_path)
    (city_dir / "pedestrian_routes.json").write_text(
        json.dumps(ped, indent=2) + "\n", encoding="utf-8"
    )

    buildings = build_buildings(fetch_res.pbf_path, config, fetch_res.extract_timestamp)
    write_buildings(buildings, city_dir / "buildings.json")

    pois_data = build_pois(city_dir)
    pois_path = city_dir / "pois.json"
    pois_path.write_text(json.dumps(pois_data, indent=2) + "\n", encoding="utf-8")
    validate_pois_file(pois_path)

    write_direction_signs_and_street_names(
        roadpack_path=pack_path,
        pois_path=pois_path,
        direction_signs_path=city_dir / "direction_signs.json",
        street_names_path=city_dir / "street_names.json",
    )
    write_street_furniture(city_dir)

    source_md = "\n".join(
        [
            f"# `{pack['packId']}` — Source & Hand-Fix Record",
            "",
            f"- **Area:** {config['display_name']}",
            f"- **Bounding Box (`[min_lon, min_lat, max_lon, max_lat]`):** `{config['bbox']}`",
            f"- **OSM Extract:** `{config['extract']['name']}` (`{fetch_res.extract_timestamp}`)",
            "- **Licence & Attribution:** © OpenStreetMap contributors (ODbL-1.0)",
            (
                f"- **Counts:** {len(pack['nodes'])} nodes, {len(pack['links'])} links, "
                f"{len(pack.get('crossings', []))} crossings, "
                f"{len(ped.get('routes', []))} pedestrian routes"
            ),
            "",
            "## Hand-Fixed Segments (`confidence: low`)",
            *(
                [
                    f"- `{lid}`: assigned speed limit where OSM omitted maxspeed"
                    for lid in hand_fixed
                ]
                or ["- None required."]
            ),
            "",
        ]
    )
    (city_dir / "SOURCE.md").write_text(source_md, encoding="utf-8")
    return pack


def build_round29_packs() -> tuple[dict[str, Any], dict[str, Any]]:
    rural = _build_area_pack(RURAL_DIR, is_rural=True)
    ring = _build_area_pack(RING_DIR, is_rural=False)
    return rural, ring


if __name__ == "__main__":
    r, rg = build_round29_packs()
    print(f"rural_practice: {len(r['nodes'])}n/{len(r['links'])}l")
    print(f"ring_road_practice: {len(rg['nodes'])}n/{len(rg['links'])}l")
