"""Extract Wolverhampton POIs from OSM PBF (including ~500 m buffer) and map accessLinkId."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import osmium
import yaml
from shapely.geometry import LineString, Point
from shapely.strtree import STRtree

from ltl_pipeline import __version__
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.geo import LocalProjection
from ltl_pipeline.schema import REPO_ROOT, validate_pois_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"
CONFIG_PATH = CITY_DIR / "config.yaml"
ROADPACK_PATH = CITY_DIR / "roadpack.json"
OUT_PATH = CITY_DIR / "pois.json"

DEFAULT_NAME_BY_KIND = {
    "railway_station": "Railway Station",
    "bus_station": "Bus Station",
    "school": "School",
    "hospital": "Hospital",
    "car_park": "Car Park",
    "shopping": "Shopping Centre",
    "university": "University",
    "police": "Police Station",
}


def _classify_poi(tags: osmium.osm.TagList) -> str | None:
    if tags.get("railway") == "station":
        return "railway_station"
    am = tags.get("amenity")
    if am == "bus_station":
        return "bus_station"
    if am == "school":
        return "school"
    if am == "hospital":
        return "hospital"
    if am == "parking":
        return "car_park"
    if am == "university":
        return "university"
    if am == "police":
        return "police"
    if tags.get("shop") == "mall":
        return "shopping"
    return None


class _PoiHandler(osmium.SimpleHandler):
    def __init__(self, expanded_bbox: tuple[float, float, float, float]) -> None:
        super().__init__()
        self.bbox = expanded_bbox
        self.items: list[tuple[int, str, str, float, float]] = []

    def _in_box(self, lat: float, lon: float) -> bool:
        min_lon, min_lat, max_lon, max_lat = self.bbox
        return min_lon <= lon <= max_lon and min_lat <= lat <= max_lat

    def node(self, n: osmium.osm.Node) -> None:
        if not n.location.valid():
            return
        lat, lon = float(n.location.lat), float(n.location.lon)
        if not self._in_box(lat, lon):
            return
        kind = _classify_poi(n.tags)
        if not kind:
            return
        name = n.tags.get("name") or DEFAULT_NAME_BY_KIND.get(kind, "Destination")
        self.items.append((int(n.id), kind, name, lat, lon))

    def way(self, w: osmium.osm.Way) -> None:
        kind = _classify_poi(w.tags)
        if not kind:
            return
        lats: list[float] = []
        lons: list[float] = []
        for n in w.nodes:
            if n.location.valid():
                lats.append(float(n.location.lat))
                lons.append(float(n.location.lon))
        if not lats:
            return
        clat = sum(lats) / len(lats)
        clon = sum(lons) / len(lons)
        if not self._in_box(clat, clon):
            return
        name = w.tags.get("name") or DEFAULT_NAME_BY_KIND.get(kind, "Destination")
        self.items.append((int(w.id), kind, name, clat, clon))


def build_pois(
    city_dir: Path = CITY_DIR,
) -> dict[str, Any]:
    """Extract POIs from cached PBF and link each to nearest main-network RoadPack link."""
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))
    pack = json.loads((city_dir / "roadpack.json").read_text(encoding="utf-8"))

    extract_cfg = config["extract"]
    fetch_res = fetch_extract(pbf_url=extract_cfg["pbf_url"], md5_url=extract_cfg["md5_url"])

    min_lon, min_lat, max_lon, max_lat = [float(v) for v in config["bbox"]]
    # Expand bbox by ~500 m (0.0075 deg lon, 0.0045 deg lat at 52.59N)
    exp_bbox = (min_lon - 0.0075, min_lat - 0.0045, max_lon + 0.0075, max_lat + 0.0045)

    handler = _PoiHandler(expanded_bbox=exp_bbox)
    handler.apply_file(str(fetch_res.pbf_path), locations=True)

    origin_lat = float(config["origin"]["lat"])
    origin_lon = float(config["origin"]["lon"])
    proj = LocalProjection(origin_lat, origin_lon)

    main_links = [lk for lk in pack["links"] if lk.get("inMainNetwork")]
    link_lines = [LineString(lk["geometry"]) for lk in main_links]
    link_tree = STRtree(link_lines)

    pois: list[dict[str, Any]] = []
    seen_keys: set[tuple[str, str]] = set()

    for osm_id, kind, name, lat, lon in sorted(handler.items, key=lambda item: (item[1], item[0])):
        # Deduplicate unnamed car parks that are very close or identical named POIs
        dedup_key = (kind, name.strip().lower())
        if kind != "car_park" and dedup_key in seen_keys:
            continue
        seen_keys.add(dedup_key)

        x, z = proj.to_local(lat, lon)
        outside = not (min_lon <= lon <= max_lon and min_lat <= lat <= max_lat)
        if kind == "car_park" and outside:
            continue

        pt = Point(x, z)
        nearest_idx = link_tree.nearest(pt)
        access_link = main_links[nearest_idx]["id"]

        entry: dict[str, Any] = {
            "id": f"poi_{kind}_{osm_id}",
            "name": name,
            "kind": kind,
            "x": x,
            "z": z,
            "accessLinkId": access_link,
            "osmId": osm_id,
        }
        if outside:
            entry["outsideArea"] = True
        pois.append(entry)

    # Add Queen Square as city_centre anchor
    qs_lat, qs_lon = 52.5863, -2.1282
    qs_x, qs_z = proj.to_local(qs_lat, qs_lon)
    qs_nearest = link_tree.nearest(Point(qs_x, qs_z))
    pois.append(
        {
            "id": "poi_city_centre_queen_square",
            "name": "City Centre (Queen Square)",
            "kind": "city_centre",
            "x": qs_x,
            "z": qs_z,
            "accessLinkId": main_links[qs_nearest]["id"],
            "osmId": 0,
            "reason": "Queen Square is Wolverhampton's civic centre used on local direction signs",
        }
    )

    pois.sort(key=lambda p: (p["kind"], p["id"]))
    return {
        "poisFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": {"lat": origin_lat, "lon": origin_lon},
        "source": {
            "kind": "osm",
            "extract": config["extract"]["name"],
            "extractTimestamp": fetch_res.extract_timestamp,
            "bbox": [float(v) for v in config["bbox"]],
            "licence": "ODbL-1.0",
            "attribution": "© OpenStreetMap contributors",
            "generator": f"ltl_pipeline/{__version__}",
        },
        "pois": pois,
    }


def write_pois(out_path: Path = OUT_PATH) -> Path:
    """Generate and validate content/cities/wolverhampton/pois.json."""
    data = build_pois()
    out_path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    validate_pois_file(out_path)
    return out_path


if __name__ == "__main__":
    written = write_pois()
    print(f"Wrote {written}")
