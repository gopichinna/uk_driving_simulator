"""Localisation readiness checker: locale parity, placeholders, button lengths, and raw text."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path

from ltl_pipeline.schema import REPO_ROOT

STRINGS_ROOT = REPO_ROOT / "content" / "countries" / "gb" / "strings"
LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
SCENARIOS_DIR = REPO_ROOT / "content" / "scenarios" / "gb"

PLACEHOLDER_RE = re.compile(r"\{[a-zA-Z0-9_]+\}")
BUTTON_KEY_RE = re.compile(
    r"(?:^|\.)(?:btn(?:_|\.)|cta$|button$|skip$|back$|next$|continue$|close$)"
)


@dataclass
class L10nReport:
    missing_keys: dict[str, list[str]] = field(default_factory=dict)
    extra_keys: dict[str, list[str]] = field(default_factory=dict)
    placeholder_mismatches: dict[str, list[str]] = field(default_factory=dict)
    long_buttons: list[str] = field(default_factory=list)
    hardcoded_content_fields: list[str] = field(default_factory=list)


def _load_locale_entries(locale_dir: Path) -> dict[str, dict[str, str]]:
    out: dict[str, dict[str, str]] = {}
    for p in sorted(locale_dir.glob("*.json")):
        data = json.loads(p.read_text(encoding="utf-8"))
        out[p.name] = {str(k): str(v) for k, v in data.get("entries", {}).items()}
    return out


def check_localisation(
    strings_root: Path = STRINGS_ROOT,
    lessons_dir: Path = LESSONS_DIR,
    scenarios_dir: Path = SCENARIOS_DIR,
) -> L10nReport:
    """Audit all locales against `en` and scan content/ for non-string-table English text."""
    report = L10nReport()
    en_dir = strings_root / "en"
    en_files = _load_locale_entries(en_dir)

    # Check en button lengths
    ui_entries = en_files.get("ui.json", {})
    for key, val in sorted(ui_entries.items()):
        if BUTTON_KEY_RE.search(key) and len(val) > 40:
            report.long_buttons.append(f"en/ui.json:{key} ({len(val)} chars): {val!r}")

    # Check every other locale against en
    for loc_dir in sorted(strings_root.iterdir()):
        if not loc_dir.is_dir() or loc_dir.name == "en":
            continue
        loc_name = loc_dir.name
        loc_files = _load_locale_entries(loc_dir)
        missing: list[str] = []
        extra: list[str] = []
        ph_bad: list[str] = []

        for fname, en_entries in en_files.items():
            target = loc_files.get(fname, {})
            for k, en_val in en_entries.items():
                if k not in target:
                    missing.append(f"{fname}:{k}")
                else:
                    en_ph = sorted(PLACEHOLDER_RE.findall(en_val))
                    loc_ph = sorted(PLACEHOLDER_RE.findall(target[k]))
                    if en_ph != loc_ph:
                        ph_bad.append(f"{fname}:{k} (en={en_ph}, {loc_name}={loc_ph})")
            for k in target:
                if k not in en_entries:
                    extra.append(f"{fname}:{k}")

        if missing:
            report.missing_keys[loc_name] = missing
        if extra:
            report.extra_keys[loc_name] = extra
        if ph_bad:
            report.placeholder_mismatches[loc_name] = ph_bad

    # Scan content/lessons and content/scenarios for hardcoded English outside strings/
    for lf in sorted(lessons_dir.glob("[0-9][0-9]-*.json")):
        data = json.loads(lf.read_text(encoding="utf-8"))
        if data.get("title"):
            report.hardcoded_content_fields.append(f"lessons/{lf.name}:title")
        for idx, _ in enumerate(data.get("learnCard", {}).get("blocks", [])):
            report.hardcoded_content_fields.append(f"lessons/{lf.name}:learnCard.blocks[{idx}]")
        for sc in data.get("scenarios", []):
            if sc.get("goal"):
                report.hardcoded_content_fields.append(f"lessons/{lf.name}:{sc['id']}.goal")

    quiz_dir = lessons_dir / "quiz"
    if quiz_dir.is_dir():
        for qf in sorted(quiz_dir.glob("*.json")):
            qdata = json.loads(qf.read_text(encoding="utf-8"))
            if qdata.get("questions"):
                report.hardcoded_content_fields.append(
                    f"lessons/quiz/{qf.name}:{len(qdata['questions'])} questions"
                )

    for sf in sorted(scenarios_dir.glob("*.json")):
        sdata = json.loads(sf.read_text(encoding="utf-8"))
        for ev in sdata.get("events", []):
            if ev.get("instruction"):
                report.hardcoded_content_fields.append(
                    f"scenarios/{sf.name}:event.instruction"
                )

    return report


def format_report(report: L10nReport) -> str:
    lines = ["# Localisation Readiness Report", ""]
    lines.append(f"- Missing keys locales: {len(report.missing_keys)}")
    for loc, items in sorted(report.missing_keys.items()):
        lines.append(f"  - `{loc}`: {len(items)} missing ({', '.join(items[:5])})")
    lines.append(f"- Extra keys locales: {len(report.extra_keys)}")
    for loc, items in sorted(report.extra_keys.items()):
        lines.append(f"  - `{loc}`: {len(items)} extra ({', '.join(items[:5])})")
    lines.append(f"- Placeholder mismatches: {len(report.placeholder_mismatches)}")
    for loc, items in sorted(report.placeholder_mismatches.items()):
        lines.append(f"  - `{loc}`: {items[:5]}")
    lines.append(f"- Long button strings (> 40 chars): {len(report.long_buttons)}")
    for item in report.long_buttons[:10]:
        lines.append(f"  - {item}")
    lines.append(
        f"- Hardcoded English fields outside `strings/` (lessons/quizzes/scenarios): "
        f"{len(report.hardcoded_content_fields)}"
    )
    return "\n".join(lines)


if __name__ == "__main__":
    print(format_report(check_localisation()))
