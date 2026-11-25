"""Generate content/lessons/gb/IMPLEMENTATION.md from lessons and rules.yaml."""

from __future__ import annotations

import json
from pathlib import Path

import yaml

from ltl_pipeline.schema import REPO_ROOT

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
OUT_PATH = LESSONS_DIR / "IMPLEMENTATION.md"


def generate_implementation_report(
    lessons_dir: Path = LESSONS_DIR,
    rules_yaml_path: Path = RULES_YAML,
) -> str:
    """Build a Markdown table of each lesson's rulesJudged and implementation status."""
    rules_data = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    implemented = {
        r["id"]
        for r in rules_data["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    }

    lines = [
        "# Lesson Rule Implementation Report",
        "",
        "Generated from `content/lessons/gb/*.json` and `content/countries/gb/rules.yaml`.",
        "- ✅ = implemented in `core/Rules.Core`",
        "- ⏳ = planned (not yet implemented in `core/Rules.Core`)",
        "",
        "| Order | Lesson | Rule ID | Status |",
        "|---|---|---|---|",
    ]

    for lf in sorted(lessons_dir.glob("*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        seen: list[str] = []
        for sc in lesson["scenarios"]:
            for rid in sc["rulesJudged"]:
                if rid not in seen:
                    seen.append(rid)
        for rid in seen:
            status = "✅" if rid in implemented else "⏳"
            lines.append(f"| {lesson['order']} | {lesson['title']} | `{rid}` | {status} |")

    lines.append("")
    return "\n".join(lines)


def write_implementation_report(out_path: Path = OUT_PATH) -> Path:
    """Write IMPLEMENTATION.md to disk."""
    out_path.write_text(generate_implementation_report(), encoding="utf-8")
    return out_path


if __name__ == "__main__":
    written = write_implementation_report()
    print(f"Wrote {written}")
