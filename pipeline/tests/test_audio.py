"""Tests for Round 10/15 coach voice & SFX WAVs (no Opus), licences, and screenshots."""

from __future__ import annotations

import json
import re
import wave

from ltl_pipeline.schema import REPO_ROOT

VOICE_JSON = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "voice.json"
AUDIO_DIR = REPO_ROOT / "content" / "audio"
VOICE_MANIFEST = AUDIO_DIR / "voice" / "en" / "manifest.json"
SFX_MANIFEST = AUDIO_DIR / "sfx" / "manifest.json"
LICENCES_MD = AUDIO_DIR / "LICENCES.md"
SCREENSHOTS_MD = REPO_ROOT / "content" / "store" / "en" / "screenshots.md"
ATTRIBUTIONS_MD = REPO_ROOT / "content" / "ATTRIBUTIONS.md"
CREDITS_MD = REPO_ROOT / "content" / "legal" / "en" / "credits.md"
CLAIMS_YAML = REPO_ROOT / "content" / "store" / "en" / "claims.yaml"


def _assert_unity_importable_audio(path) -> None:
    raw = path.read_bytes()
    assert b"OpusHead" not in raw[:64], f"{path.name}: Opus codec is not supported by Unity"
    if path.suffix == ".wav":
        with wave.open(str(path), "rb") as wf:
            assert wf.getnchannels() == 1, f"{path.name}: expected mono"
            assert wf.getsampwidth() == 2, f"{path.name}: expected 16-bit PCM"
            assert wf.getframerate() == 44100, f"{path.name}: expected 44.1 kHz"
    elif path.suffix == ".ogg":
        assert b"\x01vorbis" in raw[:128], f"{path.name}: Ogg must be Vorbis, not Opus"
    else:
        raise AssertionError(f"Unexpected audio extension: {path.name}")


def test_every_voice_key_has_audio_and_valid_duration() -> None:
    voice_keys = set(json.loads(VOICE_JSON.read_text(encoding="utf-8"))["entries"].keys())
    manifest = json.loads(VOICE_MANIFEST.read_text(encoding="utf-8"))
    by_key = {e["key"]: e for e in manifest["entries"]}
    assert set(by_key.keys()) == voice_keys
    assert not list(VOICE_MANIFEST.parent.glob("*.ogg")), "Stale Opus .ogg files remain"
    for key, entry in by_key.items():
        clip = VOICE_MANIFEST.parent / entry["file"]
        assert clip.is_file() and clip.stat().st_size > 0, f"Missing voice clip for {key}"
        _assert_unity_importable_audio(clip)
        assert 0.3 <= entry["durationS"] <= 6.0, (
            f"{key} duration {entry['durationS']}s out of range"
        )


def test_every_sfx_has_licence_and_valid_duration() -> None:
    licences_text = LICENCES_MD.read_text(encoding="utf-8")
    assert "CC-BY-NC" not in licences_text.replace("No CC-BY-NC", "")
    manifest = json.loads(SFX_MANIFEST.read_text(encoding="utf-8"))
    assert len(manifest["entries"]) >= 13
    assert not list(SFX_MANIFEST.parent.glob("*.ogg")), "Stale Opus .ogg files remain"
    for entry in manifest["entries"]:
        clip = SFX_MANIFEST.parent / entry["file"]
        assert clip.is_file() and clip.stat().st_size > 0
        _assert_unity_importable_audio(clip)
        assert entry["file"] in licences_text, f"{entry['file']} missing from LICENCES.md"
        if entry["key"].startswith("engine_"):
            assert 2.0 <= entry["durationS"] <= 4.0, (
                f"Engine loop {entry['file']} duration {entry['durationS']}s outside 2-4 s"
            )
            assert "domasx2" in licences_text
        elif not entry["loop"]:
            assert 0.02 <= entry["durationS"] <= 3.0, (
                f"SFX {entry['file']} duration {entry['durationS']}s > 3s"
            )


def test_attributions_and_credits_list_audio_sources() -> None:
    attr = ATTRIBUTIONS_MD.read_text(encoding="utf-8")
    cred = CREDITS_MD.read_text(encoding="utf-8")
    for text in (attr, cred):
        assert "Piper" in text
        assert "en_GB-cori-medium" in text
        assert "CC BY 4.0" in text
        assert "CC0 1.0" in text


def test_screenshots_captions_match_shipped_claims_and_word_limit() -> None:
    import yaml

    claims = {
        c["id"]: c["status"]
        for c in yaml.safe_load(CLAIMS_YAML.read_text(encoding="utf-8"))["claims"]
    }
    lines = [
        ln.strip()
        for ln in SCREENSHOTS_MD.read_text(encoding="utf-8").splitlines()
        if ln.strip() and ln.strip()[0].isdigit()
    ]
    assert len(lines) == 6
    for ln in lines:
        m = re.search(r"<!--\s*claim:\s*([a-z0-9_]+)\s*-->\s*(.+)$", ln)
        assert m, f"Missing claim tag in screenshot caption: {ln}"
        cid, caption = m.group(1), m.group(2).strip()
        assert claims.get(cid) == "shipped", f"Caption {cid} is not shipped"
        words = [w for w in caption.split() if w]
        assert len(words) <= 8, f"Caption too long ({len(words)} words): {caption}"
