"""Tests for rule -> lesson index generation and implemented-rule coverage."""

from __future__ import annotations

import yaml

from ltl_pipeline.rule_index import OUT_PATH, RULES_YAML, build_rule_index
from ltl_pipeline.schema import validate_ruleindex_file


def test_rule_index_validates_and_covers_implemented_rules() -> None:
    data = validate_ruleindex_file(OUT_PATH)
    assert data == build_rule_index()

    rules_yaml = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    implemented = [
        r["id"]
        for r in rules_yaml["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    ]
    missing = [rid for rid in implemented if not data["rules"].get(rid)]
    assert missing == [], f"Implemented rules with no training lesson: {missing}"
