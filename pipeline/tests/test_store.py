"""Tests for draft store listing and privacy notice constraints."""

from __future__ import annotations

import re

import yaml

from ltl_pipeline.schema import REPO_ROOT

LISTING_PATH = REPO_ROOT / "content" / "store" / "en" / "listing.md"
CLAIMS_PATH = REPO_ROOT / "content" / "store" / "en" / "claims.yaml"
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


def test_listing_bullets_match_claims_yaml() -> None:
    claims_data = yaml.safe_load(CLAIMS_PATH.read_text(encoding="utf-8"))
    claims_by_id = {c["id"]: c for c in claims_data["claims"]}

    listing = LISTING_PATH.read_text(encoding="utf-8")
    today_sec = re.search(r"### What is included today\n(.*?)\n### ", listing, flags=re.DOTALL)
    soon_sec = re.search(r"### Coming soon\n(.*?)\n### ", listing, flags=re.DOTALL)
    assert today_sec is not None and soon_sec is not None

    for line in today_sec.group(1).strip().splitlines():
        if line.startswith("- "):
            m = re.search(r"<!--\s*claim:\s*([a-z0-9_]+)\s*-->", line)
            assert m is not None, f"Missing claim comment on shipped bullet: {line}"
            cid = m.group(1)
            assert cid in claims_by_id and claims_by_id[cid]["status"] == "shipped"

    for line in soon_sec.group(1).strip().splitlines():
        if line.startswith("- "):
            m = re.search(r"<!--\s*claim:\s*([a-z0-9_]+)\s*-->", line)
            assert m is not None, f"Missing claim comment on planned bullet: {line}"
            cid = m.group(1)
            assert cid in claims_by_id and claims_by_id[cid]["status"] == "planned"
