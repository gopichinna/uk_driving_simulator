"""Tests for Round 21 theory-style quiz bank."""

from __future__ import annotations

import json
from collections import Counter

import yaml

from ltl_pipeline.quiz_bank import BANK_DIR, RULES_YAML, TESTS_DIR, validate_bank_question
from ltl_pipeline.readability import load_banned_phrases


def test_every_implemented_rule_has_eight_valid_bank_questions() -> None:
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
        assert len(items) >= 8, f"{rid} has only {len(items)} questions"
        for q in items:
            assert q["rule"] == rid
            assert validate_bank_question(q, banned) == []


def test_ten_balanced_theory_test_sets() -> None:
    assert (TESTS_DIR / "README.md").is_file()
    global_usage: Counter[str] = Counter()
    for i in range(1, 11):
        p = TESTS_DIR / f"set-{i:02d}.json"
        assert p.is_file(), f"Missing theory test set: {p}"
        doc = json.loads(p.read_text(encoding="utf-8"))
        assert doc["passMark"] == 43
        assert doc["timeLimitMinutes"] == 57
        qs = doc["questions"]
        assert len(qs) == 50
        ids = [q["id"] for q in qs]
        assert len(set(ids)) == 50, f"{p.name}: duplicate question in set"
        global_usage.update(ids)
        topic_counts = Counter(q["rule"] for q in qs)
        assert max(topic_counts.values()) <= 7, f"{p.name}: topic > 15%: {topic_counts}"
        diff_counts = Counter(q["difficulty"] for q in qs)
        assert diff_counts == {1: 20, 2: 20, 3: 10}, f"{p.name}: bad mix {diff_counts}"

    assert max(global_usage.values()) <= 3, "Question used in > 3 sets"
