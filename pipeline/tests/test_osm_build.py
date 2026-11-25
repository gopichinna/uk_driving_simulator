"""Tests for OSM extraction and RoadPack building using hand-authored .osm fixtures."""

from __future__ import annotations

import json
from pathlib import Path

from ltl_pipeline.build import build_roadpack
from ltl_pipeline.extract import extract_osm
from ltl_pipeline.schema import validate_roadpack

FIXTURE_OSM = Path(__file__).parent / "fixtures" / "mini_town.osm"
TEST_CONFIG = {
    "pack_id": "test-mini-town",
    "display_name": "Test Mini Town",
    "country": "GB",
    "region": "GB-ENG",
    "driving_side": "left",
    "origin": {"lat": 52.5862, "lon": -2.1288},
    "bbox": [-2.1320, 52.5840, -2.1260, 52.5890],
    "extract": {"name": "mini_town.osm"},
}


def _build_fixture_pack() -> dict:
    extracted = extract_osm(FIXTURE_OSM, bbox=tuple(TEST_CONFIG["bbox"]), exclude_tram=True)
    return build_roadpack(
        extracted=extracted,
        config=TEST_CONFIG,
        extract_timestamp="2026-11-25T00:00:00Z",
    )


def test_fixture_validates_against_schema() -> None:
    pack = _build_fixture_pack()
    validate_roadpack(pack)


def test_t_junction_split_and_signals() -> None:
    pack = _build_fixture_pack()
    # Way 100 (1-2-3) should be split at node 2 into two links
    way100_links = [lk for lk in pack["links"] if lk["osmWayId"] == 100]
    assert len(way100_links) == 2

    nodes_by_id = {n["id"]: n for n in pack["nodes"]}
    assert nodes_by_id["n_2"]["kind"] == "junction"
    assert nodes_by_id["n_1"]["trafficSignals"] is True


def test_maxspeed_tagged_vs_default() -> None:
    pack = _build_fixture_pack()
    way100_links = [lk for lk in pack["links"] if lk["osmWayId"] == 100]
    way101_links = [lk for lk in pack["links"] if lk["osmWayId"] == 101]

    assert all(
        lk["speedLimitMph"] == 40 and lk["speedLimitSource"] == "tagged"
        for lk in way100_links
    )
    assert all(
        lk["speedLimitMph"] == 30 and lk["speedLimitSource"] == "default"
        for lk in way101_links
    )


def test_zebra_detection() -> None:
    pack = _build_fixture_pack()
    zebras = [c for c in pack["crossings"] if c["type"] == "zebra"]
    assert len(zebras) == 1
    assert zebras[0]["osmId"] == 11
    assert zebras[0]["confidence"] >= 0.9


def test_roundabout_node_kind() -> None:
    pack = _build_fixture_pack()
    nodes_by_id = {n["id"]: n for n in pack["nodes"]}
    assert nodes_by_id["n_12"]["kind"] == "roundabout"
    rbt_links = [lk for lk in pack["links"] if lk["osmWayId"] == 200]
    assert len(rbt_links) == 1
    assert rbt_links[0]["junctionRoundabout"] is True
    assert rbt_links[0]["oneway"] is True


def test_tram_street_exclusion() -> None:
    pack = _build_fixture_pack()
    link_way_ids = {lk["osmWayId"] for lk in pack["links"]}
    assert 300 not in link_way_ids
    excluded_ids = {e["osmWayId"] for e in pack["excluded"]}
    assert 300 in excluded_ids


def test_deterministic_output() -> None:
    pack_a = json.dumps(_build_fixture_pack(), indent=2)
    pack_b = json.dumps(_build_fixture_pack(), indent=2)
    assert pack_a == pack_b


def test_junction_priority_and_signal_plans() -> None:
    pack = _build_fixture_pack()
    assert pack["packFormatVersion"] == "0.4"
    assert all("widthM" in lk and "sidewalk" in lk for lk in pack["links"])
    jp_by_node = {jp["nodeId"]: jp for jp in pack["junctionPriority"]}
    assert "n_2" in jp_by_node
    assert jp_by_node["n_2"]["controlledBy"] == "uncontrolled"
    assert jp_by_node["n_12"]["controlledBy"] == "roundabout"

