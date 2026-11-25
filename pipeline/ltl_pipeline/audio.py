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

SAMPLE_RATE = 44100


def _probe_audio(path: Path) -> tuple[float, str, int, int]:
    out = subprocess.check_output(
        [
            "ffprobe",
            "-v",
            "error",
            "-select_streams",
            "a:0",
            "-show_entries",
            "format=duration:stream=codec_name,sample_rate,channels",
            "-of",
            "json",
            str(path),
        ],
        text=True,
    )
    info = json.loads(out)
    dur = round(float(info["format"]["duration"]), 3)
    st = info["streams"][0]
    return dur, st["codec_name"], int(st["sample_rate"]), int(st["channels"])


def _to_wav(
    src_wav: Path,
    out_wav: Path,
    af_filter: str,
) -> float:
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-loglevel",
            "error",
            "-i",
            str(src_wav),
            "-ac",
            "1",
            "-ar",
            str(SAMPLE_RATE),
            "-af",
            af_filter,
            "-c:a",
            "pcm_s16le",
            str(out_wav),
        ],
        check=True,
    )
    dur, codec, sr, ch = _probe_audio(out_wav)
    assert codec == "pcm_s16le" and sr == SAMPLE_RATE and ch == 1
    return dur


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
            wav_name = f"{key}.wav"
            wav_path = out_dir / wav_name
            af = (
                "silenceremove=start_periods=1:start_silence=0.04:start_threshold=-45dB,"
                "areverse,"
                "silenceremove=start_periods=1:start_silence=0.04:start_threshold=-45dB,"
                "areverse,"
                "loudnorm=I=-16:TP=-3.0:LRA=7"
            )
            dur = _to_wav(raw_wav, wav_path, af)
            manifest_items.append(
                {
                    "key": key,
                    "file": wav_name,
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
    """Return {filename: (float32_samples, is_ambience)} for UI chimes/clicks only."""
    sr = SAMPLE_RATE
    out: dict[str, tuple[np.ndarray, bool]] = {}

    def t_arr(dur: float) -> np.ndarray:
        return np.linspace(0.0, dur, int(sr * dur), endpoint=False)

    # UI click
    t_ui = t_arr(0.04)
    out["ui_click.wav"] = (np.exp(-t_ui * 130.0) * np.sin(2 * np.pi * 1200 * t_ui), False)

    # Success chime (C5 -> E5 -> G5 arpeggio, 0.55 s)
    t_ch = t_arr(0.55)
    s_ok = np.zeros_like(t_ch)
    for start, freq in ((0.0, 523.25), (0.12, 659.25), (0.24, 783.99)):
        mask = t_ch >= start
        dt = np.maximum(0.0, t_ch - start)
        s_ok[mask] += np.exp(-dt[mask] * 7.0) * np.sin(2 * np.pi * freq * dt[mask])
    out["chime_success.wav"] = (s_ok, False)

    # Fault chime (gentle descending minor two-note tone, 0.45 s)
    t_fl = t_arr(0.45)
    s_fl = np.zeros_like(t_fl)
    for start, freq in ((0.0, 440.0), (0.14, 349.23)):
        mask = t_fl >= start
        dt = np.maximum(0.0, t_fl - start)
        s_fl[mask] += np.exp(-dt[mask] * 8.0) * np.sin(2 * np.pi * freq * dt[mask])
    out["chime_fault.wav"] = (s_fl, False)

    return out


def generate_sfx_audio(out_dir: Path = SFX_OUT_DIR) -> Path:
    """Process recorded CC0 engine/car/street clips and UI chimes to 44.1 kHz mono WAV."""
    out_dir.mkdir(parents=True, exist_ok=True)
    waveforms = _build_sfx_waveforms()
    manifest_items: list[dict[str, Any]] = []
    cache_dir = REPO_ROOT / "pipeline" / "cache" / "sfx"

    recorded_engines = {
        "engine_idle.wav": (cache_dir / "loop_0.wav", 3),
        "engine_low.wav": (cache_dir / "loop_2_0.wav", 4),
        "engine_mid.wav": (cache_dir / "loop_4_0.wav", 4),
    }
    # (src_file, ss_offset, duration_s, target_lufs, is_loop)
    recorded_clips = {
        "engine_start.wav": (cache_dir / "engine_start.mp3", 0.0, 2.5, -16, False),
        "indicator_on.wav": (cache_dir / "blinkers.mp3", 0.35, 0.14, -16, False),
        "indicator_off.wav": (cache_dir / "blinkers.mp3", 0.72, 0.14, -16, False),
        "horn.wav": (cache_dir / "horn.mp3", 0.25, 0.65, -16, False),
        "tyre_squeal.wav": (cache_dir / "tyre_squeal.mp3", 0.5, 1.2, -16, False),
        "crossing_beep.wav": (cache_dir / "crossing_beep.mp3", 12.0, 0.8, -16, False),
        "ambience_town.wav": (cache_dir / "ambience_town.mp3", 10.0, 4.0, -23, True),
    }

    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = Path(tmp)
        for fname, (src_wav, repeats) in sorted(recorded_engines.items()):
            out_wav = out_dir / fname
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
            dur = _to_wav(tiled_wav, out_wav, "loudnorm=I=-16:TP=-1.5:LRA=7")
            manifest_items.append(
                {
                    "key": Path(fname).stem,
                    "file": fname,
                    "durationS": dur,
                    "targetLufs": -16,
                    "loop": True,
                }
            )
        for fname, (src_mp3, ss, seg_dur, target_lufs, is_loop) in sorted(recorded_clips.items()):
            out_wav = out_dir / fname
            trimmed_wav = tmp_dir / f"{fname}.trim.wav"
            subprocess.run(
                [
                    "ffmpeg",
                    "-y",
                    "-loglevel",
                    "error",
                    "-ss",
                    str(ss),
                    "-t",
                    str(seg_dur),
                    "-i",
                    str(src_mp3),
                    str(trimmed_wav),
                ],
                check=True,
            )
            fade_out = max(0.01, seg_dur - 0.02)
            af = (
                f"afade=t=in:st=0:d=0.01,"
                f"afade=t=out:st={fade_out}:d=0.02,"
                f"loudnorm=I={target_lufs}:TP=-1.5:LRA=7"
            )
            dur = _to_wav(trimmed_wav, out_wav, af)
            manifest_items.append(
                {
                    "key": Path(fname).stem,
                    "file": fname,
                    "durationS": dur,
                    "targetLufs": target_lufs,
                    "loop": is_loop,
                }
            )
        for fname, (samples, is_ambience) in sorted(waveforms.items()):
            raw_wav = tmp_dir / f"{fname}.raw.wav"
            _write_pcm_wav(raw_wav, samples)
            out_wav = out_dir / fname
            target_lufs = -23 if is_ambience else -16
            af = f"loudnorm=I={target_lufs}:TP=-1.5:LRA=7"
            dur = _to_wav(raw_wav, out_wav, af)
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
