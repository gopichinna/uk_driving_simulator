"""Tests for Round 24 content health report generator."""

from __future__ import annotations

from ltl_pipeline.health import OUT_PATH, generate_health_report


def test_health_report_contains_required_sections_and_matches_disk() -> None:
    report = generate_health_report()
    for heading in (
        "## Per-Stage Summary",
        "## Implemented Rules with Fewer Than 2 Practice Drives",
        "## Readability Issues",
        "## Unreferenced Sounds or Images",
        "## Strings in `en` Missing in `en-XA`",
        "## Files Over 5 MB",
        "## Top 10 Gaps (Priority Order)",
    ):
        assert heading in report
    assert "| Stage | Lessons | Scenarios | Lesson Quiz | Bank | Voice Lines | Badges |" in report
    assert OUT_PATH.is_file()
    assert OUT_PATH.read_text(encoding="utf-8") == report
