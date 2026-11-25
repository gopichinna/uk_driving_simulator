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
STRINGS_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "strings.v0.schema.json"
SIGNS_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "signs.v0.schema.json"
LESSON_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "lesson.v0.schema.json"
ROUTE_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "route.v0.schema.json"
TESTMARKING_SCHEMA_PATH = REPO_ROOT / "content" / "schemas" / "testmarking.v0.schema.json"


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


def validate_strings_file(path: Path, schema_path: Path = STRINGS_SCHEMA_PATH) -> dict[str, Any]:
    """Load a strings/glossary JSON file and validate it against strings.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())
    return data


def validate_signs_file(path: Path, schema_path: Path = SIGNS_SCHEMA_PATH) -> dict[str, Any]:
    """Load a signs YAML file and validate it against signs.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = yaml.safe_load(f)
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())
    return data


def validate_lesson_file(path: Path, schema_path: Path = LESSON_SCHEMA_PATH) -> dict[str, Any]:
    """Load a lesson JSON file and validate it against lesson.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())
    return data


def validate_routes_file(path: Path, schema_path: Path = ROUTE_SCHEMA_PATH) -> dict[str, Any]:
    """Load a routes JSON file and validate it against route.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())
    return data


def validate_testmarking_file(
    path: Path, schema_path: Path = TESTMARKING_SCHEMA_PATH
) -> dict[str, Any]:
    """Load a test_marking YAML file and validate it against testmarking.v0.schema.json."""
    with path.open("r", encoding="utf-8") as f:
        data = yaml.safe_load(f)
    schema = load_schema(schema_path)
    jsonschema.validate(instance=data, schema=schema, format_checker=jsonschema.FormatChecker())
    return data


def validate_all_content(content_dir: Path | None = None) -> list[Path]:
    """Validate all roadpack, rules, strings, signs, lesson, and route files under content/."""
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

    for strings_path in sorted(root.glob("countries/*/strings/*/*.json")):
        validate_strings_file(strings_path)
        validated.append(strings_path)

    for signs_path in sorted(root.glob("countries/*/signs.yaml")):
        validate_signs_file(signs_path)
        validated.append(signs_path)

    for lesson_path in sorted(root.glob("lessons/*/*.json")):
        validate_lesson_file(lesson_path)
        validated.append(lesson_path)

    for routes_path in sorted(root.glob("cities/*/routes.json")):
        validate_routes_file(routes_path)
        validated.append(routes_path)

    for tm_path in sorted(root.glob("countries/*/test_marking.yaml")):
        validate_testmarking_file(tm_path)
        validated.append(tm_path)

    return validated
