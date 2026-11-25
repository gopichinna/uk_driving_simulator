"""Generate speed_signs.json, bus_stops.json, and warning_signs.json for a city pack."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

import osmium
import yaml
from shapely.geometry import Point
from shapely.strtree import STRtree

from ltl_pipeline import __version__
from ltl_pipeline.direction_signs import (
    GOV_UK_KYTS_URL,
    GOV_UK_TRAFFIC_SIGNS_URL,
    RoadGraph,
    _heading_deg,
    _point_and_dir_from_end,
    _polyline_length,
)
from ltl_pipeline.fetch import fetch_extract
from ltl_pipeline.geo import LocalProjection
from ltl_pipeline.schema import REPO_ROOT, validate_signs_placed_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"

DIAGRAM_REFS = {
    "school": "Diagram 545 (Children going to or from school) + Diagram 546 (School plate)",
    "road_humps": "Diagram 557.1 (Road humps)",
    "road_narrows": "Diagram 516 / 517 (Road narrows)",
    "pedestrians_in_road": "Diagram 544 (Pedestrians in road ahead)",
    "queues_likely": "Diagram 584 (Traffic queues likely ahead)",
    "bend_ahead": "Diagram 512 (Bend ahead)",
}


def _place_on_verge(
    graph: RoadGraph,
    px: float,
    pz: float,
    dx: float,
    dz: float,
    width_m: float,
    side: int = 1,
) -> tuple[float, float] | None:
    """Place a sign on nearside (side=1) or offside (side=-1) verge >= 0.5 m off carriageway."""
    nx, nz = (-dz * side, dx * side)
    for extra in (0.9, 1.6, 2.5, 3.5):
        offset = width_m / 2.0 + extra
        cx = round(px + nx * offset, 3)
        cz = round(pz + nz * offset, 3)
        if not graph.on_carriageway(cx, cz, min_clearance_m=0.5):
            return cx, cz
    return None


def build_speed_signs(pack: dict[str, Any]) -> dict[str, Any]:
    """Generate speed limit start pairs (where limit changes) and 20 mph repeaters."""
    graph = RoadGraph(pack)
    signs: list[dict[str, Any]] = []
    idx = 0
    seen_entry: set[tuple[str, str, int]] = set()

    for nid in sorted(graph.nodes):
        for in_link, in_fwd, _ in graph.in_edges.get(nid, []):
            in_mph = int(graph.links[in_link].get("speedLimitMph", 30))
            for out_link, out_fwd, _ in graph.allowed_next(in_link, nid):
                out_lk = graph.links[out_link]
                out_mph = int(out_lk.get("speedLimitMph", 30))
                if out_mph == in_mph:
                    continue
                key = (out_link, "fwd" if out_fwd else "rev", out_mph)
                if key in seen_entry:
                    continue
                seen_entry.add(key)
                length = graph.link_lengths[out_link]
                dist_from_end = max(2.0, length - min(6.0, length * 0.25))
                placed = _point_and_dir_from_end(out_lk["geometry"], out_fwd, dist_from_end)
                if placed is None:
                    continue
                px, pz, dx, dz = placed
                width = float(out_lk.get("widthM", 6.5))
                heading = _heading_deg(dx, dz)
                for side in (1, -1):
                    pt = _place_on_verge(graph, px, pz, dx, dz, width, side=side)
                    if pt is None:
                        continue
                    idx += 1
                    signs.append(
                        {
                            "id": f"sp_{idx:03d}",
                            "x": pt[0],
                            "z": pt[1],
                            "heading": heading,
                            "mph": out_mph,
                            "kind": "start",
                            "linkId": out_link,
                        }
                    )

    # Repeaters on long 20 mph stretches (> 140 m)
    for lk in sorted(pack["links"], key=lambda item: item["id"]):
        if not lk.get("inMainNetwork", True):
            continue
        if int(lk.get("speedLimitMph", 30)) != 20:
            continue
        length = graph.link_lengths[lk["id"]]
        if length <= 140.0:
            continue
        width = float(lk.get("widthM", 6.5))
        for fwd in (True, False) if not lk.get("oneway", False) else (True,):
            placed = _point_and_dir_from_end(lk["geometry"], fwd, length * 0.5)
            if placed is None:
                continue
            px, pz, dx, dz = placed
            pt = _place_on_verge(graph, px, pz, dx, dz, width, side=1)
            if pt is None:
                continue
            idx += 1
            signs.append(
                {
                    "id": f"sp_{idx:03d}",
                    "x": pt[0],
                    "z": pt[1],
                    "heading": _heading_deg(dx, dz),
                    "mph": 20,
                    "kind": "repeater",
                    "linkId": lk["id"],
                }
            )

    return {
        "signsFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "headingConvention": "Degrees clockwise from +z (North): 0=+z, 90=+x, 180=-z, 270=-x",
        "source": {
            "kind": pack["source"]["kind"],
            "licence": pack["source"]["licence"],
            "attribution": pack["source"]["attribution"],
            "generator": f"ltl_pipeline/{__version__}",
            "styleSources": [GOV_UK_TRAFFIC_SIGNS_URL, GOV_UK_KYTS_URL],
        },
        "speedSigns": signs,
    }


class _FurnitureHandler(osmium.SimpleHandler):
    def __init__(self, bbox: list[float]) -> None:
        super().__init__()
        self.min_lon, self.min_lat, self.max_lon, self.max_lat = [float(v) for v in bbox]
        self.bus_stops: list[tuple[int, str, float, float]] = []
        self.hazards: list[tuple[str, float, float]] = []

    def _in_box(self, lat: float, lon: float) -> bool:
        return self.min_lon <= lon <= self.max_lon and self.min_lat <= lat <= self.max_lat

    def _classify_hazard(self, tags: osmium.osm.TagList) -> str | None:
        tc = tags.get("traffic_calming")
        if tc in ("bump", "hump", "table", "humps", "speed_table"):
            return "road_humps"
        if tc in ("choker", "chokers", "island"):
            return "road_narrows"
        hz = tags.get("hazard")
        if hz == "pedestrians":
            return "pedestrians_in_road"
        if hz == "queues_likely":
            return "queues_likely"
        if hz == "curve":
            return "bend_ahead"
        if hz == "bump":
            return "road_humps"
        return None

    def node(self, n: osmium.osm.Node) -> None:
        if not n.location.valid():
            return
        lat, lon = float(n.location.lat), float(n.location.lon)
        if not self._in_box(lat, lon):
            return
        if n.tags.get("highway") == "bus_stop":
            name = (n.tags.get("name") or "Bus Stop").strip()
            self.bus_stops.append((int(n.id), name, lat, lon))
        hkind = self._classify_hazard(n.tags)
        if hkind:
            self.hazards.append((hkind, lat, lon))

    def way(self, w: osmium.osm.Way) -> None:
        hkind = self._classify_hazard(w.tags)
        if not hkind:
            return
        lats = [float(n.location.lat) for n in w.nodes if n.location.valid()]
        lons = [float(n.location.lon) for n in w.nodes if n.location.valid()]
        if not lats:
            return
        lat = sum(lats) / len(lats)
        lon = sum(lons) / len(lons)
        if self._in_box(lat, lon):
            self.hazards.append((hkind, lat, lon))


def build_bus_stops_and_warnings(
    city_dir: Path = CITY_DIR,
) -> tuple[dict[str, Any], dict[str, Any]]:
    pack = json.loads((city_dir / "roadpack.json").read_text(encoding="utf-8"))
    pois_data = json.loads((city_dir / "pois.json").read_text(encoding="utf-8"))
    config = yaml.safe_load((city_dir / "config.yaml").read_text(encoding="utf-8"))

    graph = RoadGraph(pack)
    main_links = [lk for lk in pack["links"] if lk.get("inMainNetwork", True)]
    link_geoms = [graph.link_lines[lk["id"]] for lk in main_links]
    link_tree = STRtree(link_geoms)

    origin_lat = float(pack["origin"]["lat"])
    origin_lon = float(pack["origin"]["lon"])
    proj = LocalProjection(origin_lat, origin_lon)

    fetch_res = fetch_extract(config["extract"]["pbf_url"])
    handler = _FurnitureHandler(config["bbox"])
    handler.apply_file(str(fetch_res.pbf_path), locations=True)

    # 1. Bus stops
    bus_stops: list[dict[str, Any]] = []
    for idx, (osm_id, name, lat, lon) in enumerate(
        sorted(handler.bus_stops, key=lambda b: b[0]), start=1
    ):
        x, z = proj.to_local(lat, lon)
        nearest_idx = link_tree.nearest(Point(x, z))
        lk = main_links[nearest_idx]
        line = link_geoms[nearest_idx]
        proj_d = line.project(Point(x, z))
        total = _polyline_length(lk["geometry"])
        dist_from_end = max(1.0, min(total - 1.0, total - proj_d))
        res = _point_and_dir_from_end(lk["geometry"], True, dist_from_end)
        if res is None:
            continue
        px, pz, dx, dz = res
        # Keep original x,z if >= 0.5 m outside carriageway, else place on nearside verge
        if not graph.on_carriageway(x, z, min_clearance_m=0.5):
            bx, bz = x, z
        else:
            pt = _place_on_verge(graph, px, pz, dx, dz, float(lk.get("widthM", 6.5)), side=1)
            if pt is None:
                continue
            bx, bz = pt
        bus_stops.append(
            {
                "id": f"bs_{idx:03d}",
                "x": bx,
                "z": bz,
                "heading": _heading_deg(dx, dz),
                "name": name,
                "linkId": lk["id"],
                "osmId": osm_id,
            }
        )

    # 2. Warning signs: schools (within 150 m) + OSM hazards/traffic calming
    warnings: list[dict[str, Any]] = []
    warn_idx = 0
    placed_by_kind: dict[str, list[tuple[float, float]]] = {}

    def _too_close(kind: str, wx: float, wz: float, min_sep: float = 40.0) -> bool:
        for ex, ez in placed_by_kind.get(kind, []):
            if math.hypot(wx - ex, wz - ez) < min_sep:
                return True
        return False

    def _add_warning(kind: str, wx: float, wz: float, heading: float, link_id: str) -> None:
        nonlocal warn_idx
        if _too_close(kind, wx, wz):
            return
        placed_by_kind.setdefault(kind, []).append((wx, wz))
        warn_idx += 1
        warnings.append(
            {
                "id": f"ws_{warn_idx:03d}",
                "kind": kind,
                "x": wx,
                "z": wz,
                "heading": heading,
                "linkId": link_id,
                "diagramRef": DIAGRAM_REFS[kind],
            }
        )

    # School warnings on approaches within 150 m of each school POI
    schools = [p for p in pois_data["pois"] if p["kind"] == "school" and not p.get("outsideArea")]
    for sc in schools:
        sx_p, sz_p = sc["x"], sc["z"]
        for lk in main_links:
            mid = graph.link_lines[lk["id"]].interpolate(0.5, normalized=True)
            if math.hypot(mid.x - sx_p, mid.y - sz_p) > 150.0:
                continue
            length = graph.link_lengths[lk["id"]]
            if length < 15.0:
                continue
            res = _point_and_dir_from_end(lk["geometry"], True, min(25.0, length * 0.5))
            if res is None:
                continue
            px, pz, dx, dz = res
            pt = _place_on_verge(graph, px, pz, dx, dz, float(lk.get("widthM", 6.5)), side=1)
            if pt is not None:
                _add_warning("school", pt[0], pt[1], _heading_deg(dx, dz), lk["id"])

    # OSM traffic_calming / hazard tags
    for hkind, lat, lon in handler.hazards:
        x, z = proj.to_local(lat, lon)
        nearest_idx = link_tree.nearest(Point(x, z))
        lk = main_links[nearest_idx]
        line = link_geoms[nearest_idx]
        if Point(x, z).distance(line) > 25.0:
            continue
        proj_d = line.project(Point(x, z))
        total = _polyline_length(lk["geometry"])
        dist_from_end = max(1.0, min(total - 1.0, total - proj_d))
        res = _point_and_dir_from_end(lk["geometry"], True, dist_from_end)
        if res is None:
            continue
        px, pz, dx, dz = res
        pt = _place_on_verge(graph, px, pz, dx, dz, float(lk.get("widthM", 6.5)), side=1)
        if pt is not None:
            _add_warning(hkind, pt[0], pt[1], _heading_deg(dx, dz), lk["id"])

    base_meta = {
        "signsFormatVersion": "0.1",
        "packId": pack["packId"],
        "origin": pack["origin"],
        "headingConvention": "Degrees clockwise from +z (North): 0=+z, 90=+x, 180=-z, 270=-x",
        "source": {
            "kind": pack["source"]["kind"],
            "licence": pack["source"]["licence"],
            "attribution": pack["source"]["attribution"],
            "generator": f"ltl_pipeline/{__version__}",
            "styleSources": [GOV_UK_TRAFFIC_SIGNS_URL, GOV_UK_KYTS_URL],
        },
    }
    bus_doc = {**base_meta, "busStops": bus_stops}
    warn_meta = dict(base_meta)
    warn_meta["source"] = {**base_meta["source"], "diagramRefs": DIAGRAM_REFS}
    warn_doc = {**warn_meta, "warningSigns": warnings}
    return bus_doc, warn_doc


def write_street_furniture(city_dir: Path = CITY_DIR) -> tuple[Path, Path, Path]:
    pack = json.loads((city_dir / "roadpack.json").read_text(encoding="utf-8"))
    speed_doc = build_speed_signs(pack)
    bus_doc, warn_doc = build_bus_stops_and_warnings(city_dir)

    speed_path = city_dir / "speed_signs.json"
    bus_path = city_dir / "bus_stops.json"
    warn_path = city_dir / "warning_signs.json"

    speed_path.write_text(json.dumps(speed_doc, indent=2) + "\n", encoding="utf-8")
    bus_path.write_text(json.dumps(bus_doc, indent=2) + "\n", encoding="utf-8")
    warn_path.write_text(json.dumps(warn_doc, indent=2) + "\n", encoding="utf-8")

    validate_signs_placed_file(speed_path)
    validate_signs_placed_file(bus_path)
    validate_signs_placed_file(warn_path)
    return speed_path, bus_path, warn_path


if __name__ == "__main__":
    sp, bs, ws = write_street_furniture()
    print(f"Wrote {sp}, {bs}, {ws}")
