"""Tests for the readability and string-completeness linter."""

from __future__ import annotations

from ltl_pipeline.readability import (
    extract_glossary_abbreviations,
    lint_all_strings,
    lint_rule_strings,
)

SAMPLE_GLOSSARY = {
    "terms": [
        {
            "id": "uk",
            "term": "UK",
            "definition": "The United Kingdom.",
            "relatedRules": [],
        },
        {
            "id": "msm",
            "term": "MSM",
            "definition": "Mirrors, Signal, Manoeuvre.",
            "relatedRules": ["GB.OBS.MSM"],
        },
    ]
}

SAMPLE_RULES_YAML = {
    "rules": [
        {"id": "GB.SPD.OVER_LIMIT", "phase": "P"},
    ]
}


def _valid_rules_json() -> dict:
    return {
        "rules": {
            "GB.SPD.OVER_LIMIT": {
                "title": "Speed limit",
                "fault": "You drove at {speedMph} mph in a {limitMph} mph zone.",
                "why": "Higher speeds reduce your time to stop. You must stay within the limit.",
                "tip": "Watch for speed limit signs after every junction.",
                "good": "You stayed within the speed limit.",
            }
        }
    }


def test_long_sentence_fails() -> None:
    rules_json = _valid_rules_json()
    rules_json["rules"]["GB.SPD.OVER_LIMIT"]["why"] = (
        "This single sentence has way too many words so it should definitely trigger "
        "the fifteen word limit."
    )
    errors = lint_rule_strings(rules_json, SAMPLE_GLOSSARY, SAMPLE_RULES_YAML, [])
    assert any("words (max 15)" in e for e in errors)


def test_banned_phrase_fails() -> None:
    rules_json = _valid_rules_json()
    rules_json["rules"]["GB.SPD.OVER_LIMIT"]["tip"] = "Proceed with caution near schools."
    errors = lint_rule_strings(
        rules_json, SAMPLE_GLOSSARY, SAMPLE_RULES_YAML, ["proceed with caution"]
    )
    assert any("banned phrase" in e for e in errors)


def test_unknown_abbreviation_fails() -> None:
    rules_json = _valid_rules_json()
    rules_json["rules"]["GB.SPD.OVER_LIMIT"]["tip"] = "Follow DVSA rules at all times."
    errors = lint_rule_strings(rules_json, SAMPLE_GLOSSARY, SAMPLE_RULES_YAML, [])
    assert any("DVSA" in e and "glossary" in e for e in errors)


def test_known_glossary_abbreviation_passes() -> None:
    assert "UK" in extract_glossary_abbreviations(SAMPLE_GLOSSARY)
    rules_json = _valid_rules_json()
    rules_json["rules"]["GB.SPD.OVER_LIMIT"]["tip"] = "In the UK, obey every limit."
    errors = lint_rule_strings(rules_json, SAMPLE_GLOSSARY, SAMPLE_RULES_YAML, [])
    assert errors == []


def test_missing_phase_p_rule_fails() -> None:
    errors = lint_rule_strings({"rules": {}}, SAMPLE_GLOSSARY, SAMPLE_RULES_YAML, [])
    assert any("GB.SPD.OVER_LIMIT" in e and "missing" in e for e in errors)


def test_actual_gb_strings_pass_readability() -> None:
    errors = lint_all_strings()
    assert errors == [], f"Readability errors: {errors}"


REQUIRED_UI_KEYS = [
    "hud.finish_drive",
    "hud.reset",
    "hud.course",
    "hud.view_seat",
    "hud.view_chase",
    "hud.settings",
    "debrief.title",
    "debrief.no_faults",
    "debrief.tap_to_replay",
    "debrief.replay_close",
    "debrief.count.driving",
    "debrief.count.serious",
    "debrief.count.dangerous",
    "debrief.count.good",
    "debrief.result.pass",
    "debrief.result.fail",
    "debrief.result.note",
    "debrief.drive_again",
    "debrief.keep_driving",
    "settings.title",
    "settings.steering.label",
    "settings.steering.hint",
    "settings.units.hint",
    "settings.reset_progress.label",
    "settings.reset_progress.confirm",
    "settings.close",
    "course.town",
    "course.two_lane_roundabout",
    "course.roundabout",
    "course.dual_carriageway",
    "course.wolverhampton_beta",
]


def test_required_ui_keys_and_pseudolocale() -> None:
    import json

    from ltl_pipeline.pseudolocale import XA_DIR
    from ltl_pipeline.readability import DEFAULT_STRINGS_UI

    ui_data = json.loads(DEFAULT_STRINGS_UI.read_text(encoding="utf-8"))
    entries = ui_data["entries"]
    for key in REQUIRED_UI_KEYS:
        assert key in entries, f"Missing required UI key: {key}"

    xa_ui = json.loads((XA_DIR / "ui.json").read_text(encoding="utf-8"))
    assert xa_ui["language"] == "en-XA"
    for key in REQUIRED_UI_KEYS:
        assert key in xa_ui["entries"]
        assert "{count" not in entries[key] or "{count" in xa_ui["entries"][key]
