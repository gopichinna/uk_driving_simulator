# Download & Build Size Breakdown (Android / Source Assets)

**Measured on:** 2026-11-27 (`content/` and `unity/Assets/Scenes/`). No shipped files were modified.

## 1. Where the Bytes Go

| Category | Path | Files | Uncompressed Size | Notes |
|---|---|---:|---:|---|
| **Voice WAVs** | `content/audio/voice/` | 327 | **99.31 MB** | Largest source asset group (16-bit PCM 44.1 kHz mono WAVs) |
| **Unity Scenes** | `unity/Assets/Scenes/` | 14 | **49.74 MB** | Generated 3D scenes & baked meshes |
| **City Road Packs** | `content/cities/` | 87 | **13.57 MB** | JSON roadpacks, buildings, POIs, signs (`coventry` ~3.1 MB, `wolverhampton`, `m54-j2`, etc.) |
| **SFX** | `content/audio/sfx/` | 22 | **3.55 MB** | Engine loops (`engine_idle.mp3`, `engine_mid.mp3`, `engine_low.wav`, `engine_high.wav`) + UI/ambience |
| **Diagrams** | `content/lessons/gb/diagrams/` | 65 | **0.56 MB** | Lesson PNG + SVG diagrams |
| **Badges** | `content/lessons/gb/badges/` | 64 | **0.26 MB** | 256×256 PNG + SVG badges |
| **UI Icons** | `content/ui/icons/` | 19 | **0.02 MB** | Fault group icons |
| **Other JSON/YAML** | `content/` (lessons, quizzes, scenarios, schemas) | 333 | **1.13 MB** | Negligible size; text compresses well |

*(Previous Android APK build was ~57 MB compressed from ~683 MB total source/library assets.)*

## 2. Safe Compression Options (No Quality Risk)
- **Voice WAVs → Unity Ogg Vorbis import (70–96 kbps mono):** Unity imports `.wav` and encodes to **Ogg Vorbis** at build time (**do not use Opus**, which Unity does not support cleanly on all mobile targets).
  - **Estimated saving:** ~99.31 MB source → **~11–14 MB** in APK (**~85 MB source-to-compressed reduction**, **~30–35 MB APK reduction** vs lightly compressed audio).
- **SFX WAVs → Ogg Vorbis for loops/ambience (`ambience_town.wav`, `rain_loop.wav`), ADPCM for short clicks:**
  - **Estimated saving:** **~2.0 MB** in APK with zero audible latency on indicator/chime clicks.
- **City JSON packs:** Already compressed by Android ZIP (~13.57 MB → **~3.4 MB** in APK). No custom compression needed.

## 3. First-Run / On-Demand Download Candidates
- **Optional city & practice packs (`coventry`, `m54-j2`, `queslett_roundabout`, `rural_practice`, `ring_road_practice`):**
  - Keep Stage 0–3 core (`town`, `roundabout`, `two_lane_roundabout`, `dual_carriageway`, `wolverhampton`) in the base APK so free lessons 1–3 and early stages launch offline immediately.
  - Move later-stage voice lines (Stages 4–8, ~55 MB raw / ~7 MB Vorbis) + optional city packs (~8 MB raw / ~2 MB zipped) to a **Play Asset Delivery** fast-follow / on-demand pack.
  - **Estimated base APK saving:** **~9–12 MB** (on top of Vorbis compression), keeping initial Play Store download well under **40 MB**.
