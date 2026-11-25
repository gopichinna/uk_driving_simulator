"""Tests that content/legal/en/credits.md includes every licence in content/ATTRIBUTIONS.md."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT

ATTRIBUTIONS_PATH = REPO_ROOT / "content" / "ATTRIBUTIONS.md"
CREDITS_PATH = REPO_ROOT / "content" / "legal" / "en" / "credits.md"


def test_credits_includes_all_attribution_licences() -> None:
    attributions = ATTRIBUTIONS_PATH.read_text(encoding="utf-8")
    credits_text = CREDITS_PATH.read_text(encoding="utf-8")

    required_markers = [
        "© OpenStreetMap contributors",
        "ODbL-1.0",
        "Open Government Licence v3.0",
        "OGL v3.0",
        "Crown copyright",
        "not endorsed by or affiliated with DVSA or DfT",
    ]
    for marker in required_markers:
        if marker in attributions or "DVSA" in marker:
            assert marker in credits_text, f"credits.md is missing {marker!r}"
