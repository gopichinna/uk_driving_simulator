"""Tests for Stages 0-2 GB lesson files."""

from __future__ import annotations

import json

import yaml

from ltl_pipeline.readability import (
    check_text_readability,
    extract_glossary_abbreviations,
    load_banned_phrases,
)
from ltl_pipeline.schema import REPO_ROOT, validate_lesson_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
GLOSSARY_JSON = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "glossary.json"

ALLOWED_COURSE_ANCHORS: dict[str, set[str]] = {
    "town": {
        "crossroads_signals",
        "tjunction_north",
        "tjunction_south",
        "zebra",
        "box_junction",
        "school_street",
        "start",
    },
    "roundabout": {"start"},
    "two_lane_roundabout": {"start"},
    "dual_carriageway": {"start"},
    "wolverhampton": {"start"},
}


def test_lessons_validate_and_match_constraints() -> None:
    lesson_files = sorted(LESSONS_DIR.glob("[01][0-9]-*.json"))
    assert len(lesson_files) == 19

    rules_data = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    valid_rule_ids = {r["id"] for r in rules_data["rules"]}
    glossary_data = json.loads(GLOSSARY_JSON.read_text(encoding="utf-8"))
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    banned = load_banned_phrases()

    free_orders: list[int] = []
    all_orders: list[int] = []

    for lf in lesson_files:
        lesson = validate_lesson_file(lf)
        all_orders.append(lesson["order"])
        if lesson["freeTier"]:
            free_orders.append(lesson["order"])

        for rid in lesson["learnCard"]["ruleIds"]:
            assert rid in valid_rule_ids, f"{lf.name}: unknown ruleId {rid}"

        diagram_rel = lesson["learnCard"].get("diagram")
        assert diagram_rel, f"{lf.name}: missing learnCard.diagram"
        diagram_png = REPO_ROOT / diagram_rel
        diagram_svg = diagram_png.with_suffix(".svg")
        assert diagram_png.is_file(), f"Missing diagram PNG: {diagram_png}"
        assert diagram_svg.is_file(), f"Missing diagram SVG: {diagram_svg}"

        for idx, block in enumerate(lesson["learnCard"]["blocks"]):
            errs = check_text_readability(
                block,
                context=f"{lf.name}.learnCard[{idx}]",
                banned_phrases=banned,
                allowed_abbrevs=allowed_abbrevs,
            )
            assert errs == [], f"Readability errors in {lf.name}: {errs}"

        for sc in lesson["scenarios"]:
            course = sc["course"]
            anchor = sc["anchor"]
            assert anchor in ALLOWED_COURSE_ANCHORS[course], (
                f"{lf.name}/{sc['id']}: invalid pair ({course}, {anchor})"
            )
            for rid in sc["rulesJudged"]:
                assert rid in valid_rule_ids, f"{lf.name}/{sc['id']}: unknown rule {rid}"

    assert sorted(all_orders) == list(range(1, 20))
    assert sorted(free_orders) == [1, 2, 3]
