"""Generate docs/CONTENT_HEALTH.md summarising content coverage, gaps, and asset health."""

from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.readability import lint_all_strings
from ltl_pipeline.schema import REPO_ROOT

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
QUIZZES_DIR = REPO_ROOT / "content" / "quizzes" / "gb"
RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
STRINGS_EN = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en"
STRINGS_XA = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en-XA"
AUDIO_DIR = REPO_ROOT / "content" / "audio"
OUT_PATH = REPO_ROOT / "docs" / "CONTENT_HEALTH.md"


def _flatten_keys(obj: Any, prefix: str = "") -> set[str]:
    keys: set[str] = set()
    if isinstance(obj, dict):
        for k, v in obj.items():
            full = f"{prefix}.{k}" if prefix else str(k)
            if isinstance(v, (dict, list)):
                keys.update(_flatten_keys(v, full))
            else:
                keys.add(full)
    elif isinstance(obj, list):
        for idx, v in enumerate(obj):
            keys.update(_flatten_keys(v, f"{prefix}[{idx}]"))
    return keys


def generate_health_report(repo_root: Path = REPO_ROOT) -> str:
    """Build the Markdown content health report."""
    lessons_dir = repo_root / "content" / "lessons" / "gb"
    quizzes_dir = repo_root / "content" / "quizzes" / "gb"
    rules_yaml = repo_root / "content" / "countries" / "gb" / "rules.yaml"
    strings_en = repo_root / "content" / "countries" / "gb" / "strings" / "en"
    strings_xa = repo_root / "content" / "countries" / "gb" / "strings" / "en-XA"
    audio_dir = repo_root / "content" / "audio"

    stage_stats: dict[int, dict[str, int]] = defaultdict(
        lambda: {"lessons": 0, "scenarios": 0, "quiz_questions": 0, "voice_lines": 0, "badges": 0}
    )
    rule_practice_counts: dict[str, int] = defaultdict(int)
    referenced_rel_paths: set[str] = set()

    for lf in sorted(lessons_dir.glob("[0-9][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        st = int(lesson["stage"])
        stage_stats[st]["lessons"] += 1
        stage_stats[st]["scenarios"] += len(lesson["scenarios"])
        badge_png = lessons_dir / "badges" / f"{lesson['id']}.png"
        if badge_png.is_file():
            stage_stats[st]["badges"] += 1
            referenced_rel_paths.add(f"content/lessons/gb/badges/{lesson['id']}.png")
            referenced_rel_paths.add(f"content/lessons/gb/badges/{lesson['id']}.svg")
        diag = lesson["learnCard"].get("diagram")
        if diag:
            referenced_rel_paths.add(diag)
            referenced_rel_paths.add(str(Path(diag).with_suffix(".svg")))
        if lesson["id"] != "gb-s8-19-mock-driving-test":
            for sc in lesson["scenarios"]:
                for rid in sc["rulesJudged"]:
                    rule_practice_counts[rid] += 1

    for qf in sorted(quizzes_dir.glob("[0-9][0-9]-*.json")):
        quiz = json.loads(qf.read_text(encoding="utf-8"))
        st = int(quiz["stage"])
        stage_stats[st]["quiz_questions"] += len(quiz.get("questions", []))

    voice_manifest = json.loads(
        (audio_dir / "voice" / "en" / "manifest.json").read_text(encoding="utf-8")
    )
    voice_json = json.loads((strings_en / "voice.json").read_text(encoding="utf-8"))
    for entry in voice_manifest["entries"]:
        referenced_rel_paths.add(f"content/audio/voice/en/{entry['file']}")
    sfx_manifest = json.loads((audio_dir / "sfx" / "manifest.json").read_text(encoding="utf-8"))
    for entry in sfx_manifest["entries"]:
        referenced_rel_paths.add(f"content/audio/sfx/{entry['file']}")

    # Attribute voice lines per stage by rule or lesson prefix
    rules_data = yaml.safe_load(rules_yaml.read_text(encoding="utf-8"))["rules"]
    implemented_rules = [
        r["id"] for r in rules_data if r.get("implementation", {}).get("status") == "implemented"
    ]
    rule_stage: dict[str, int] = {}
    for lf in sorted(lessons_dir.glob("[0-9][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        for sc in lesson["scenarios"]:
            for rid in sc["rulesJudged"]:
                rule_stage.setdefault(rid, int(lesson["stage"]))
    for key in voice_json["entries"]:
        parts = key.split(".")
        if len(parts) >= 4 and parts[0] == "voice":
            rid = ".".join(parts[1:-1])
            st = rule_stage.get(rid, 0)
            stage_stats[st]["voice_lines"] += 1

    # Rules with < 2 practice drives
    under_practised = [
        (rid, rule_practice_counts.get(rid, 0))
        for rid in implemented_rules
        if rule_practice_counts.get(rid, 0) < 2
    ]

    # Readability errors
    readability_errors = lint_all_strings()

    # Unreferenced sounds or images
    unreferenced: list[str] = []
    for folder in (
        audio_dir / "voice" / "en",
        audio_dir / "sfx",
        lessons_dir / "diagrams",
        lessons_dir / "badges",
    ):
        if not folder.exists():
            continue
        for p in sorted(folder.rglob("*")):
            if not p.is_file() or p.suffix not in (".wav", ".png", ".svg"):
                continue
            if p.name == "contact-sheet.png":
                continue
            rel = p.relative_to(repo_root).as_posix()
            if rel not in referenced_rel_paths:
                unreferenced.append(rel)

    # Strings in en missing in en-XA
    missing_xa: list[str] = []
    for en_file in sorted(strings_en.glob("*.json")):
        xa_file = strings_xa / en_file.name
        if not xa_file.is_file():
            missing_xa.append(f"Missing file: en-XA/{en_file.name}")
            continue
        en_keys = _flatten_keys(json.loads(en_file.read_text(encoding="utf-8")))
        xa_keys = _flatten_keys(json.loads(xa_file.read_text(encoding="utf-8")))
        for k in sorted(en_keys - xa_keys):
            missing_xa.append(f"{en_file.name}: {k}")

    # Files over 5 MB
    large_files: list[tuple[str, float]] = []
    for p in sorted((repo_root / "content").rglob("*")):
        if p.is_file():
            mb = p.stat().st_size / (1024 * 1024)
            if mb > 5.0:
                large_files.append((p.relative_to(repo_root).as_posix(), round(mb, 2)))

    # Planned rules count
    planned_rules = [
        r["id"] for r in rules_data if r.get("implementation", {}).get("status") != "implemented"
    ]

    lines = [
        "# Content Health Report",
        "",
        "Generated by `python -m ltl_pipeline.health`.",
        "",
        "## Per-Stage Summary",
        "",
        "| Stage | Lessons | Scenarios | Quiz Questions | Coach Voice Lines | Badges |",
        "|---|---|---|---|---|---|",
    ]
    for st in sorted(stage_stats):
        row = stage_stats[st]
        lines.append(
            f"| {st} | {row['lessons']} | {row['scenarios']} | "
            f"{row['quiz_questions']} | {row['voice_lines']} | {row['badges']} |"
        )

    lines.extend(
        [
            "",
            "## Implemented Rules with Fewer Than 2 Practice Drives",
            "",
            *(
                [f"- `{rid}`: {cnt} practice drive(s)" for rid, cnt in under_practised]
                or ["- None (all implemented rules have >= 2 practice drives)."]
            ),
            "",
            "## Readability Issues",
            "",
            *([f"- {err}" for err in readability_errors] or ["- None (0 readability errors)."]),
            "",
            "## Unreferenced Sounds or Images",
            "",
            *([f"- `{rel}`" for rel in unreferenced[:30]] or ["- None."]),
            "",
            "## Strings in `en` Missing in `en-XA`",
            "",
            *([f"- `{item}`" for item in missing_xa[:30]] or ["- None."]),
            "",
            "## Files Over 5 MB",
            "",
            *(
                [f"- `{rel}` ({mb} MB)" for rel, mb in large_files]
                or ["- None (all files under `content/` are <= 5 MB)."]
            ),
            "",
            "## Top 10 Gaps (Priority Order)",
            "",
            (
                f"1. **Unimplemented rules in `rules.yaml`:** {len(planned_rules)} planned"
                " rules still need `Rules.Core` implementations."
            ),
            f"2. **Implemented rules < 2 practice drives:** {len(under_practised)} rule(s).",
            f"3. **Readability linter errors:** {len(readability_errors)} issue(s).",
            f"4. **Missing `en-XA` strings:** {len(missing_xa)} key(s).",
            f"5. **Unreferenced audio/image assets:** {len(unreferenced)} file(s).",
            f"6. **Oversized content files (> 5 MB):** {len(large_files)} file(s).",
            (
                "7. **Stage 6 & 7 quiz banks:** theory-bank questions currently cover"
                " implemented rules only."
            ),
            (
                "8. **Store screenshots & feature graphic:** capture final 16:9 / 9:16"
                " shots from Unity."
            ),
            (
                "9. **Owner compliance placeholders:** fill `<owner email>` and privacy"
                " policy URL before Play upload."
            ),
            (
                "10. **Non-English localisations:** v1 ships English (`en`) +"
                " pseudo-locale (`en-XA`) only."
            ),
            "",
        ]
    )
    return "\n".join(lines)


def write_health_report(out_path: Path = OUT_PATH) -> Path:
    """Write docs/CONTENT_HEALTH.md."""
    out_path.write_text(generate_health_report(), encoding="utf-8")
    return out_path


if __name__ == "__main__":
    written = write_health_report()
    print(f"Wrote {written}")
