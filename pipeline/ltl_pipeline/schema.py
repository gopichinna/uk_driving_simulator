"""Schema validation helpers for RoadPack v0 and RulePack v0."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import jsonschema
import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
ROADPACK_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "roadpack.v0.schema.json"
RULEPACK_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "rulepack.v0.schema.json"


def load_schema(schema_path: Path) -> dict[str, Any]:
    """Load a JSON Schema file from disk."""
    with schema_path.open("r", encoding="utf-8") as f:
        return json.load(f)


def validate_roadpack(data: dict[str, Any], schema_path: Path = ROADPACK_SCHEMA_PATH) -> None:
    """Validate an in-memory RoadPack dict against roadpack.v0.schema.json."""
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())


def validate_roadpack_file(path: Path, schema_path: Path = ROADPACK_SCHEMA_PATH) -> dict[str, Any]:
    """Load a RoadPack JSON file and validate it against roadpack.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    validate_roadpack(data, schema_path=schema_path)
    return data


def validate_rulepack(data: dict[str, Any], schema_path: Path = RULEPACK_SCHEMA_PATH) -> None:
    """Validate an in-memory RulePack dict against rulepack.v0.schema.json."""
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())


def validate_rulepack_file(path: Path, schema_path: Path = RULEPACK_SCHEMA_PATH) -> dict[str, Any]:
    """Load a RulePack YAML file and validate it against rulepack.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = yaml.safe_load(f)
    validate_rulepack(data, schema_path=schema_path)
    return data


def validate_all_content(content_dir: Path | None = None) -> list[Path]:
    """Validate all roadpack.json and rules.yaml files under content/."""
    root = content_dir or (REPO_ROOT / "content")
    validated: list[Path] = []

    for pack_path in sorted(root.glob("samples/*roadpack*.json")):
        validate_roadpack_file(pack_path)
        validated.append(pack_path)

    for pack_path in sorted(root.glob("cities/*/roadpack.json")):
        validate_roadpack_file(pack_path)
        validated.append(pack_path)

    for rules_path in sorted(root.glob("countries/*/rules.yaml")):
        validate_rulepack_file(rules_path)
        validated.append(rules_path)

    return validated
