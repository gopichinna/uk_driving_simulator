"""Tests for Wolverhampton POIs (content/cities/wolverhampton/pois.json)."""

from __future__ import annotations

import json

from ltl_pipeline.schema import REPO_ROOT, validate_pois_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"
POIS_PATH = CITY_DIR / "pois.json"
ROADPACK_PATH = CITY_DIR / "roadpack.json"


def _load() -> tuple[dict, dict]:
    pois = json.loads(POIS_PATH.read_text(encoding="utf-8"))
    pack = json.loads(ROADPACK_PATH.read_text(encoding="utf-8"))
    return pois, pack


def test_pois_validates_against_schema() -> None:
    assert POIS_PATH.exists(), "pois.json should exist"
    data = validate_pois_file(POIS_PATH)
    assert data["poisFormatVersion"] == "0.1"
    assert len(data["pois"]) >= 10


def test_every_poi_access_link_is_in_main_network() -> None:
    pois, pack = _load()
    main_link_ids = {
        link["id"] for link in pack["links"] if link.get("inMainNetwork", True)
    }
    for poi in pois["pois"]:
        assert poi["accessLinkId"] in main_link_ids, (
            f"POI {poi['id']} accessLinkId {poi['accessLinkId']} not in mainNetwork"
        )
        assert poi["name"].strip() != ""


def test_queen_square_city_centre_present() -> None:
    pois, _ = _load()
    centres = [p for p in pois["pois"] if p["kind"] == "city_centre"]
    assert centres, "Queen Square city_centre anchor should be present"
    assert any("Queen Square" in p["name"] for p in centres)


def test_outside_area_buffer_pois_and_odbl_attribution() -> None:
    pois, _ = _load()
    assert pois["source"]["licence"] == "ODbL-1.0"
    assert "© OpenStreetMap contributors" in pois["source"]["attribution"]
    outside = [p for p in pois["pois"] if p.get("outsideArea")]
    assert outside, "Expected at least one POI in the 500 m buffer outside the bbox"
