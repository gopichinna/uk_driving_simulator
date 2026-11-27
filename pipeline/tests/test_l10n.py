"""Tests for Round 37 localisation readiness checker."""

from __future__ import annotations

from ltl_pipeline.l10n import check_localisation, format_report


def test_l10n_en_and_en_xa_have_zero_missing_extra_or_placeholder_errors() -> None:
    report = check_localisation()
    assert report.missing_keys == {}, f"Missing keys: {report.missing_keys}"
    assert report.extra_keys == {}, f"Extra keys: {report.extra_keys}"
    assert report.placeholder_mismatches == {}, (
        f"Placeholder mismatches: {report.placeholder_mismatches}"
    )
    assert report.long_buttons == [], f"Button strings > 40 chars: {report.long_buttons}"
    text = format_report(report)
    assert "Localisation Readiness Report" in text
