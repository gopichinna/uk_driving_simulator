"""Extract and build simplified anticlockwise building footprints from OSM."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

import osmium
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient

from ltl_pipeline import __version__
from ltl_pipeline.geo import LocalProjection
from ltl_pipeline.schema import validate_buildings_file

DEFAULT_HEIGHT_BY_KIND: dict[str, float] = {
    "church": 14.0,
    "office": 12.0,
    "civic": 12.0,
    "shop": 8.0,
    "house": 7.0,
    "other": 8.0,
}

KIND_MAP: dict[str, str] = {
    "retail": "shop",
    "shop": "shop",
    "commercial": "office",
    "office": "office",
    "house": "house",
    "residential": "house",
    "terrace": "house",
    "detached": "house",
    "semidetached_house": "house",
    "apartments": "house",
    "church": "church",
    "cathedral": "church",
    "chapel": "church",
    "civic": "civic",
    "public": "civic",
    "university": "civic",
    "college": "civic",
    "school": "civic",
    "hospital": "civic",
    "train_station": "civic",
    "government": "civic",
}


class _BuildingHandler(osmium.SimpleHandler):
    def __init__(self, bbox: tuple[float, float, float, float]) -> None:
        super().__init__()
        self.bbox = bbox
        self.raw_buildings: list[tuple[int, list[tuple[float, float]], dict[str, str]]] = []

    def way(self, w: osmium.osm.Way) -> None:
        if "building" not in w.tags:
            return
        coords: list[tuple[float, float]] = []
        any_in = False
        min_lon, min_lat, max_lon, max_lat = self.bbox
        for n in w.nodes:
            if not n.location.valid():
                continue
            lat, lon = float(n.location.lat), float(n.location.lon)
            coords.append((lat, lon))
            if min_lon <= lon <= max_lon and min_lat <= lat <= max_lat:
                any_in = True
        if not any_in or len(coords) < 4:
            return
        tags = {
            k: w.tags[k]
            for k in ("building", "height", "building:levels", "name")
            if k in w.tags
        }
        self.raw_buildings.append((int(w.id), coords, tags))


def _parse_height(tags: dict[str, str], kind: str) -> tuple[float, str]:
    raw_h = tags.get("height", "").strip().lower()
    if raw_h:
        m = re.match(r"^(\d+(?:\.\d+)?)\s*m?$", raw_h)
        if m:
            h = float(m.group(1))
            if h >= 1.0:
                return (round(h, 2), "tagged")
    raw_lvl = tags.get("building:levels", "").strip()
    if raw_lvl:
        m = re.match(r"^(\d+(?:\.\d+)?)$", raw_lvl)
        if m:
            h = float(m.group(1)) * 3.0
            if h >= 1.0:
                return (round(h, 2), "levels")
    return (DEFAULT_HEIGHT_BY_KIND.get(kind, 8.0), "default")


def build_buildings(
    osm_path: Path,
    config: dict[str, Any],
    extract_timestamp: str,
) -> dict[str, Any]:
    """Extract buildings inside bbox, simplify to <=0.5m, orient CCW, and return dict."""
    bbox = tuple(float(v) for v in config["bbox"])
    handler = _BuildingHandler(bbox=(bbox[0], bbox[1], bbox[2], bbox[3]))
    handler.apply_file(str(osm_path), locations=True)

    origin_lat = float(config["origin"]["lat"])
    origin_lon = float(config["origin"]["lon"])
    proj = LocalProjection(origin_lat, origin_lon)

    buildings: list[dict[str, Any]] = []
    for osm_id, latlon_pts, tags in sorted(handler.raw_buildings, key=lambda x: x[0]):
        local_pts = [proj.to_local(lat, lon) for lat, lon in latlon_pts]
        poly = Polygon(local_pts)
        if not poly.is_valid:
            poly = poly.buffer(0)
        if poly.is_empty or poly.geom_type != "Polygon" or poly.area < 15.0:
            continue

        simplified = poly.simplify(0.5, preserve_topology=True)
        if simplified.is_empty or simplified.geom_type != "Polygon" or simplified.area < 15.0:
            continue
        ccw = orient(simplified, sign=1.0)
        ring = [[round(float(x), 2), round(float(z), 2)] for x, z in list(ccw.exterior.coords)[:-1]]
        if len(ring) < 3:
            continue

        b_tag = tags.get("building", "yes").strip().lower()
        kind = KIND_MAP.get(b_tag, "other")
        height_m, height_src = _parse_height(tags, kind)
        entry: dict[str, Any] = {
            "id": f"b_{osm_id}",
            "footprint": ring,
            "heightM": height_m,
            "heightSource": height_src,
            "kind": kind,
            "osmId": osm_id,
        }
        if tags.get("name"):
            entry["name"] = tags["name"]
        buildings.append(entry)

    return {
        "buildingsFormatVersion": "0.1",
        "packId": config["pack_id"],
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
        "buildings": buildings,
    }


def write_buildings(data: dict[str, Any], out_path: Path) -> Path:
    """Write buildings.json compactly (to stay well under 3 MB) and validate it."""
    out_path.write_text(json.dumps(data, separators=(",", ":")) + "\n", encoding="utf-8")
    validate_buildings_file(out_path)
    return out_path
