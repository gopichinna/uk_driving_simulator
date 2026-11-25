"""Tests for GB practical driving test marking rules."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT, validate_testmarking_file

TEST_MARKING_PATH = REPO_ROOT / "content" / "countries" / "gb" / "test_marking.yaml"


def test_test_marking_validates_and_has_govuk_url() -> None:
    data = validate_testmarking_file(TEST_MARKING_PATH)
    assert data["country"] == "GB"
    assert data["maxDrivingFaults"] == 15
    assert data["failOnSerious"] is True
    assert data["failOnDangerous"] is True
    assert data["source"]["verified"] is True
    url = data["source"]["url"]
    assert url.startswith("https://") and "gov.uk" in url
