"""Tests for roundabout_furniture.json across Wolverhampton city packs."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT, validate_roundabout_furniture_file


def test_roundabout_furniture_validates_with_diagram_refs() -> None:
    for city in ("wolverhampton", "wolverhampton-test-area"):
        path = REPO_ROOT / "content" / "cities" / city / "roundabout_furniture.json"
        doc = validate_roundabout_furniture_file(path)
        assert doc["roundabouts"], f"{city}: expected at least 1 roundabout"
        assert "515" in doc["source"]["diagramRefs"]["chevron"]
        assert "1003" in doc["source"]["diagramRefs"]["give_way_line"]
        for rbt in doc["roundabouts"]:
            assert len(rbt["islandPolygon"]) >= 3
            assert rbt["giveWayLines"] or rbt["roundaboutSigns"]
