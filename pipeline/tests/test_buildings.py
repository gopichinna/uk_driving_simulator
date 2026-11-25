"""Tests for Wolverhampton buildings.json."""

from __future__ import annotations

from shapely.geometry import Polygon

from ltl_pipeline.schema import REPO_ROOT, validate_buildings_file

BUILDINGS_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "buildings.json"


def test_buildings_valid_under_3mb_and_ccw() -> None:
    assert BUILDINGS_PATH.is_file()
    assert BUILDINGS_PATH.stat().st_size < 3 * 1024 * 1024
    data = validate_buildings_file(BUILDINGS_PATH)
    assert data["packId"] == "gb-wolverhampton-centre"
    assert len(data["buildings"]) > 50
    for b in data["buildings"][:50]:
        poly = Polygon(b["footprint"])
        assert poly.area >= 15.0
        assert poly.exterior.is_ccw
