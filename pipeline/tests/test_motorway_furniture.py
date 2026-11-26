"""Tests for m54-j2 motorway_signs.json and gantry_messages.json."""

from __future__ import annotations

from ltl_pipeline.schema import (
    REPO_ROOT,
    validate_gantry_messages_file,
    validate_signs_placed_file,
)

M54_DIR = REPO_ROOT / "content" / "cities" / "m54-j2"


def test_motorway_signs_and_gantries_validate() -> None:
    signs_doc = validate_signs_placed_file(M54_DIR / "motorway_signs.json")
    signs = signs_doc["motorwaySigns"]
    kinds = {s["kind"] for s in signs}
    assert {"countdown_3", "countdown_2", "countdown_1", "final_exit", "emergency_area"} <= kinds

    gantries_doc = validate_gantry_messages_file(M54_DIR / "gantry_messages.json")
    assert len(gantries_doc["gantries"]) >= 6
    for g in gantries_doc["gantries"]:
        assert g["laneIndices"] == list(range(g["lanes"]))
        assert g["supportsRedX"] is True
        assert g["supportsVariableLimit"] is True
