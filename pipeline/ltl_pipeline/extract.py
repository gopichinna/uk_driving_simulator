"""Clip an OSM PBF/XML file to a bbox and extract drivable highways and traffic features."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

import osmium

DRIVABLE_HIGHWAY_MAP: dict[str, str] = {
    "motorway": "motorway",
    "motorway_link": "motorway",
    "trunk": "trunk",
    "trunk_link": "trunk",
    "primary": "primary",
    "primary_link": "primary",
    "secondary": "secondary",
    "secondary_link": "secondary",
    "tertiary": "tertiary",
    "tertiary_link": "tertiary",
    "unclassified": "unclassified",
    "residential": "residential",
    "service": "service",
    "living_street": "living_street",
}

KEPT_WAY_TAG_KEYS = (
    "highway",
    "name",
    "ref",
    "width",
    "sidewalk",
    "sidewalk:left",
    "sidewalk:right",
    "sidewalk:both",
    "lanes",
    "lanes:forward",
    "lanes:backward",
    "turn:lanes",
    "turn:lanes:forward",
    "turn:lanes:backward",
    "oneway",
    "maxspeed",
    "maxspeed:type",
    "zone:maxspeed",
    "junction",
    "busway",
    "busway:left",
    "busway:right",
    "busway:both",
    "bus:lanes",
    "bus:lanes:forward",
    "bus:lanes:backward",
    "bus:lanes:conditional",
    "bus:lanes:forward:conditional",
    "bus:lanes:backward:conditional",
    "cycleway",
    "cycleway:left",
    "cycleway:right",
    "cycleway:both",
    "railway",
)

KEPT_NODE_TAG_KEYS = (
    "highway",
    "crossing",
    "crossing_ref",
    "traffic_signals",
    "give_way",
    "stop",
)


@dataclass(frozen=True)
class OsmNode:
    osm_id: int
    lat: float
    lon: float
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class OsmWay:
    osm_id: int
    road_class: str
    node_ids: tuple[int, ...]
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class ExcludedWay:
    osm_way_id: int
    name: str | None
    reason: str


@dataclass(frozen=True)
class OsmRestriction:
    osm_id: int
    restriction_type: str
    from_way_id: int
    via_node_id: int
    to_way_id: int


@dataclass(frozen=True)
class ExtractedOsm:
    nodes: dict[int, OsmNode]
    ways: list[OsmWay]
    excluded: list[ExcludedWay]
    restrictions: list[OsmRestriction] = field(default_factory=list)


def _in_bbox(lat: float, lon: float, bbox: tuple[float, float, float, float]) -> bool:
    min_lon, min_lat, max_lon, max_lat = bbox
    return min_lon <= lon <= max_lon and min_lat <= lat <= max_lat


def _filter_tags(tags: osmium.osm.TagList, allowed: tuple[str, ...]) -> dict[str, str]:
    result: dict[str, str] = {}
    for k in allowed:
        if k in tags:
            result[k] = tags[k]
    return result


class _Pass1Handler(osmium.SimpleHandler):
    """Collect candidate drivable ways and tramway node sets inside bbox."""

    def __init__(self, bbox: tuple[float, float, float, float], exclude_tram: bool = True) -> None:
        super().__init__()
        self.bbox = bbox
        self.exclude_tram = exclude_tram
        self.tram_node_ids: set[int] = set()
        self.candidate_ways: list[tuple[int, str, tuple[int, ...], dict[str, str]]] = []
        self.explicit_tram_highways: list[ExcludedWay] = []
        self.candidate_restrictions: list[OsmRestriction] = []

    def relation(self, r: osmium.osm.Relation) -> None:
        if r.tags.get("type") != "restriction":
            return
        rtype = r.tags.get("restriction") or r.tags.get("restriction:motorcar")
        if not rtype:
            return
        from_way: int | None = None
        to_way: int | None = None
        via_node: int | None = None
        for m in r.members:
            if m.role == "from" and m.type == "w":
                from_way = int(m.ref)
            elif m.role == "to" and m.type == "w":
                to_way = int(m.ref)
            elif m.role == "via" and m.type == "n":
                via_node = int(m.ref)
        if from_way is not None and to_way is not None and via_node is not None:
            self.candidate_restrictions.append(
                OsmRestriction(
                    osm_id=int(r.id),
                    restriction_type=rtype.strip(),
                    from_way_id=from_way,
                    via_node_id=via_node,
                    to_way_id=to_way,
                )
            )

    def way(self, w: osmium.osm.Way) -> None:
        node_ids: list[int] = []
        any_in_bbox = False
        for n in w.nodes:
            if not n.location.valid():
                continue
            node_ids.append(n.ref)
            if _in_bbox(n.location.lat, n.location.lon, self.bbox):
                any_in_bbox = True

        if not any_in_bbox or len(node_ids) < 2:
            return

        tags = w.tags
        is_tram = self.exclude_tram and tags.get("railway") == "tram"
        if is_tram:
            self.tram_node_ids.update(node_ids)

        hw = tags.get("highway")
        if hw not in DRIVABLE_HIGHWAY_MAP:
            return

        if is_tram:
            self.explicit_tram_highways.append(
                ExcludedWay(
                    osm_way_id=int(w.id),
                    name=tags.get("name"),
                    reason="Excluded highway tagged railway=tram",
                )
            )
            return

        filtered_tags = _filter_tags(tags, KEPT_WAY_TAG_KEYS)
        self.candidate_ways.append(
            (int(w.id), DRIVABLE_HIGHWAY_MAP[hw], tuple(node_ids), filtered_tags)
        )


class _Pass2Handler(osmium.SimpleHandler):
    """Collect all nodes referenced by kept ways or tagged features in bbox."""

    def __init__(
        self,
        bbox: tuple[float, float, float, float],
        needed_node_ids: set[int],
    ) -> None:
        super().__init__()
        self.bbox = bbox
        self.needed_node_ids = needed_node_ids
        self.nodes: dict[int, OsmNode] = {}

    def node(self, n: osmium.osm.Node) -> None:
        if not n.location.valid():
            return
        nid = int(n.id)
        if nid not in self.needed_node_ids:
            return
        tags = _filter_tags(n.tags, KEPT_NODE_TAG_KEYS)
        self.nodes[nid] = OsmNode(
            osm_id=nid,
            lat=float(n.location.lat),
            lon=float(n.location.lon),
            tags=tags,
        )


def extract_osm(
    osm_path: Path,
    bbox: tuple[float, float, float, float],
    exclude_tram: bool = True,
    excluded_way_ids: set[int] | None = None,
) -> ExtractedOsm:
    """Extract drivable ways and relevant nodes within bbox from .osm.pbf or .osm."""
    extra_excluded = excluded_way_ids or set()
    p1 = _Pass1Handler(bbox=bbox, exclude_tram=exclude_tram)
    p1.apply_file(str(osm_path), locations=True)

    kept_ways: list[OsmWay] = []
    excluded: list[ExcludedWay] = list(p1.explicit_tram_highways)
    needed_node_ids: set[int] = set()

    for osm_id, road_class, node_ids, tags in p1.candidate_ways:
        if osm_id in extra_excluded:
            excluded.append(
                ExcludedWay(
                    osm_way_id=osm_id,
                    name=tags.get("name"),
                    reason="Excluded by city config",
                )
            )
            continue

        # If >= 2 nodes are shared with a railway=tram way, this is a street-running tramway.
        if exclude_tram and len(p1.tram_node_ids.intersection(node_ids)) >= 2:
            excluded.append(
                ExcludedWay(
                    osm_way_id=osm_id,
                    name=tags.get("name"),
                    reason="Street shares carriageway with railway=tram",
                )
            )
            continue

        kept_ways.append(
            OsmWay(
                osm_id=osm_id,
                road_class=road_class,
                node_ids=node_ids,
                tags=tags,
            )
        )
        needed_node_ids.update(node_ids)

    p2 = _Pass2Handler(bbox=bbox, needed_node_ids=needed_node_ids)
    p2.apply_file(str(osm_path), locations=False)

    kept_way_ids = {w.osm_id for w in kept_ways}
    restrictions = [
        r
        for r in p1.candidate_restrictions
        if r.from_way_id in kept_way_ids
        and r.to_way_id in kept_way_ids
        and r.via_node_id in p2.nodes
    ]
    restrictions.sort(key=lambda r: r.osm_id)

    # Sort deterministically
    kept_ways.sort(key=lambda w: w.osm_id)
    excluded.sort(key=lambda e: e.osm_way_id)

    return ExtractedOsm(
        nodes=p2.nodes,
        ways=kept_ways,
        excluded=excluded,
        restrictions=restrictions,
    )
