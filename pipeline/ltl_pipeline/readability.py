"""Readability and completeness linter for player-facing strings and lessons."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.schema import REPO_ROOT

DEFAULT_STRINGS_RULES = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "rules.json"
DEFAULT_GLOSSARY = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "glossary.json"
DEFAULT_BANNED = (
    REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "banned_phrases.txt"
)
DEFAULT_RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"

# Match uppercase abbreviations (2+ chars, or H1/H2/H3) outside ICU {placeholders}
ABBREV_RE = re.compile(r"\b(?:[A-Z]{2,}|H[1-3])\b")
ICU_PLACEHOLDER_RE = re.compile(r"\{[^}]+\}")


def load_banned_phrases(path: Path = DEFAULT_BANNED) -> list[str]:
    """Load non-empty, non-comment banned phrases (lowercased)."""
    lines = path.read_text(encoding="utf-8").splitlines()
    return [ln.strip().lower() for ln in lines if ln.strip() and not ln.strip().startswith("#")]


def split_sentences(text: str) -> list[str]:
    """Split text into sentences without breaking on decimal numbers like 1.5."""
    cleaned = ICU_PLACEHOLDER_RE.sub("VALUE", text.strip())
    parts = re.split(r"(?<!\d)[.!?]+(?:\s+|$)", cleaned)
    return [p.strip() for p in parts if p.strip()]


def count_words(sentence: str) -> int:
    """Count words in a sentence."""
    return len([w for w in re.split(r"\s+", sentence.strip()) if w])


def extract_glossary_abbreviations(glossary_data: dict[str, Any]) -> set[str]:
    """Collect allowed abbreviations from glossary terms and IDs."""
    allowed: set[str] = set()
    for entry in glossary_data.get("terms", []):
        term = entry.get("term", "")
        for m in ABBREV_RE.finditer(term):
            allowed.add(m.group(0))
        # Also allow exact term if it's an abbreviation
        if ABBREV_RE.fullmatch(term.strip()):
            allowed.add(term.strip())
    return allowed


def check_text_readability(
    text: str,
    context: str,
    banned_phrases: list[str],
    allowed_abbrevs: set[str],
    max_words_per_sentence: int = 15,
) -> list[str]:
    """Return a list of readability violation messages for a single string."""
    errors: list[str] = []

    for sent in split_sentences(text):
        wc = count_words(sent)
        if wc > max_words_per_sentence:
            errors.append(
                f"{context}: sentence has {wc} words (max {max_words_per_sentence}): {sent!r}"
            )

    lowered = text.lower()
    for phrase in banned_phrases:
        if phrase in lowered:
            errors.append(f"{context}: contains banned phrase {phrase!r}")

    stripped_icu = ICU_PLACEHOLDER_RE.sub("", text)
    for m in ABBREV_RE.finditer(stripped_icu):
        abbr = m.group(0)
        if abbr not in allowed_abbrevs:
            errors.append(f"{context}: abbreviation {abbr!r} is not in the glossary")

    return errors


def lint_rule_strings(
    rules_json: dict[str, Any],
    glossary_data: dict[str, Any],
    rules_yaml: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check all rule strings for readability, glossary abbreviations, and Phase P coverage."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    strings_map = rules_json.get("rules", {})

    # Check Phase P rules are all present
    for rule in rules_yaml.get("rules", []):
        if rule.get("phase") == "P" and rule["id"] not in strings_map:
            errors.append(f"{rule['id']}: Phase P rule is missing from strings/en/rules.json")

    for rule_id, entry in sorted(strings_map.items()):
        for field_name in ("title", "fault", "why", "tip", "good"):
            val = entry.get(field_name)
            if not val:
                continue
            errors.extend(
                check_text_readability(
                    val,
                    context=f"{rule_id}.{field_name}",
                    banned_phrases=banned_phrases,
                    allowed_abbrevs=allowed_abbrevs,
                )
            )
        if count_words(entry["title"]) > 5:
            errors.append(f"{rule_id}.title: exceeds 5 words ({entry['title']!r})")
        if count_words(entry["fault"]) > 12:
            errors.append(f"{rule_id}.fault: exceeds 12 words ({entry['fault']!r})")
        if len(split_sentences(entry["why"])) > 2:
            errors.append(f"{rule_id}.why: exceeds 2 sentences")
        if len(split_sentences(entry["tip"])) > 1:
            errors.append(f"{rule_id}.tip: exceeds 1 sentence")

    return errors


def lint_glossary(
    glossary_data: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check glossary definitions for readability and abbreviation coverage."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    for entry in glossary_data.get("terms", []):
        eid = entry.get("id", "<unknown>")
        defn = entry.get("definition", "")
        errors.extend(
            check_text_readability(
                defn,
                context=f"glossary.{eid}",
                banned_phrases=banned_phrases,
                allowed_abbrevs=allowed_abbrevs,
            )
        )
        if len(split_sentences(defn)) > 2:
            errors.append(f"glossary.{eid}: definition exceeds 2 sentences")
    return errors


def lint_all_strings(
    rules_json_path: Path = DEFAULT_STRINGS_RULES,
    glossary_path: Path = DEFAULT_GLOSSARY,
    rules_yaml_path: Path = DEFAULT_RULES_YAML,
    banned_path: Path = DEFAULT_BANNED,
) -> list[str]:
    """Run all readability checks on GB English strings and glossary."""
    rules_json = json.loads(rules_json_path.read_text(encoding="utf-8"))
    glossary_data = json.loads(glossary_path.read_text(encoding="utf-8"))
    rules_yaml = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    banned = load_banned_phrases(banned_path)

    errors = lint_rule_strings(rules_json, glossary_data, rules_yaml, banned)
    errors.extend(lint_glossary(glossary_data, banned))
    return errors


if __name__ == "__main__":
    found_errors = lint_all_strings()
    if found_errors:
        for err in found_errors:
            print("ERROR:", err)
        raise SystemExit(1)
    print("Readability check passed with 0 errors.")
