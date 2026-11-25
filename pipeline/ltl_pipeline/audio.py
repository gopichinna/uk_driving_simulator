"""Generate coach voice OGGs (Piper en_GB-cori-medium) and CC0 procedural SFX."""

from __future__ import annotations

import json
import subprocess
import tempfile
import wave
from pathlib import Path
from typing import Any

import numpy as np
from piper import PiperVoice

from ltl_pipeline.schema import REPO_ROOT

VOICE_JSON = REPO_ROOT / "content" / "countries" / "gb" / "strings" / "en" / "voice.json"
MODEL_PATH = REPO_ROOT / "pipeline" / "cache" / "piper" / "en_GB-cori-medium.onnx"
VOICE_OUT_DIR = REPO_ROOT / "content" / "audio" / "voice" / "en"
SFX_OUT_DIR = REPO_ROOT / "content" / "audio" / "sfx"

SAMPLE_RATE = 24000


def _probe_duration_s(path: Path) -> float:
    out = subprocess.check_output(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            str(path),
        ],
        text=True,
    ).strip()
    return round(float(out), 3)


def _wav_to_ogg(
    wav_path: Path,
    ogg_path: Path,
    af_filter: str,
) -> float:
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-loglevel",
            "error",
            "-i",
            str(wav_path),
            "-ac",
            "1",
            "-ar",
            str(SAMPLE_RATE),
            "-af",
            af_filter,
            "-c:a",
            "libopus",
            "-b:a",
            "48k",
            str(ogg_path),
        ],
        check=True,
    )
    return _probe_duration_s(ogg_path)


def generate_voice_audio(
    voice_json_path: Path = VOICE_JSON,
    model_path: Path = MODEL_PATH,
    out_dir: Path = VOICE_OUT_DIR,
) -> Path:
    """Synthesise every entry in voice.json to mono 22.05 kHz OGG (~ -3 dBFS peak)."""
    out_dir.mkdir(parents=True, exist_ok=True)
    voice_data = json.loads(voice_json_path.read_text(encoding="utf-8"))
    entries: dict[str, str] = voice_data["entries"]
    voice = PiperVoice.load(str(model_path))

    manifest_items: list[dict[str, Any]] = []
    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = Path(tmp)
        for key, text in sorted(entries.items()):
            raw_wav = tmp_dir / f"{key}.wav"
            with wave.open(str(raw_wav), "wb") as wf:
                voice.synthesize_wav(text, wf)

            # Trim leading/trailing silence and normalise peak to ~ -3 dBFS
            ogg_name = f"{key}.ogg"
            ogg_path = out_dir / ogg_name
            af = (
                "silenceremove=start_periods=1:start_silence=0.04:start_threshold=-45dB,"
                "areverse,"
                "silenceremove=start_periods=1:start_silence=0.04:start_threshold=-45dB,"
                "areverse,"
                "loudnorm=I=-16:TP=-3.0:LRA=7"
            )
            dur = _wav_to_ogg(raw_wav, ogg_path, af)
            manifest_items.append(
                {
                    "key": key,
                    "file": ogg_name,
                    "text": text,
                    "durationS": dur,
                }
            )

    manifest = {
        "audioFormatVersion": "0.1",
        "language": "en-GB",
        "voiceModel": "en_GB-cori-medium",
        "licence": "CC-BY-4.0 (model/dataset) + MIT (Piper)",
        "entries": manifest_items,
    }
    manifest_path = out_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest_path


def _write_pcm_wav(path: Path, samples: np.ndarray, sr: int = SAMPLE_RATE) -> None:
    peak = float(np.max(np.abs(samples))) or 1.0
    norm = np.clip(samples / peak * 0.7, -1.0, 1.0)
    pcm = (norm * 32767.0).astype(np.int16)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sr)
        wf.writeframes(pcm.tobytes())


def _build_sfx_waveforms() -> dict[str, tuple[np.ndarray, bool]]:
    """Return {filename: (float32_samples, is_ambience)}."""
    sr = SAMPLE_RATE
    rng = np.random.default_rng(42)
    out: dict[str, tuple[np.ndarray, bool]] = {}

    def t_arr(dur: float) -> np.ndarray:
        return np.linspace(0.0, dur, int(sr * dur), endpoint=False)

    # Indicator relay clicks (on slightly brighter than off)
    t_clk = t_arr(0.06)
    out["indicator_on.ogg"] = (
        np.exp(-t_clk * 90.0)
        * (np.sin(2 * np.pi * 1400 * t_clk) + 0.5 * np.sin(2 * np.pi * 2800 * t_clk)),
        False,
    )
    out["indicator_off.ogg"] = (
        np.exp(-t_clk * 100.0)
        * (np.sin(2 * np.pi * 950 * t_clk) + 0.4 * np.sin(2 * np.pi * 1900 * t_clk)),
        False,
    )

    # Engine loops are sourced from recorded CC0 WAVs in pipeline/cache/sfx/
    # (see content/audio/LICENCES.md — not synthesised).

    # Short car horn (dual tone 400 + 500 Hz, 0.35 s)
    t_hrn = t_arr(0.35)
    env_hrn = np.minimum(1.0, t_hrn / 0.02) * np.minimum(1.0, (0.35 - t_hrn) / 0.03)
    out["horn.ogg"] = (
        env_hrn * (0.6 * np.sin(2 * np.pi * 400 * t_hrn) + 0.5 * np.sin(2 * np.pi * 500 * t_hrn)),
        False,
    )

    # Tyre squeal (filtered chirp + noise, 0.55 s)
    t_sq = t_arr(0.55)
    env_sq = np.minimum(1.0, t_sq / 0.04) * np.exp(-t_sq * 3.2)
    freq_sq = 1350.0 + 250.0 * np.sin(2 * np.pi * 9.0 * t_sq)
    out["tyre_squeal.ogg"] = (
        env_sq * (0.7 * np.sin(2 * np.pi * freq_sq * t_sq) + 0.3 * rng.normal(0.0, 1.0, len(t_sq))),
        False,
    )

    # UK puffin/pelican crossing beep (2.5 kHz, 0.14 s)
    t_bp = t_arr(0.14)
    env_bp = np.minimum(1.0, t_bp / 0.008) * np.minimum(1.0, (0.14 - t_bp) / 0.012)
    out["crossing_beep.ogg"] = (env_bp * np.sin(2 * np.pi * 2500 * t_bp), False)

    # UI click
    t_ui = t_arr(0.04)
    out["ui_click.ogg"] = (np.exp(-t_ui * 130.0) * np.sin(2 * np.pi * 1200 * t_ui), False)

    # Success chime (C5 -> E5 -> G5 arpeggio, 0.55 s)
    t_ch = t_arr(0.55)
    s_ok = np.zeros_like(t_ch)
    for start, freq in ((0.0, 523.25), (0.12, 659.25), (0.24, 783.99)):
        mask = t_ch >= start
        dt = np.maximum(0.0, t_ch - start)
        s_ok[mask] += np.exp(-dt[mask] * 7.0) * np.sin(2 * np.pi * freq * dt[mask])
    out["chime_success.ogg"] = (s_ok, False)

    # Fault chime (gentle descending minor two-note tone, 0.45 s)
    t_fl = t_arr(0.45)
    s_fl = np.zeros_like(t_fl)
    for start, freq in ((0.0, 440.0), (0.14, 349.23)):
        mask = t_fl >= start
        dt = np.maximum(0.0, t_fl - start)
        s_fl[mask] += np.exp(-dt[mask] * 8.0) * np.sin(2 * np.pi * freq * dt[mask])
    out["chime_fault.ogg"] = (s_fl, False)

    # Light town ambience loop (4.0 s pink-ish filtered rumble)
    t_amb = t_arr(4.0)
    raw_noise = rng.normal(0.0, 1.0, len(t_amb))
    # Smooth moving-average lowpass for soft distant traffic hum
    kernel = np.ones(45) / 45.0
    hum = np.convolve(raw_noise, kernel, mode="same") + 0.08 * np.sin(2 * np.pi * 48.0 * t_amb)
    out["ambience_town.ogg"] = (hum, True)

    return out


def generate_sfx_audio(out_dir: Path = SFX_OUT_DIR) -> Path:
    """Synthesise CC0 sound effects (-16 LUFS) and town ambience (-23 LUFS)."""
    out_dir.mkdir(parents=True, exist_ok=True)
    waveforms = _build_sfx_waveforms()
    manifest_items: list[dict[str, Any]] = []

    recorded_engines = {
        "engine_idle.ogg": (REPO_ROOT / "pipeline" / "cache" / "sfx" / "loop_0.wav", 3),
        "engine_low.ogg": (REPO_ROOT / "pipeline" / "cache" / "sfx" / "loop_2_0.wav", 4),
        "engine_mid.ogg": (REPO_ROOT / "pipeline" / "cache" / "sfx" / "loop_4_0.wav", 4),
    }

    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = Path(tmp)
        for fname, (src_wav, repeats) in sorted(recorded_engines.items()):
            ogg_path = out_dir / fname
            tiled_wav = tmp_dir / f"{fname}.tiled.wav"
            subprocess.run(
                [
                    "ffmpeg",
                    "-y",
                    "-loglevel",
                    "error",
                    "-stream_loop",
                    str(repeats - 1),
                    "-i",
                    str(src_wav),
                    str(tiled_wav),
                ],
                check=True,
            )
            dur = _wav_to_ogg(tiled_wav, ogg_path, "loudnorm=I=-16:TP=-1.5:LRA=7")
            manifest_items.append(
                {
                    "key": Path(fname).stem,
                    "file": fname,
                    "durationS": dur,
                    "targetLufs": -16,
                    "loop": True,
                }
            )
        for fname, (samples, is_ambience) in sorted(waveforms.items()):
            raw_wav = tmp_dir / f"{fname}.wav"
            _write_pcm_wav(raw_wav, samples)
            ogg_path = out_dir / fname
            target_lufs = -23 if is_ambience else -16
            af = f"loudnorm=I={target_lufs}:TP=-1.5:LRA=7"
            dur = _wav_to_ogg(raw_wav, ogg_path, af)
            manifest_items.append(
                {
                    "key": Path(fname).stem,
                    "file": fname,
                    "durationS": dur,
                    "targetLufs": target_lufs,
                    "loop": is_ambience or fname.startswith("engine_"),
                }
            )

    manifest = {
        "audioFormatVersion": "0.1",
        "licence": "CC0-1.0",
        "entries": manifest_items,
    }
    manifest_path = out_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest_path


if __name__ == "__main__":
    v_man = generate_voice_audio()
    s_man = generate_sfx_audio()
    print(f"Wrote {v_man} and {s_man}")
