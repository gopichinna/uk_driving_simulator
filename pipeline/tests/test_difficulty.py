"""Tests for Round 40 difficulty ladder across all lesson scenarios."""

from __future__ import annotations

import json

from ltl_pipeline.schema import REPO_ROOT, validate_scenario_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
SCENARIOS_DIR = REPO_ROOT / "content" / "scenarios" / "gb"


def test_every_lesson_has_valid_difficulty_ladder() -> None:
    lesson_files = sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json"))
    assert len(lesson_files) == 32

    for lf in lesson_files:
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        diffs: list[int] = []
        for sc in lesson["scenarios"]:
            sdata = validate_scenario_file(SCENARIOS_DIR / f"{sc['id']}.json")
            diff = sdata.get("difficulty")
            assert diff in (1, 2, 3), f"{lf.name}/{sc['id']}: missing or invalid difficulty {diff}"
            diffs.append(diff)

        assert diffs[0] == 1, f"{lf.name}: first scenario must be difficulty 1, got {diffs[0]}"
        for i in range(1, len(diffs)):
            assert abs(diffs[i] - diffs[i - 1]) <= 1, (
                f"{lf.name}: difficulty jump > 1 between scenarios {i - 1} and {i}: {diffs}"
            )


def test_ten_central_scenarios_have_b_and_c_variants() -> None:
    variant_bases: set[str] = set()
    for lf in sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        for v in lesson.get("variants", []):
            vid = v["id"]
            assert vid.endswith(("_b", "_c")), f"{lf.name}: unexpected variant id {vid}"
            sdata = validate_scenario_file(SCENARIOS_DIR / f"{vid}.json")
            assert sdata["scenarioId"] == vid
            variant_bases.add(vid[:-2])
    assert len(variant_bases) >= 10, (
        f"Expected >= 10 scenarios with _b/_c variants, got {len(variant_bases)}"
    )
