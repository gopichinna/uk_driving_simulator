"""Generate en-XA pseudo-locale strings from en/ string tables (~40% longer, accented)."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

from ltl_pipeline.schema import REPO_ROOT, validate_strings_file

EN_DIR = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en"
XA_DIR = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en-XA"

ACCENT_MAP = str.maketrans(
    "AEIOUaeiou",
    "ÁÉÍÓÚáéíóú",
)


def pseudolocalise(text: str) -> str:
    """Accent plain text and expand by ~40% while keeping ICU {placeholders} intact."""
    out: list[str] = []
    depth = 0
    plain_len = 0
    for ch in text:
        if ch == "{":
            depth += 1
            out.append(ch)
        elif ch == "}":
            depth = max(0, depth - 1)
            out.append(ch)
        elif depth > 0:
            out.append(ch)
        else:
            out.append(ch.translate(ACCENT_MAP))
            plain_len += 1

    extra = max(4, math.ceil(plain_len * 0.40))
    pad = "~" * extra
    return f"[{''.join(out)} {pad}]"


def transform_strings_doc(data: dict[str, Any]) -> dict[str, Any]:
    """Return a pseudo-localised copy of a strings.v0 document."""
    result = dict(data)
    result["language"] = "en-XA"
    kind = data["kind"]

    if kind == "rules":
        new_rules: dict[str, dict[str, str]] = {}
        for rid, entry in data["rules"].items():
            new_rules[rid] = {k: pseudolocalise(v) for k, v in entry.items()}
        result["rules"] = new_rules
    elif kind == "glossary":
        new_terms: list[dict[str, Any]] = []
        for entry in data["terms"]:
            item = dict(entry)
            item["term"] = pseudolocalise(entry["term"])
            item["definition"] = pseudolocalise(entry["definition"])
            if "kmhHint" in entry:
                item["kmhHint"] = pseudolocalise(entry["kmhHint"])
            new_terms.append(item)
        result["terms"] = new_terms
    elif kind in ("ui", "voice"):
        result["entries"] = {k: pseudolocalise(v) for k, v in data["entries"].items()}

    return result


def generate_pseudolocale(en_dir: Path = EN_DIR, xa_dir: Path = XA_DIR) -> list[Path]:
    """Generate all en-XA JSON files from en/*.json and validate them."""
    xa_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for src in sorted(en_dir.glob("*.json")):
        data = json.loads(src.read_text(encoding="utf-8"))
        xa_data = transform_strings_doc(data)
        dest = xa_dir / src.name
        dest.write_text(
            json.dumps(xa_data, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        validate_strings_file(dest)
        written.append(dest)
    return written


if __name__ == "__main__":
    files = generate_pseudolocale()
    print("Generated pseudo-locale:", [str(p) for p in files])
