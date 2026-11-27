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


def test_google_play_listing_en_md() -> None:
    play_listing = REPO_ROOT / "content" / "store" / "listing" / "en.md"
    assert play_listing.is_file()
    text = play_listing.read_text(encoding="utf-8")

    name_m = re.search(r"## App name[^\n]*\n+([^\n#]+)", text)
    short_m = re.search(r"## Short description[^\n]*\n+([^\n#]+)", text)
    full_m = re.search(r"## Full description[^\n]*\n+(.+)$", text, flags=re.DOTALL)
    assert name_m is not None and short_m is not None and full_m is not None

    app_name = name_m.group(1).strip()
    short_desc = short_m.group(1).strip()
    full_desc = full_m.group(1).strip()

    assert 1 <= len(app_name) <= 30, f"App name too long: {len(app_name)}"
    assert 1 <= len(short_desc) <= 80, f"Short desc too long: {len(short_desc)}"
    assert 100 <= len(full_desc) <= 4000, f"Full desc out of bounds: {len(full_desc)}"
    assert "not a substitute for lessons with an approved driving instructor" in text.lower()
    assert "not endorsed by or affiliated with dvsa" in text.lower()
    assert "Open Government Licence" in text
    assert "OpenStreetMap" in text
    assert "10 mock theory tests" in text
    assert "50 practice questions" in text
    assert "43 to pass" in text
    assert "57-minute" in text
    assert "does not guarantee" in text.lower()


def test_every_claim_has_valid_evidence_files() -> None:
    claims_data = yaml.safe_load(CLAIMS_PATH.read_text(encoding="utf-8"))
    for claim in claims_data["claims"]:
        evidence = claim.get("evidence")
        assert evidence, f"Claim {claim['id']} is missing evidence paths"
        for rel_path in evidence:
            target = REPO_ROOT / rel_path
            assert target.is_file(), f"Claim {claim['id']} points to missing file: {rel_path}"
