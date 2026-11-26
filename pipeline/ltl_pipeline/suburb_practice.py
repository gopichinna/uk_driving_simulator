"""Build content/cities/suburb_practice/ RoadPack v0.7, POIs, street furniture, and SOURCE.md."""

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

CITY_DIR = REPO_ROOT / "content" / "cities" / "suburb_practice"


def build_suburb_practice(city_dir: Path = CITY_DIR) -> dict[str, Any]:
    """Build suburb_practice RoadPack v0.7 + buildings + POIs + signs + furniture + SOURCE.md."""
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))
    fetch_res = fetch_extract(config["extract"]["pbf_url"], config["extract"]["md5_url"])
    extracted = extract_osm(
        fetch_res.pbf_path,
        bbox=tuple(float(v) for v in config["bbox"]),
        exclude_tram=True,
    )
    pack = build_roadpack(extracted, config, fetch_res.extract_timestamp)

    # Ensure at least one 40 mph distributor segment exists if OSM omits maxspeed on A41
    hand_fixed_links: list[str] = []
    has_40 = any(lk.get("speedLimitMph") == 40 for lk in pack["links"])
    if not has_40:
        for lk in pack["links"]:
            if lk.get("roadClass") in ("primary", "secondary") and lk.get("ref", "").startswith(
                "A41"
            ):
                lk["speedLimitMph"] = 40
                lk["confidence"] = "low"
                hand_fixed_links.append(lk["id"])
        if not hand_fixed_links:
            for lk in pack["links"]:
                if lk.get("roadClass") in ("primary", "secondary"):
                    lk["speedLimitMph"] = 40
                    lk["confidence"] = "low"
                    hand_fixed_links.append(lk["id"])
                    if len(hand_fixed_links) >= 4:
                        break

    # Add start / join anchor metadata in anchors.json (roadpack.v0 stays strict)
    res_links = [lk for lk in pack["links"] if lk.get("speedLimitMph") == 30]
    dist_links = [lk for lk in pack["links"] if lk.get("speedLimitMph") == 40]
    start_lk = res_links[0] if res_links else pack["links"][0]
    join_lk = dist_links[0] if dist_links else pack["links"][min(1, len(pack["links"]) - 1)]
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
            "# `suburb_practice` — Source & Hand-Fix Record",
            "",
            "- **Location:** Tettenhall & Claregate, Wolverhampton, West Midlands (UK)",
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
                    f"- `{lid}`: set `speedLimitMph: 40` on distributor road"
                    for lid in hand_fixed_links
                ]
                or ["- None required (all distributor speeds present in OSM)."]
            ),
            "",
        ]
    )
    (city_dir / "SOURCE.md").write_text(source_md, encoding="utf-8")
    return pack


if __name__ == "__main__":
    p = build_suburb_practice()
    print(f"Built suburb_practice: {len(p['nodes'])} nodes, {len(p['links'])} links")
