"""Readability and completeness linter for player-facing strings and lessons."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.schema import REPO_ROOT

DEFAULT_STRINGS_RULES = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "rules.json"
DEFAULT_STRINGS_UI = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "ui.json"
DEFAULT_STRINGS_VOICE = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "voice.json"
DEFAULT_GLOSSARY = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "glossary.json"
DEFAULT_BANNED = (
    REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "banned_phrases.txt"
)
DEFAULT_JARGON = (
    REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "jargon_phrases.txt"
)
DEFAULT_RULES_YAML = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
DEFAULT_ROUTES_JSON = REPO_ROOT / "content" / "cities" / "wolverhampton" / "routes.json"
DEFAULT_DIRECTION_SIGNS_JSON = (
    REPO_ROOT / "content" / "cities" / "wolverhampton" / "direction_signs.json"
)

# Match uppercase abbreviations (2+ chars, or H1/H2/H3) outside ICU {placeholders}
ABBREV_RE = re.compile(r"\b(?:[A-Z]{2,}|H[1-3])\b")
ICU_PLACEHOLDER_RE = re.compile(r"\{[^}]+\}")


def load_banned_phrases(path: Path = DEFAULT_BANNED) -> list[str]:
    """Load non-empty, non-comment banned phrases (lowercased)."""
    lines = path.read_text(encoding="utf-8").splitlines()
    return [ln.strip().lower() for ln in lines if ln.strip() and not ln.strip().startswith("#")]


def _strip_icu_blocks(text: str, replacement: str = "VALUE") -> str:
    """Replace top-level and nested ICU {placeholder} blocks with replacement."""
    out: list[str] = []
    depth = 0
    for ch in text:
        if ch == "{":
            if depth == 0:
                out.append(replacement)
            depth += 1
        elif ch == "}":
            depth = max(0, depth - 1)
        elif depth == 0:
            out.append(ch)
    return "".join(out)


def split_sentences(text: str) -> list[str]:
    """Split text into sentences without breaking on decimal numbers like 1.5."""
    cleaned = _strip_icu_blocks(text.strip(), "VALUE")
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


def load_jargon_phrases(path: Path = DEFAULT_JARGON) -> list[str]:
    """Load non-empty, non-comment jargon phrases (lowercased)."""
    if not path.is_file():
        return []
    lines = path.read_text(encoding="utf-8").splitlines()
    return [ln.strip().lower() for ln in lines if ln.strip() and not ln.strip().startswith("#")]


def check_text_readability(
    text: str,
    context: str,
    banned_phrases: list[str],
    allowed_abbrevs: set[str],
    max_words_per_sentence: int = 15,
    jargon_phrases: list[str] | None = None,
) -> list[str]:
    """Return a list of readability violation messages for a single string."""
    errors: list[str] = []

    if jargon_phrases:
        # Allow 'dual carriageway' as a standard UK road type; flag other jargon unless explained
        cleaned_jargon = re.sub(r"\bdual carriageway\b", "", text, flags=re.IGNORECASE)
        for jp in jargon_phrases:
            if re.search(rf"\b{re.escape(jp)}\b", cleaned_jargon, flags=re.IGNORECASE):
                errors.append(f"{context}: contains jargon phrase {jp!r} ({text!r})")

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

    stripped_icu = _strip_icu_blocks(text, "")
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


def lint_ui_strings(
    ui_json: dict[str, Any],
    glossary_data: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check UI strings for readability and glossary abbreviations."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    for key, val in sorted(ui_json.get("entries", {}).items()):
        errors.extend(
            check_text_readability(
                val,
                context=f"ui.{key}",
                banned_phrases=banned_phrases,
                allowed_abbrevs=allowed_abbrevs,
            )
        )
    return errors


def lint_route_instructions(
    routes_json: dict[str, Any],
    glossary_data: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check sat-nav instruction text for readability and glossary abbreviations."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    for route in routes_json.get("routes", []):
        rid = route.get("id", "<unknown>")
        for idx, inst in enumerate(route.get("instructions", [])):
            errors.extend(
                check_text_readability(
                    inst["text"],
                    context=f"routes.{rid}.instructions[{idx}]",
                    banned_phrases=banned_phrases,
                    allowed_abbrevs=allowed_abbrevs,
                )
            )
    return errors


def lint_voice_strings(
    voice_json: dict[str, Any],
    glossary_data: dict[str, Any],
    rules_yaml: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check coach voice lines (<= 12 words, B1 readability, implemented rule coverage)."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    entries = voice_json.get("entries", {})

    for req in ("voice.lesson.start", "voice.lesson.passed", "voice.lesson.try_again"):
        if req not in entries:
            errors.append(f"voice: missing required key {req}")

    for rule in rules_yaml.get("rules", []):
        if rule.get("implementation", {}).get("status") == "implemented":
            rid = rule["id"]
            for suffix in ("fault", "good"):
                k = f"voice.{rid}.{suffix}"
                if k not in entries:
                    errors.append(f"voice: missing key {k} for implemented rule")

    for key, val in sorted(entries.items()):
        errors.extend(
            check_text_readability(
                val,
                context=f"voice.{key}",
                banned_phrases=banned_phrases,
                allowed_abbrevs=allowed_abbrevs,
                max_words_per_sentence=12,
            )
        )
        if count_words(val) > 12:
            errors.append(f"voice.{key}: exceeds 12 words ({val!r})")
    return errors


def lint_direction_signs(
    signs_json: dict[str, Any],
    glossary_data: dict[str, Any],
    banned_phrases: list[str],
) -> list[str]:
    """Check direction sign row text for B1 readability."""
    errors: list[str] = []
    allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
    for sign in signs_json.get("signs", []):
        sid = sign.get("id", "<unknown>")
        for idx, row in enumerate(sign.get("rows", [])):
            errors.extend(
                check_text_readability(
                    row["text"],
                    context=f"direction_signs.{sid}.rows[{idx}]",
                    banned_phrases=banned_phrases,
                    allowed_abbrevs=allowed_abbrevs,
                )
            )
    return errors


def lint_all_strings(
    rules_json_path: Path = DEFAULT_STRINGS_RULES,
    glossary_path: Path = DEFAULT_GLOSSARY,
    rules_yaml_path: Path = DEFAULT_RULES_YAML,
    banned_path: Path = DEFAULT_BANNED,
    ui_json_path: Path = DEFAULT_STRINGS_UI,
    routes_json_path: Path = DEFAULT_ROUTES_JSON,
    voice_json_path: Path = DEFAULT_STRINGS_VOICE,
    direction_signs_path: Path = DEFAULT_DIRECTION_SIGNS_JSON,
) -> list[str]:
    """Run all readability checks on GB English strings, UI, voice, routes, and glossary."""
    rules_json = json.loads(rules_json_path.read_text(encoding="utf-8"))
    glossary_data = json.loads(glossary_path.read_text(encoding="utf-8"))
    rules_yaml = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    banned = load_banned_phrases(banned_path)
    jargon = load_jargon_phrases()

    errors = lint_rule_strings(rules_json, glossary_data, rules_yaml, banned)
    errors.extend(lint_glossary(glossary_data, banned))
    if jargon:
        allowed_abbrevs = extract_glossary_abbreviations(glossary_data)
        for rid, fields in sorted(rules_json.get("rules", {}).items()):
            for fn, val in fields.items():
                errors.extend(
                    check_text_readability(
                        val,
                        f"rules.{rid}.{fn}",
                        banned,
                        allowed_abbrevs,
                        jargon_phrases=jargon,
                    )
                )
        for lf in sorted((REPO_ROOT / "content" / "lessons" / "gb").glob("[0-9][0-9]-*.json")):
            lesson = json.loads(lf.read_text(encoding="utf-8"))
            for idx, b in enumerate(lesson["learnCard"]["blocks"]):
                errors.extend(
                    check_text_readability(
                        b, f"{lf.name}.block[{idx}]", banned, allowed_abbrevs, jargon_phrases=jargon
                    )
                )
            for sc in lesson["scenarios"]:
                errors.extend(
                    check_text_readability(
                        sc["goal"],
                        f"{lf.name}.{sc['id']}.goal",
                        banned,
                        allowed_abbrevs,
                        jargon_phrases=jargon,
                    )
                )
            for idx, r in enumerate(lesson["recap"]):
                errors.extend(
                    check_text_readability(
                        r, f"{lf.name}.recap[{idx}]", banned, allowed_abbrevs, jargon_phrases=jargon
                    )
                )
        for qf in sorted((REPO_ROOT / "content" / "lessons" / "gb" / "quiz").glob("*.json")):
            quiz = json.loads(qf.read_text(encoding="utf-8"))
            for q in quiz["questions"]:
                for fn in ("question", "explanation"):
                    errors.extend(
                        check_text_readability(
                            q[fn],
                            f"{qf.name}.{q['id']}.{fn}",
                            banned,
                            allowed_abbrevs,
                            jargon_phrases=jargon,
                        )
                    )
                for idx, opt in enumerate(q["options"]):
                    errors.extend(
                        check_text_readability(
                            opt,
                            f"{qf.name}.{q['id']}.opt[{idx}]",
                            banned,
                            allowed_abbrevs,
                            jargon_phrases=jargon,
                        )
                    )
    if ui_json_path.is_file():
        ui_json = json.loads(ui_json_path.read_text(encoding="utf-8"))
        errors.extend(lint_ui_strings(ui_json, glossary_data, banned))
    if voice_json_path.is_file():
        voice_json = json.loads(voice_json_path.read_text(encoding="utf-8"))
        errors.extend(lint_voice_strings(voice_json, glossary_data, rules_yaml, banned))
    if routes_json_path.is_file():
        routes_json = json.loads(routes_json_path.read_text(encoding="utf-8"))
        errors.extend(lint_route_instructions(routes_json, glossary_data, banned))
    if direction_signs_path.is_file():
        signs_json = json.loads(direction_signs_path.read_text(encoding="utf-8"))
        errors.extend(lint_direction_signs(signs_json, glossary_data, banned))
    return errors


if __name__ == "__main__":
    found_errors = lint_all_strings()
    if found_errors:
        for err in found_errors:
            print("ERROR:", err)
        raise SystemExit(1)
    print("Readability check passed with 0 errors.")
