"""Tests for lesson progression, stage unlocks, star rules, and free tier."""

from __future__ import annotations

import json

import yaml

from ltl_pipeline.schema import REPO_ROOT, validate_progression_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
PROGRESSION_PATH = LESSONS_DIR / "progression.json"
TEST_MARKING_PATH = REPO_ROOT / "content" / "countries" / "gb" / "test_marking.yaml"


def test_progression_validates_and_matches_lessons() -> None:
    prog = validate_progression_file(PROGRESSION_PATH)
    test_marking = yaml.safe_load(TEST_MARKING_PATH.read_text(encoding="utf-8"))

    lesson_ids_from_files: list[str] = []
    free_from_files: list[str] = []
    for lf in sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        lesson_ids_from_files.append(lesson["id"])
        if lesson["freeTier"]:
            free_from_files.append(lesson["id"])

    seen_in_stages: list[str] = []
    for st in prog["stages"]:
        seen_in_stages.extend(st["lessonIds"])
        if st["unlockAfter"] is not None:
            assert st["unlockAfter"]["stage"] < st["stage"], (
                f"Stage {st['stage']} unlockAfter must reference an earlier stage"
            )

    assert len(seen_in_stages) == len(set(seen_in_stages))
    assert sorted(seen_in_stages) == sorted(lesson_ids_from_files)
    assert sorted(prog["freeTier"]) == sorted(free_from_files)

    for ref in prog["stars"]["two"].get("testMarkingRefs", []):
        assert ref in test_marking, f"Unknown test_marking.yaml field: {ref}"
