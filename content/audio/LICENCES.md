# Audio Licences — L to Local

Every audio asset shipped in `content/audio/` has its engine, voice model, or sound source licence recorded here before generation. **No CC-BY-NC or unknown-licence assets are permitted.**

## 1. Coach Voice (`content/audio/voice/en/*.ogg`)

| Component | Name / ID | Source URL | Licence | Commercial Use |
|---|---|---|---|---|
| TTS Engine | Piper (`piper-tts` 1.8.0) | https://github.com/rhasspy/piper | MIT Licence | Yes |
| Voice Model | `en_GB-cori-medium` (22,050 Hz, `en_GB`) | https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_GB/cori/medium | CC BY 4.0 (model card & Hi-Fi TTS dataset) | Yes (with attribution) |
| Training Dataset | Hi-Fi Multi-Speaker English TTS Dataset (reader 92, Cori Samuel LibriVox recordings) | https://www.openslr.org/109/ | CC BY 4.0 (NVIDIA Corporation); underlying LibriVox recordings Public Domain | Yes (with attribution) |

Required attribution (included in `content/ATTRIBUTIONS.md` and `content/legal/en/credits.md`):
> Coach voice generated with Piper (MIT) using `en_GB-cori-medium` (CC BY 4.0), trained on the Hi-Fi TTS dataset (NVIDIA, OpenSLR 109) from Cori Samuel's public-domain LibriVox recordings.

Checked: 2026-11-25 via `https://huggingface.co/rhasspy/piper-voices/raw/main/en/en_GB/cori/medium/MODEL_CARD`.

## 2. Sound Effects (`content/audio/sfx/*.ogg`)

All sound effects and loops are procedurally synthesised in Python (`pipeline/ltl_pipeline/audio.py`) specifically for **L to Local** and normalised with `ffmpeg` (`loudnorm`: −16 LUFS for one-shot effects, −23 LUFS for town ambience loop).

| File | Description | Author | Source | Licence | Target Loudness |
|---|---|---|---|---|---|
| `indicator_on.ogg` | Indicator relay click (on) | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `indicator_off.ogg` | Indicator relay click (off) | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `engine_idle.ogg` | Recorded car engine loop (idle) | domasx2 | https://opengameart.org/content/racing-car-engine-sound-loops (`loop_0.wav`) | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | −16 LUFS |
| `engine_low.ogg` | Recorded car engine loop (~1500 rpm) | domasx2 | https://opengameart.org/content/racing-car-engine-sound-loops (`loop_2_0.wav`) | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | −16 LUFS |
| `engine_mid.ogg` | Recorded car engine loop (~2500 rpm) | domasx2 | https://opengameart.org/content/racing-car-engine-sound-loops (`loop_4_0.wav`) | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | −16 LUFS |
| `horn.ogg` | Short car horn | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `tyre_squeal.ogg` | Short tyre squeal | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `crossing_beep.ogg` | UK puffin/pelican crossing beep | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `ui_click.ogg` | UI button click | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `chime_success.ogg` | Gentle success chime | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `chime_fault.ogg` | Gentle fault chime | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −16 LUFS |
| `ambience_town.ogg` | Light town ambience loop | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | −23 LUFS |
