"""Tests for M54 Junction 2 motorway pack and Stage 6-8 progression order."""

from __future__ import annotations

import json

from ltl_pipeline.schema import REPO_ROOT, validate_progression_file, validate_roadpack_file

M54_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"
PROGRESSION_PATH = REPO_ROOT / "content" / "lessons" / "gb" / "progression.json"


def test_m54_j2_pack_and_motorway_fields() -> None:
    pack = validate_roadpack_file(M54_DIR / "roadpack.json")
    assert pack["packFormatVersion"] == "0.7"
    assert pack["packId"] == "gb-m54-j2"
    assert (M54_DIR / "QA.md").is_file()
    assert pack["gantries"], "Expected motorway gantries"
    assert pack["emergencyAreas"], "Expected motorway emergency areas"

    mwy_links = [lk for lk in pack["links"] if lk["roadClass"] == "motorway"]
    assert any(lk.get("hardShoulder") is True for lk in mwy_links)
    assert any(lk.get("junctionNumber") == "2" for lk in mwy_links)


def test_progression_places_stages_6_and_7_before_mock_test() -> None:
    prog = validate_progression_file(PROGRESSION_PATH)
    stage_nums = [s["stage"] for s in prog["stages"]]
    assert stage_nums == [0, 1, 2, 3, 4, 5, 6, 7, 8]
    by_stage = {s["stage"]: s for s in prog["stages"]}
    assert by_stage[6]["unlockAfter"]["stage"] == 5
    assert by_stage[7]["unlockAfter"]["stage"] == 6
    assert by_stage[8]["unlockAfter"]["stage"] == 7
    assert len(json.loads(PROGRESSION_PATH.read_text(encoding="utf-8"))["stages"]) == 9
