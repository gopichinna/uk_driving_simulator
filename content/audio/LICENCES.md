# Audio Licences — L to Local

Every audio asset shipped in `content/audio/` has its engine, voice model, or sound source licence recorded here before generation. **No CC-BY-NC, ND, or unknown-licence assets are permitted.**

## 1. Coach Voice (`content/audio/voice/en/*.wav`, 16-bit PCM mono 44.1 kHz)

| Component | Name / ID | Source URL | Licence | Commercial Use |
|---|---|---|---|---|
| TTS Engine | Piper (`piper-tts` 1.8.0) | https://github.com/rhasspy/piper | MIT Licence | Yes |
| Voice Model | `en_GB-cori-medium` (22,050 Hz, `en_GB`) | https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_GB/cori/medium | CC BY 4.0 (model card & Hi-Fi TTS dataset) | Yes (with attribution) |
| Training Dataset | Hi-Fi Multi-Speaker English TTS Dataset (reader 92, Cori Samuel LibriVox recordings) | https://www.openslr.org/109/ | CC BY 4.0 (NVIDIA Corporation); underlying LibriVox recordings Public Domain | Yes (with attribution) |
| Line Count | 207 voice WAVs (43 base coach/UI lines + 80 `.v2`/`.v3` coach variety lines + 27 lesson learn-card narrations + 57 scenario briefing lines) | `content/audio/voice/en/manifest.json` | Same as above | Yes |

Required attribution (included in `content/ATTRIBUTIONS.md` and `content/legal/en/credits.md`):
> Coach voice generated with Piper (MIT) using `en_GB-cori-medium` (CC BY 4.0), trained on the Hi-Fi TTS dataset (NVIDIA, OpenSLR 109) from Cori Samuel's public-domain LibriVox recordings.

Checked: 2026-11-25 via `https://huggingface.co/rhasspy/piper-voices/raw/main/en/en_GB/cori/medium/MODEL_CARD`.

## 2. Recorded Sound Effects & Loops (`content/audio/sfx/*.wav`, 16-bit PCM mono 44.1 kHz)

All driving effects and loops are sourced from **CC0** recordings and normalised with `ffmpeg` (`loudnorm`: −16 LUFS for effects/engine loops, −23 LUFS for town ambience loop). UI clicks and chimes are clean UI tones.

| File | Description | Author | Source URL | Licence | Realism (1–5) | Target Loudness |
|---|---|---|---|---|---|---|
| `engine_idle.wav` | 2002 Mini Cooper 1.6L inline-4 idle loop (seamless crossfade) | seth-m | https://freesound.org/people/seth-m/sounds/269771/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 (small 1.6L hatchback idle) | −16 LUFS |
| `engine_low.wav` | 2002 Mini Cooper 1.6L inline-4 low-rev loop (~1500 rpm, crossfaded) | seth-m | https://freesound.org/people/seth-m/sounds/269771/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 (small 1.6L hatchback low revs) | −16 LUFS |
| `engine_mid.wav` | 2002 Mini Cooper 1.6L inline-4 mid-rev loop (~2500 rpm, crossfaded) | seth-m | https://freesound.org/people/seth-m/sounds/269771/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 (small 1.6L hatchback mid revs) | −16 LUFS |
| `engine_high.wav` | 2002 Mini Cooper 1.6L inline-4 high-rev loop (~3500 rpm, crossfaded) | seth-m | https://freesound.org/people/seth-m/sounds/269771/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 (small 1.6L hatchback high revs) | −16 LUFS |
| `engine_start.wav` | Recorded car engine start-up | GiocoSound | https://freesound.org/people/GiocoSound/sounds/401558/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −16 LUFS |
| `indicator_on.wav` | Real car indicator relay tick (on) | j1987 | https://freesound.org/people/j1987/sounds/94999/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −16 LUFS |
| `indicator_off.wav` | Real car indicator relay tick (off) | j1987 | https://freesound.org/people/j1987/sounds/94999/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −16 LUFS |
| `horn.wav` | Alfa Romeo MiTo car horn | boedie | https://freesound.org/people/boedie/sounds/457425/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 5/5 (real European hatchback) | −16 LUFS |
| `tyre_squeal.wav` | Recorded tyre squeal | audible-edge | https://freesound.org/people/audible-edge/sounds/71736/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −16 LUFS |
| `crossing_beep.wav` | Recorded pedestrian crossing beep | roisin.gleeson | https://freesound.org/people/roisin.gleeson/sounds/699144/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −16 LUFS |
| `ambience_town.wav` | Recorded city street traffic ambience | szegvari | https://freesound.org/people/szegvari/sounds/608158/ | CC0 1.0 Universal (`http://creativecommons.org/publicdomain/zero/1.0/`) | 4/5 | −23 LUFS |
| `ui_click.wav` | UI button click | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | 4/5 | −16 LUFS |
| `chime_success.wav` | Gentle UI success chime | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | 4/5 | −16 LUFS |
| `chime_fault.wav` | Gentle UI fault chime | L to Local contributors | `pipeline/ltl_pipeline/audio.py` | CC0 1.0 Universal | 4/5 | −16 LUFS |

### Commercial Engine-Pack Options (for owner review, since free CC0 engine loops scored 3/5)
If you want a dedicated small-petrol-hatchback multi-RPM set before launch, these Unity Asset Store packs are available (not purchased):
1. **Petrol Car Engine SFX Pack** (Stormwave Audio) — **$4.99**, Standard Unity Asset Store EULA.
2. **Realistic Engine Sounds — Lite Edition** (Skril Studio) — **$24.99**, Standard Unity Asset Store EULA.
3. **Realistic Engine Sounds — Plus Edition** (Skril Studio) — **$39.99**, Standard Unity Asset Store EULA.
