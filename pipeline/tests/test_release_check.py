"""Tests for Round 42 release-candidate checks."""

from __future__ import annotations

from pathlib import Path

from ltl_pipeline.release_check import (
    check_claims_evidence,
    check_licences,
    check_placeholders,
    check_required_legal_strings,
    check_unverified_shipped_rules,
    format_report,
    run_release_check,
)


def test_release_check_core_gates_pass() -> None:
    assert check_licences() == []
    assert check_claims_evidence() == []
    assert check_unverified_shipped_rules() == []
    assert check_required_legal_strings() == []


def test_release_check_placeholder_detection(tmp_path: Path) -> None:
    (tmp_path / "content").mkdir()
    (tmp_path / "docs" / "store").mkdir(parents=True)
    (tmp_path / "docs" / "store" / "privacy.md").write_text(
        "Contact <owner email>", encoding="utf-8"
    )
    errs = check_placeholders(tmp_path)
    assert len(errs) == 1
    assert "<owner email>" in errs[0]

    rep = run_release_check()
    text = format_report(rep)
    assert "Release Check Report" in text
