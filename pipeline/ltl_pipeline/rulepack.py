"""Parse docs/02-rules-catalogue.md into content/countries/gb/rules.yaml."""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any

import yaml

from ltl_pipeline.schema import REPO_ROOT, validate_rulepack

CATALOGUE_PATH = REPO_ROOT / "docs" / "02-rules-catalogue.md"
RULES_YAML_PATH = REPO_ROOT / "content" / "countries" / "gb" / "rules.yaml"
RULES_CORE_DIR = REPO_ROOT / "core" / "Rules.Core"
VERSION_TXT_PATH = REPO_ROOT / "content" / "countries" / "gb" / "highway-code-version.txt"

RULE_ID_RE = re.compile(r"^GB\.[A-Z]+\.[A-Z0-9_]+$")
CONST_STMT_RE = re.compile(r"public\s+const\s+string\s+([^;]+);")
CONST_RULE_LITERAL_RE = re.compile(r'"(GB\.[A-Z]+\.[A-Z0-9_]+)"')
NAMESPACE_RE = re.compile(r"namespace\s+([A-Za-z0-9_.]+)")
CLASS_RE = re.compile(r"class\s+([A-Za-z0-9_]+)")


def scan_implemented_rules(rules_core_dir: Path = RULES_CORE_DIR) -> dict[str, dict[str, str]]:
    """Scan core/Rules.Core/*.cs read-only for GB.* rule constants."""
    found: dict[str, dict[str, str]] = {}
    if not rules_core_dir.is_dir():
        return found

    for cs_path in sorted(rules_core_dir.glob("*.cs")):
        text = cs_path.read_text(encoding="utf-8")
        ns_match = NAMESPACE_RE.search(text)
        ns = ns_match.group(1) if ns_match else ""
        classes = [(m.start(), m.group(1)) for m in CLASS_RE.finditer(text)]
        rel_file = cs_path.relative_to(REPO_ROOT).as_posix()
        for stmt in CONST_STMT_RE.finditer(text):
            pos = stmt.start()
            cls_name = ""
            for cls_pos, name in classes:
                if cls_pos < pos:
                    cls_name = name
                else:
                    break
            full_class = f"{ns}.{cls_name}" if ns and cls_name else (cls_name or cs_path.stem)
            for lit in CONST_RULE_LITERAL_RE.finditer(stmt.group(1)):
                rule_id = lit.group(1)
                found[rule_id] = {
                    "status": "implemented",
                    "class": full_class,
                    "file": rel_file,
                }
    return found


def _parse_source_urls(lines: list[str]) -> dict[str, str]:
    urls: dict[str, str] = {}
    in_sources = False
    for line in lines:
        if line.startswith("### 1.1 Sources"):
            in_sources = True
            continue
        if in_sources and line.startswith("##"):
            break
        is_row = (
            in_sources
            and line.startswith("|")
            and not line.startswith("| Code")
            and not line.startswith("|---")
        )
        if is_row:
            parts = [p.strip() for p in line.strip("|").split("|")]
            if len(parts) >= 3 and parts[2].startswith("https://"):
                urls[parts[0]] = parts[2]
    return urls


def _parse_force(text: str, severity: str) -> str:
    upper = text.upper()
    if "MUST" in upper:
        return "MUST"
    if "SHOULD" in upper:
        return "should"
    if severity.strip() == "i":
        return "info"
    return "should"


def _parse_verified(text: str) -> str:
    if "✅" in text:
        return "verified"
    if "⚠️" in text:
        return "partial"
    return "unverified"


def _extract_source(source_cell: str, source_urls: dict[str, str]) -> dict[str, Any]:
    parts = [p.strip() for p in source_cell.split("·")]
    src_text = parts[0] if parts else source_cell.strip()

    url: str | None = None
    for code, candidate_url in source_urls.items():
        if re.search(rf"\b{re.escape(code)}\b", src_text):
            url = candidate_url
            break
    if url is None and "GOV.UK" in src_text:
        url = "https://www.gov.uk/"

    rule_nums = re.findall(r"\b(?:H[1-3]|\d{2,3})\b", src_text)
    result: dict[str, Any] = {"text": src_text}
    if rule_nums:
        result["rules"] = rule_nums
    if url:
        result["url"] = url
    return result


def parse_catalogue(
    catalogue_path: Path = CATALOGUE_PATH,
    rules_core_dir: Path = RULES_CORE_DIR,
    version_txt_path: Path = VERSION_TXT_PATH,
) -> dict[str, Any]:
    """Parse docs/02-rules-catalogue.md into a RulePack v0 dict."""
    text = catalogue_path.read_text(encoding="utf-8")
    lines = text.splitlines()

    checked_match = re.search(r"checked against GOV\.UK on \*\*(\d{4}-\d{2}-\d{2})\*\*", text)
    verified_on = checked_match.group(1) if checked_match else "2026-11-25"

    hc_version = (
        version_txt_path.read_text(encoding="utf-8").strip()
        if version_txt_path.is_file()
        else "2025-10-22"
    )

    source_urls = _parse_source_urls(lines)
    implemented = scan_implemented_rules(rules_core_dir)

    rules: list[dict[str, Any]] = []
    current_group = ""
    in_catalogue = False

    for line in lines:
        if line.startswith("## 2. Catalogue"):
            in_catalogue = True
            continue
        if in_catalogue and line.startswith("## "):
            break
        if not in_catalogue:
            continue

        if line.startswith("### "):
            current_group = line.removeprefix("### ").strip()
            continue

        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if not cells or not RULE_ID_RE.match(cells[0]):
            continue

        rule_id = cells[0]
        plain_english = cells[1]
        source_cell = cells[2]

        if len(cells) == 4:
            # Table T: | rule_id | Topic | Source · V | Ph |
            detection = ""
            severity = "i"
            teaching = ""
            phase = cells[3]
        else:
            detection = cells[3] if len(cells) > 3 else ""
            severity = cells[4] if len(cells) > 4 else "i"
            teaching = cells[5] if len(cells) > 5 else ""
            phase = cells[6] if len(cells) > 6 else "M"

        verified = _parse_verified(source_cell)
        entry: dict[str, Any] = {
            "id": rule_id,
            "group": current_group,
            "plainEnglish": plain_english,
            "source": _extract_source(source_cell, source_urls),
            "force": _parse_force(source_cell, severity),
            "verified": verified,
            "severity": severity,
            "phase": phase,
        }
        if verified in ("verified", "partial"):
            entry["verifiedOn"] = verified_on
        if detection:
            entry["detection"] = detection
        if teaching:
            entry["teachingMoment"] = teaching
        if rule_id in implemented:
            entry["implementation"] = implemented[rule_id]

        rules.append(entry)

    pack: dict[str, Any] = {
        "rulePackVersion": "0.1",
        "country": "GB",
        "sourceDocument": "docs/02-rules-catalogue.md",
        "highwayCodeLastUpdate": hc_version,
        "rules": rules,
    }
    validate_rulepack(pack)
    return pack


def write_rulepack(
    out_path: Path = RULES_YAML_PATH,
    catalogue_path: Path = CATALOGUE_PATH,
    rules_core_dir: Path = RULES_CORE_DIR,
) -> Path:
    """Generate and write content/countries/gb/rules.yaml deterministically."""
    pack = parse_catalogue(catalogue_path=catalogue_path, rules_core_dir=rules_core_dir)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    header = (
        "# Generated by ltl_pipeline.rulepack from docs/02-rules-catalogue.md."
        " Do not edit by hand.\n"
    )
    dumped = yaml.safe_dump(pack, sort_keys=False, allow_unicode=True, width=120)
    out_path.write_text(header + dumped, encoding="utf-8")
    return out_path


def verify_code_rules_in_yaml(
    rules_yaml_path: Path = RULES_YAML_PATH,
    rules_core_dir: Path = RULES_CORE_DIR,
) -> None:
    """Raise AssertionError if any GB.* constant in Rules.Core is missing from rules.yaml."""
    data = yaml.safe_load(rules_yaml_path.read_text(encoding="utf-8"))
    yaml_ids = {r["id"] for r in data["rules"]}
    code_rules = scan_implemented_rules(rules_core_dir)
    missing = sorted(set(code_rules.keys()) - yaml_ids)
    if missing:
        raise AssertionError(f"Rule IDs in core/Rules.Core missing from rules.yaml: {missing}")


if __name__ == "__main__":
    written = write_rulepack()
    verify_code_rules_in_yaml()
    print(f"Wrote and verified {written}")
