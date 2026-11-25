"""Tests for lesson knowledge-check quizzes."""

from __future__ import annotations

import json

import yaml

from ltl_pipeline.readability import (
    check_text_readability,
    extract_glossary_abbreviations,
    load_banned_phrases,
)
from ltl_pipeline.schema import REPO_ROOT, validate_quiz_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
QUIZ_DIR = LESSONS_DIR / "quiz"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
GLOSSARY_JSON = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "glossary.json"


def test_every_lesson_has_valid_original_quiz() -> None:
    rules_data = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    valid_rule_ids = {r["id"] for r in rules_data["rules"]}
    glossary_data = json.loads(GLOSSARY_JSON.read_text(encoding="utf-8"))
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    banned = load_banned_phrases()

    lesson_files = sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json"))
    assert len(lesson_files) == 20

    for lf in lesson_files:
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        lid = lesson["id"]
        qpath = QUIZ_DIR / f"{lid}.json"
        assert qpath.is_file(), f"Missing quiz file: {qpath}"
        quiz = validate_quiz_file(qpath)
        assert quiz["lessonId"] == lid

        for q in quiz["questions"]:
            assert 0 <= q["answerIndex"] < len(q["options"])
            assert len(set(q["options"])) == len(q["options"]), (
                f"{lid}/{q['id']}: duplicate options"
            )
            assert q["ruleId"] in valid_rule_ids, f"{lid}/{q['id']}: unknown ruleId"
            for field_name in ("question", "explanation"):
                errs = check_text_readability(
                    q[field_name],
                    context=f"{lid}.{q['id']}.{field_name}",
                    banned_phrases=banned,
                    allowed_abbrevs=allowed_abbrevs,
                )
                assert errs == [], f"Readability error: {errs}"
            for opt in q["options"]:
                errs = check_text_readability(
                    opt,
                    context=f"{lid}.{q['id']}.option",
                    banned_phrases=banned,
                    allowed_abbrevs=allowed_abbrevs,
                )
                assert errs == [], f"Readability error: {errs}"
