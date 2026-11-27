"""Tests for draft scenarios in content/scenarios/gb/drafts/ (R32.2 + R33.3 + R33.4)."""

from __future__ import annotations

import json
import re

from ltl_pipeline.schema import REPO_ROOT, validate_scenario_file

DRAFTS_DIR = REPO_ROOT / "content" / "scenarios" / "gb" / "drafts"
LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
PROGRESSION_PATH = LESSONS_DIR / "progression.json"
SPECS_DIR = REPO_ROOT / "docs" / "specs"


def _spec_rule_ids() -> set[str]:
    ids: set[str] = set()
    for spec_name in ("next-rules.md", "next-rules-2.md"):
        text = (SPECS_DIR / spec_name).read_text(encoding="utf-8")
        for m in re.finditer(r"^##\s+\d+\.\s+`(GB\.[A-Z]+\.[A-Z0-9_]+)`", text, re.MULTILINE):
            ids.add(m.group(1))
    return ids


def _rule_id_to_draft_id(rule_id: str) -> str:
    return "draft_" + rule_id.removeprefix("GB.").replace(".", "_").lower()


def test_forty_draft_scenarios_validate_and_match_specs() -> None:
    draft_files = sorted(DRAFTS_DIR.glob("draft_*.json"))
    assert len(draft_files) == 40, f"Expected 40 draft scenarios, found {len(draft_files)}"

    wired_ids: set[str] = set()
    for lesson_path in LESSONS_DIR.glob("[0-9][0-9]-*.json"):
        data = json.loads(lesson_path.read_text(encoding="utf-8"))
        for sc in data.get("scenarios", []):
            wired_ids.add(sc["id"])

    progression_text = PROGRESSION_PATH.read_text(encoding="utf-8")

    draft_ids: set[str] = set()
    for path in draft_files:
        payload = validate_scenario_file(path)
        sid = payload["scenarioId"]
        draft_ids.add(sid)
        assert sid not in wired_ids, f"Draft {sid} must not be wired into lessons yet"
        assert sid not in progression_text, f"Draft {sid} must not be in progression.json yet"

    spec_ids = _spec_rule_ids()
    assert len(spec_ids) == 40, (
        f"Expected 40 spec rule IDs across batch 1 & 2, found {len(spec_ids)}"
    )
    expected_draft_ids = {_rule_id_to_draft_id(rid) for rid in spec_ids}
    assert draft_ids == expected_draft_ids, (
        f"Mismatch between spec rule IDs and draft files: "
        f"missing={expected_draft_ids - draft_ids}, extra={draft_ids - expected_draft_ids}"
    )
