"""Tests for RoadPack and RulePack schema validation."""

from pathlib import Path

import jsonschema
import pytest

from ltl_pipeline.schema import REPO_ROOT, validate_roadpack, validate_roadpack_file

SAMPLE_PACK = REPO_ROOT / "content" / "samples" / "roadpack.sample.json"


def test_sample_roadpack_validates() -> None:
    data = validate_roadpack_file(SAMPLE_PACK)
    assert data["packId"] == "sample-t-junction"
    assert data["country"] == "GB"
    assert data["drivingSide"] == "left"


def test_invalid_roadpack_raises() -> None:
    with pytest.raises(jsonschema.ValidationError):
        validate_roadpack({"packFormatVersion": "0.1"})


def test_sample_path_exists() -> None:
    assert Path(SAMPLE_PACK).is_file()
