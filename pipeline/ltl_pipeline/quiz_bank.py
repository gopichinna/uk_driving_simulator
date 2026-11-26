"""Validate Round 21 theory-style quiz bank under content/quizzes/gb/bank/ and write INDEX.md."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.readability import load_banned_phrases
from ltl_pipeline.schema import REPO_ROOT

RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
BANK_DIR = REPO_ROOT / "content" / "quizzes" / "gb" / "bank"
TESTS_DIR = REPO_ROOT / "content" / "quizzes" / "gb" / "tests"


def validate_bank_question(item: dict[str, Any], banned: list[str]) -> list[str]:
    """Validate one theory-bank question against R21 constraints."""
    errs: list[str] = []
    required = {"id", "rule", "question", "options", "answer", "explain", "hc_rule", "difficulty"}
    if set(item.keys()) != required:
        errs.append(f"{item.get('id')}: keys {set(item.keys())} != {required}")
        return errs
    if not isinstance(item["options"], list) or len(item["options"]) != 4:
        errs.append(f"{item['id']}: options must be 4 strings")
    if not isinstance(item["answer"], int) or not (0 <= item["answer"] <= 3):
        errs.append(f"{item['id']}: answer {item['answer']} out of 0..3")
    if not isinstance(item["difficulty"], int) or not (1 <= item["difficulty"] <= 3):
        errs.append(f"{item['id']}: difficulty {item['difficulty']} out of 1..3")
    words = item["question"].split()
    if len(words) > 25:
        errs.append(f"{item['id']}: question has {len(words)} words (> 25)")
    if item["hc_rule"] not in item["explain"]:
        errs.append(f"{item['id']}: explain does not quote hc_rule {item['hc_rule']}")

    texts = [item["question"], *item["options"], item["explain"]]
    for txt in texts:
        low = txt.lower()
        for phrase in banned:
            if re.search(rf"\b{re.escape(phrase)}\b", low):
                errs.append(f"{item['id']}: banned jargon '{phrase}' in '{txt}'")
    return errs


def write_quiz_bank_index(
    bank_dir: Path = BANK_DIR,
    rules_yaml_path: Path = RULES_YAML,
) -> Path:
    """Validate every implemented rule's quiz bank JSON and write INDEX.md."""
    rules_data = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    implemented = [
        r["id"]
        for r in rules_data["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    ]
    banned = load_banned_phrases()

    lines = [
        "# Theory-Style Quiz Bank Index (GB)",
        "",
        "Generated from `content/countries/gb/rules.yaml` (implemented rules only).",
        "",
        "| Rule ID | Questions | Highway Code |",
        "|---|---|---|",
    ]
    total = 0
    for rid in implemented:
        path = bank_dir / f"{rid}.json"
        items = json.loads(path.read_text(encoding="utf-8"))
        for q in items:
            errs = validate_bank_question(q, banned)
            assert errs == [], f"Invalid bank question: {errs}"
        hc = ", ".join(sorted({q["hc_rule"] for q in items}))
        lines.append(f"| `{rid}` | {len(items)} | Rule {hc} |")
        total += len(items)

    lines.extend(["", f"**Total:** {len(implemented)} implemented rules, {total} questions.", ""])
    index_path = bank_dir / "INDEX.md"
    index_path.write_text("\n".join(lines), encoding="utf-8")
    return index_path


def build_theory_test_sets(
    bank_dir: Path = BANK_DIR,
    tests_dir: Path = TESTS_DIR,
    num_sets: int = 10,
) -> list[Path]:
    """Build 10 balanced 50-question theory mock-test sets (40% easy, 40% med, 20% hard)."""
    tests_dir.mkdir(parents=True, exist_ok=True)
    all_questions: list[dict[str, Any]] = []
    for p in sorted(bank_dir.glob("GB.*.json")):
        all_questions.extend(json.loads(p.read_text(encoding="utf-8")))

    by_diff: dict[int, list[dict[str, Any]]] = {1: [], 2: [], 3: []}
    for q in all_questions:
        by_diff[q["difficulty"]].append(q)

    # Deterministic round-robin with usage cap <= 3 and per-set topic cap <= 7 (14% <= 15%)
    usage: dict[str, int] = {q["id"]: 0 for q in all_questions}
    targets = {1: 20, 2: 20, 3: 10}
    out_paths: list[Path] = []

    for set_idx in range(1, num_sets + 1):
        chosen: list[dict[str, Any]] = []
        rule_counts: dict[str, int] = {}
        seen_ids: set[str] = set()

        for diff, need in targets.items():
            pool = sorted(
                by_diff[diff],
                key=lambda q: (
                    usage[q["id"]],
                    rule_counts.get(q["rule"], 0),
                    (hash(f"{set_idx}:{q['id']}") if False else (set_idx * 7 + len(q["id"]))),
                    q["id"],
                ),
            )
            picked = 0
            # Multi-pass: prefer lowest usage & lowest rule count
            while picked < need:
                candidates = [
                    q
                    for q in pool
                    if q["id"] not in seen_ids
                    and usage[q["id"]] < 3
                    and rule_counts.get(q["rule"], 0) < 7
                ]
                assert candidates, f"Ran out of candidates for set {set_idx} diff {diff}"
                candidates.sort(key=lambda q: (usage[q["id"]], rule_counts.get(q["rule"], 0)))
                q = candidates[0]
                chosen.append(q)
                seen_ids.add(q["id"])
                usage[q["id"]] += 1
                rule_counts[q["rule"]] = rule_counts.get(q["rule"], 0) + 1
                picked += 1

        doc = {
            "theoryTestFormatVersion": "0.1",
            "id": f"gb-theory-set-{set_idx:02d}",
            "setNumber": set_idx,
            "passMark": 43,
            "questionCount": len(chosen),
            "timeLimitMinutes": 57,
            "questions": chosen,
        }
        out_path = tests_dir / f"set-{set_idx:02d}.json"
        out_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
        out_paths.append(out_path)

    return out_paths


if __name__ == "__main__":
    idx = write_quiz_bank_index()
    sets = build_theory_test_sets()
    print(f"Wrote {idx} and {len(sets)} theory test sets")
