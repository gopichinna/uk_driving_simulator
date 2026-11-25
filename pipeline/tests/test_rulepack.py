"""Tests for GB rule pack generation and validation."""

from __future__ import annotations

import re

from ltl_pipeline.rulepack import (
    CATALOGUE_PATH,
    RULE_ID_RE,
    RULES_YAML_PATH,
    parse_catalogue,
    scan_implemented_rules,
    verify_code_rules_in_yaml,
)
from ltl_pipeline.schema import validate_rulepack_file


def test_rules_yaml_validates_against_schema() -> None:
    data = validate_rulepack_file(RULES_YAML_PATH)
    assert data["country"] == "GB"
    assert len(data["rules"]) == 130


def test_every_catalogue_row_in_output() -> None:
    text = CATALOGUE_PATH.read_text(encoding="utf-8")
    catalogue_ids = re.findall(r"^\|\s*(GB\.[A-Z]+\.[A-Z0-9_]+)\s*\|", text, flags=re.MULTILINE)
    pack = parse_catalogue()
    output_ids = [r["id"] for r in pack["rules"]]
    assert output_ids == catalogue_ids
    assert len(output_ids) == len(set(output_ids))


def test_rule_fields_and_sources() -> None:
    pack = parse_catalogue()
    for rule in pack["rules"]:
        assert RULE_ID_RE.match(rule["id"])
        assert rule["force"] in {"MUST", "should", "info"}
        if rule["verified"] != "unverified":
            url = rule["source"].get("url", "")
            assert url.startswith("https://") and "gov.uk" in url


def test_implemented_code_rules_present() -> None:
    verify_code_rules_in_yaml()
    implemented = scan_implemented_rules()
    assert "GB.RBT.GIVE_WAY_RIGHT" in implemented
    assert "GB.SPD.OVER_LIMIT" in implemented
    pack = parse_catalogue()
    by_id = {r["id"]: r for r in pack["rules"]}
    for rid in implemented:
        assert by_id[rid]["implementation"]["status"] == "implemented"


def test_regenerable_deterministic() -> None:
    p1 = parse_catalogue()
    p2 = parse_catalogue()
    assert p1 == p2
