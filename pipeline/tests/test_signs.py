"""Tests for GB traffic-sign catalogue validation."""

from __future__ import annotations

import yaml

from ltl_pipeline.schema import REPO_ROOT, validate_signs_file

SIGNS_YAML = REPO_ROOT / "content" / "countries" / "gb" / "signs.yaml"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"


def test_signs_yaml_validates_against_schema() -> None:
    data = validate_signs_file(SIGNS_YAML)
    assert data["country"] == "GB"
    assert data["licence"]["status"] == "verified"
    assert len(data["signs"]) >= 17


def test_signs_related_rules_and_images_exist() -> None:
    signs_data = yaml.safe_load(SIGNS_YAML.read_text(encoding="utf-8"))
    rules_data = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    rule_ids = {r["id"] for r in rules_data["rules"]}

    for sign in signs_data["signs"]:
        for rid in sign["relatedRules"]:
            assert rid in rule_ids, f"{sign['id']} references unknown rule {rid}"
        if sign["image"] is not None:
            img_path = REPO_ROOT / sign["image"]
            assert img_path.is_file(), f"Missing sign image: {img_path}"
