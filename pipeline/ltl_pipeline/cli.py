"""CLI entry point for building city RoadPacks and country RulePacks."""

from __future__ import annotations

import argparse
from pathlib import Path

import yaml

from ltl_pipeline.build import build_roadpack, write_roadpack
from ltl_pipeline.extract import extract_osm
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.schema import REPO_ROOT


def build_city(city: str, force_fetch: bool = False) -> Path:
    """Fetch, extract, and build RoadPack v0 for a configured city."""
    city_dir = REPO_ROOT / "content" / "cities" / city
    config_path = city_dir / "config.yaml"
    config = yaml.safe_load(config_path.read_text(encoding="utf-8"))

    extract_cfg = config["extract"]
    fetch_res = fetch_extract(
        pbf_url=extract_cfg["pbf_url"],
        md5_url=extract_cfg["md5_url"],
        force=force_fetch,
    )

    bbox = tuple(float(v) for v in config["bbox"])
    exclude_cfg = config.get("exclude", {})
    exclude_tram = "tram" in exclude_cfg.get("railway_types", [])
    excluded_ids = set(exclude_cfg.get("way_ids", []))

    extracted = extract_osm(
        osm_path=fetch_res.pbf_path,
        bbox=(bbox[0], bbox[1], bbox[2], bbox[3]),
        exclude_tram=exclude_tram,
        excluded_way_ids=excluded_ids,
    )
    pack = build_roadpack(
        extracted=extracted,
        config=config,
        extract_timestamp=fetch_res.extract_timestamp,
    )
    out_path = city_dir / "roadpack.json"
    write_roadpack(pack, out_path)
    return out_path


def main() -> None:
    parser = argparse.ArgumentParser(prog="ltl_pipeline")
    sub = parser.add_subparsers(dest="command", required=True)

    city_p = sub.add_parser("build-city", help="Build roadpack.json for a city")
    city_p.add_argument("city", help="City directory under content/cities/ (e.g. wolverhampton)")
    city_p.add_argument("--force-fetch", action="store_true")

    args = parser.parse_args()
    if args.command == "build-city":
        out = build_city(args.city, force_fetch=args.force_fetch)
        print(f"Wrote {out}")


if __name__ == "__main__":
    main()
