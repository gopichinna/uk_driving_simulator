"""Tests for plain-English rule explainer cards (en and en-XA)."""

from __future__ import annotations

import json

from ltl_pipeline.readability import (
    check_text_readability,
    extract_glossary_abbreviations,
    load_banned_phrases,
)
from ltl_pipeline.schema import REPO_ROOT, validate_strings_file

EN_EXPLAINERS = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "explainers.json"
XA_EXPLAINERS = (
    REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en-XA" / "explainers.json"
)
GLOSSARY_JSON = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "glossary.json"

IMPLEMENTED_RULE_IDS = {
    "GB.SPD.OVER_LIMIT",
    "GB.VRU.PASS_CYCLIST",
    "GB.OBS.LANE_CHANGE",
    "GB.SIG.TIMELY",
    "GB.POS.KEEP_LEFT",
    "GB.RBT.GIVE_WAY_RIGHT",
    "GB.RBT.APPROACH_LANE",
    "GB.RBT.STAY_IN_LANE",
    "GB.RBT.SIGNAL_APPROACH",
    "GB.RBT.SIGNAL_EXIT",
    "GB.RBT.WRONG_WAY",
    "GB.GAP.TWO_SECOND",
    "GB.TL.RED",
    "GB.TL.AMBER",
    "GB.TL.RED_AMBER",
    "GB.TL.ASL",
    "GB.JCT.GIVE_WAY",
    "GB.JCT.STOP_SIGN",
    "GB.SCH.SLOW",
    "GB.JCT.TURN_NEAR",
    "GB.JCT.TURN_FAR_GAP",
    "GB.XNG.ZEBRA",
    "GB.JCT.H2_PEDESTRIANS",
    "GB.XNG.ZIGZAG_OVERTAKE",
    "GB.JCT.BOX",
}


def test_every_implemented_rule_has_valid_explainer_card() -> None:
    en_data = validate_strings_file(EN_EXPLAINERS)
    xa_data = validate_strings_file(XA_EXPLAINERS)

    assert set(en_data["explainers"].keys()) >= IMPLEMENTED_RULE_IDS
    assert set(xa_data["explainers"].keys()) == set(en_data["explainers"].keys())

    glossary = json.loads(GLOSSARY_JSON.read_text(encoding="utf-8"))
    allowed_abbrevs = extract_glossary_abbreviations(glossary)
    banned = load_banned_phrases()

    for rid, card in en_data["explainers"].items():
        words = " ".join((card["rule"], card["why"], card["mistake"])).split()
        assert len(words) <= 60, f"{rid}: explainer has {len(words)} words (> 60)"
        for field in ("rule", "why", "mistake"):
            errs = check_text_readability(
                card[field],
                context=f"explainers.{rid}.{field}",
                banned_phrases=banned,
                allowed_abbrevs=allowed_abbrevs,
            )
            assert errs == [], f"Readability errors in {rid}.{field}: {errs}"
