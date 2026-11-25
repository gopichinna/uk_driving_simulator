"""Tests for M54 motorway pack (>= 4 km per carriageway, no service roads) and Stage 6-8."""

from __future__ import annotations

import json
import math

from ltl_pipeline.schema import REPO_ROOT, validate_progression_file, validate_roadpack_file

M54_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"
PROGRESSION_PATH = REPO_ROOT / "content" / "lessons" / "gb" / "progression.json"


def _len_m(geom: list[list[float]]) -> float:
    return sum(
        math.hypot(geom[i + 1][0] - geom[i][0], geom[i + 1][1] - geom[i][1])
        for i in range(len(geom) - 1)
    )


def test_m54_j2_pack_and_motorway_fields() -> None:
    pack = validate_roadpack_file(M54_DIR / "roadpack.json")
    assert pack["packFormatVersion"] == "0.7"
    assert pack["packId"] == "gb-m54-j2"
    assert (M54_DIR / "QA.md").is_file()
    assert len(pack["gantries"]) >= 6, "Expected gantries along >= 4 km motorway"
    assert pack["emergencyAreas"], "Expected motorway emergency areas"

    assert all(lk["roadClass"] != "service" for lk in pack["links"]), "No service roads allowed"

    mwy_links = [lk for lk in pack["links"] if lk["roadClass"] == "motorway"]
    assert any(lk.get("hardShoulder") is True for lk in mwy_links)
    assert any(lk.get("junctionNumber") in ("1", "2") for lk in mwy_links)

    eb_m = sum(
        _len_m(lk["geometry"])
        for lk in mwy_links
        if lk.get("hardShoulder")
        and (lk["geometry"][-1][0] - lk["geometry"][0][0])
        - (lk["geometry"][-1][1] - lk["geometry"][0][1])
        >= 0
    )
    wb_m = sum(
        _len_m(lk["geometry"])
        for lk in mwy_links
        if lk.get("hardShoulder")
        and (lk["geometry"][-1][0] - lk["geometry"][0][0])
        - (lk["geometry"][-1][1] - lk["geometry"][0][1])
        < 0
    )
    assert eb_m >= 4000.0, f"Eastbound motorway length {eb_m:.1f} m < 4000 m"
    assert wb_m >= 4000.0, f"Westbound motorway length {wb_m:.1f} m < 4000 m"


def test_progression_places_stages_6_and_7_before_mock_test() -> None:
    prog = validate_progression_file(PROGRESSION_PATH)
    stage_nums = [s["stage"] for s in prog["stages"]]
    assert stage_nums == [0, 1, 2, 3, 4, 5, 6, 7, 8]
    by_stage = {s["stage"]: s for s in prog["stages"]}
    assert by_stage[6]["unlockAfter"]["stage"] == 5
    assert by_stage[7]["unlockAfter"]["stage"] == 6
    assert by_stage[8]["unlockAfter"]["stage"] == 7
    assert len(json.loads(PROGRESSION_PATH.read_text(encoding="utf-8"))["stages"]) == 9
