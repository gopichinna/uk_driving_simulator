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
            kfs = drive["playerKeyframes"]
            t_min, t_max = kfs[0]["t"], kfs[-1]["t"]
            for i in range(1, len(kfs)):
                a, b = kfs[i - 1], kfs[i]
                dt = b["t"] - a["t"]
                assert dt > 0, f"{rid}/{drive['id']}: non-increasing keyframe time"
                dist = ((b["x"] - a["x"]) ** 2 + (b["z"] - a["z"]) ** 2) ** 0.5
                avg_v = (a["speedMps"] + b["speedMps"]) / 2.0
                expected_dist = avg_v * dt
                if max(dist, expected_dist) > 0.5:
                    assert abs(dist - expected_dist) <= 0.15 * max(dist, expected_dist), (
                        f"{rid}/{drive['id']}: speed/distance mismatch at t={b['t']} "
                        f"(dist={dist:.2f}m vs avg_v*dt={expected_dist:.2f}m)"
                    )
                dh = abs((b["headingDeg"] - a["headingDeg"] + 180.0) % 360.0 - 180.0)
                assert dh <= 120.0, f"{rid}/{drive['id']}: heading jump {dh:.1f}deg > 120"

            kind_to_sev = {
                "pass": "none",
                "minor": "Driving",
                "serious": "Serious",
                "dangerous": "Dangerous",
            }
            for ev in drive["expectedEvents"]:
                assert ev["ruleId"] in valid_rule_ids
                assert t_min <= ev["timeS"] <= t_max, (
                    f"{rid}/{drive['id']}: timeS {ev['timeS']} outside [{t_min}, {t_max}]"
                )
                assert ev["severity"] == kind_to_sev[drive["kind"]], (
                    f"{rid}/{drive['id']}: kind {drive['kind']} != severity {ev['severity']}"
                )
                if drive["kind"] == "pass":
                    assert ev["outcome"] == "pass"
                else:
                    assert ev["outcome"] == "fault"
