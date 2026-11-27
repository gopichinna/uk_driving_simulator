"""Tests for Round 41 machine-readable rule fixtures (Batch 3 rules)."""

from __future__ import annotations

import yaml

from ltl_pipeline.schema import REPO_ROOT, validate_rulefixture_file

FIXTURES_DIR = REPO_ROOT / "content" / "fixtures" / "rules"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"

BATCH_3_RULES = {
    "GB.SIG.MISLEADING",
    "GB.SIG.HORN",
    "GB.SPD.PROGRESS",
    "GB.JCT.DUAL_CROSS",
    "GB.XNG.LIGHT_CROSSINGS",
    "GB.XNG.SCHOOL_PATROL",
    "GB.VRU.PASS_HORSE",
    "GB.VRU.CYCLE_LANE_SOLID",
    "GB.BUS.PULLING_OUT",
    "GB.OVT.DOUBLE_WHITE",
}


def test_batch3_rule_fixtures_validate_and_cover_pass_minor_serious() -> None:
    rules_data = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    valid_rule_ids = {r["id"] for r in rules_data["rules"]}

    fixture_files = sorted(FIXTURES_DIR.glob("GB.*.json"))
    assert {p.stem for p in fixture_files} >= BATCH_3_RULES

    for path in fixture_files:
        doc = validate_rulefixture_file(path)
        rid = doc["ruleId"]
        assert rid == path.stem
        assert rid in valid_rule_ids, f"Unknown ruleId {rid}"

        kinds = [d["kind"] for d in doc["drives"]]
        assert kinds.count("pass") >= 2, f"{rid}: need at least 2 pass drives"
        assert kinds.count("minor") >= 1, f"{rid}: need at least 1 minor drive"
        assert any(k in ("serious", "dangerous") for k in kinds), (
            f"{rid}: need at least 1 serious or dangerous drive"
        )
        for drive in doc["drives"]:
            for ev in drive["expectedEvents"]:
                assert ev["ruleId"] in valid_rule_ids
                if drive["kind"] == "pass":
                    assert ev["outcome"] == "pass"
                    assert ev["severity"] == "none"
                else:
                    assert ev["outcome"] == "fault"
                    assert ev["severity"] in ("Driving", "Serious", "Dangerous")
