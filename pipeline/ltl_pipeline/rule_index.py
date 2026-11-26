"""Generate content/lessons/gb/rule_index.json mapping rules to lessons."""

from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.schema import REPO_ROOT, validate_ruleindex_file

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
OUT_PATH = LESSONS_DIR / "rule_index.json"


def build_rule_index(
    lessons_dir: Path = LESSONS_DIR,
    rules_yaml_path: Path = RULES_YAML,
) -> dict[str, Any]:
    """Build a deterministic ruleId -> [{lessonId, scenarioId, title}] index."""
    rules_data = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    implemented_ids = [
        r["id"]
        for r in rules_data["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    ]

    lessons: list[dict[str, Any]] = []
    for lf in sorted(lessons_dir.glob("[0-9][0-9]-*.json")):
        lessons.append(json.loads(lf.read_text(encoding="utf-8")))
    lessons.sort(key=lambda item: (item["stage"], item["order"], item["id"]))

    mapping: dict[str, list[dict[str, str]]] = defaultdict(list)
    for lesson in lessons:
        for sc in lesson["scenarios"]:
            for rid in sc["rulesJudged"]:
                mapping[rid].append(
                    {
                        "lessonId": lesson["id"],
                        "scenarioId": sc["id"],
                        "title": lesson["title"],
                    }
                )

    for rid in implemented_ids:
        mapping.setdefault(rid, [])

    ordered_rules = {rid: mapping[rid] for rid in sorted(mapping)}
    return {
        "ruleIndexVersion": "0.1",
        "country": "GB",
        "rules": ordered_rules,
    }


def write_rule_index(out_path: Path = OUT_PATH) -> Path:
    """Write and schema-validate content/lessons/gb/rule_index.json."""
    data = build_rule_index()
    out_path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    validate_ruleindex_file(out_path)
    return out_path


if __name__ == "__main__":
    written = write_rule_index()
    print(f"Wrote {written}")
