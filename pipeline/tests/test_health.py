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
    quiz_files = list(
        (OUT_PATH.parent.parent / "content" / "lessons" / "gb" / "quiz").glob("gb-*.json")
    )
    if quiz_files:
        # Ensure Lesson Quiz column is non-zero when lesson quiz files exist
        total_lq = sum(
            int(line.split("|")[4].strip())
            for line in report.splitlines()
            if line.startswith("| ") and line.split("|")[1].strip().isdigit()
        )
        assert total_lq > 0, "Lesson Quiz column reported 0 despite quiz files existing"
    assert OUT_PATH.is_file()
    assert OUT_PATH.read_text(encoding="utf-8") == report
