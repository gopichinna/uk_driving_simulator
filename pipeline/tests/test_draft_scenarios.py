"""Tests for Round 32 draft scenarios in content/scenarios/gb/drafts/."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT, validate_scenario_file

DRAFTS_DIR = REPO_ROOT / "content" / "scenarios" / "gb" / "drafts"
LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"


def test_twenty_draft_scenarios_validate_and_are_not_wired_yet() -> None:
    drafts = sorted(DRAFTS_DIR.glob("draft_*.json"))
    assert len(drafts) == 20
    prog_text = (LESSONS_DIR / "progression.json").read_text(encoding="utf-8")
    lesson_text = "\n".join(
        lf.read_text(encoding="utf-8") for lf in sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json"))
    )
    for path in drafts:
        data = validate_scenario_file(path)
        sc_id = data["scenarioId"]
        assert sc_id not in prog_text, f"{sc_id} must not be in progression.json yet"
        assert sc_id not in lesson_text, f"{sc_id} must not be wired into a lesson yet"
