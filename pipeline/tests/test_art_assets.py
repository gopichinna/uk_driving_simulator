"""Tests for Round 18 visual assets: lesson badges, fault icons, and app icon concepts."""

from __future__ import annotations

import json
import struct
from pathlib import Path

from ltl_pipeline.art_assets import (
    BADGES_DIR,
    ICON_CONCEPTS_DIR,
    ICONS_DIR,
    LESSONS_DIR,
    RULE_GROUP_SPECS,
)


def _png_size(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", f"{path} is not a PNG"
    w, h = struct.unpack(">II", data[16:24])
    return w, h


def test_all_27_lesson_badges_exist_as_svg_and_256px_png() -> None:
    lesson_files = sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json"))
    assert len(lesson_files) == 31
    for lf in lesson_files:
        lid = json.loads(lf.read_text(encoding="utf-8"))["id"]
        svg = BADGES_DIR / f"{lid}.svg"
        png = BADGES_DIR / f"{lid}.png"
        assert svg.is_file(), f"Missing badge SVG: {svg}"
        assert "#0B6E4F" in svg.read_text(encoding="utf-8")
        assert png.is_file(), f"Missing badge PNG: {png}"
        assert _png_size(png) == (256, 256)


def test_fault_icons_and_rule_groups_mapping() -> None:
    mapping_path = ICONS_DIR / "rule_groups.json"
    assert mapping_path.is_file()
    mapping = json.loads(mapping_path.read_text(encoding="utf-8"))["groups"]
    expected_groups = {spec["id"] for spec in RULE_GROUP_SPECS}
    assert set(mapping.keys()) == expected_groups
    for gid in expected_groups:
        svg = ICONS_DIR / f"{gid}.svg"
        png = ICONS_DIR / f"{gid}.png"
        assert svg.is_file()
        assert png.is_file()
        assert _png_size(png) == (96, 96)


def test_three_app_icon_concepts_exist_at_1024px() -> None:
    pngs = sorted(ICON_CONCEPTS_DIR.glob("*.png"))
    assert len(pngs) == 3
    for png in pngs:
        assert _png_size(png) == (1024, 1024)
