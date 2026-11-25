"""Tests for draft store listing and privacy notice constraints."""

from __future__ import annotations

import re

from ltl_pipeline.schema import REPO_ROOT

LISTING_PATH = REPO_ROOT / "content" / "store" / "en" / "listing.md"
PRIVACY_PATH = REPO_ROOT / "content" / "store" / "en" / "privacy-draft.md"


def test_store_listing_lengths_and_disclaimers() -> None:
    text = LISTING_PATH.read_text(encoding="utf-8")
    assert "DRAFT: owner review needed" in text
    assert "not endorsed by or affiliated with DVSA" in text

    short_m = re.search(
        r"## Short description[^\n]*\n+([^\n#]+)",
        text,
    )
    assert short_m is not None
    short_desc = short_m.group(1).strip()
    assert 1 <= len(short_desc) <= 80

    full_m = re.search(
        r"## Full description[^\n]*\n+(.+)$",
        text,
        flags=re.DOTALL,
    )
    assert full_m is not None
    full_desc = full_m.group(1).strip()
    assert 100 <= len(full_desc) <= 4000


def test_privacy_draft_markers_and_open_questions() -> None:
    text = PRIVACY_PATH.read_text(encoding="utf-8")
    assert "DRAFT: owner and legal review needed" in text
    assert "analytics" in text.lower()
    assert "ads" in text.lower()
