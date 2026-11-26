"""Tests for Round 21 theory-style quiz bank."""

from __future__ import annotations

import json

import yaml

from ltl_pipeline.quiz_bank import BANK_DIR, RULES_YAML, validate_bank_question
from ltl_pipeline.readability import load_banned_phrases


def test_every_implemented_rule_has_four_valid_bank_questions() -> None:
    rules_data = yaml.safe_load(RULES_YAML.read_text(encoding="utf-8"))
    implemented = [
        r["id"]
        for r in rules_data["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    ]
    banned = load_banned_phrases()
    assert (BANK_DIR / "INDEX.md").is_file()

    for rid in implemented:
        path = BANK_DIR / f"{rid}.json"
        assert path.is_file(), f"Missing quiz bank file for {rid}: {path}"
        items = json.loads(path.read_text(encoding="utf-8"))
        assert len(items) >= 4, f"{rid} has only {len(items)} questions"
        for q in items:
            assert q["rule"] == rid
            assert validate_bank_question(q, banned) == []
