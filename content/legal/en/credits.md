# Credits & Licences

**L to Local** ("Keep Left") uses the following open data, public sector information, and open-source software.

## Map Data (OpenStreetMap)
- **Attribution:** © OpenStreetMap contributors
- **Licence:** Open Database Licence v1.0 (**ODbL-1.0**) — https://opendatacommons.org/licenses/odbl/1-0/
- **Derived Database Access:** City road packs (including Wolverhampton) are derived from OpenStreetMap and are licensed under ODbL-1.0. You can request the derived road-pack data or reproduce it using the open pipeline in our repository.

## The Highway Code (Great Britain)
- **Source:** GOV.UK — https://www.gov.uk/guidance/the-highway-code
- **Licence:** **Open Government Licence v3.0** (OGL v3.0) — https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/
- **Disclaimer:** This app is an independent learning tool. It is **not endorsed by or affiliated with DVSA or DfT**.

## Traffic Sign Images (DfT / TSRGD)
- **Source:** Department for Transport, GOV.UK — https://www.gov.uk/guidance/traffic-sign-images
- **Licence:** Contains public sector information licensed under the **Open Government Licence v3.0** (**© Crown copyright**).
- **Scope:** Used in `content/countries/gb/signs/`, lesson badges (`content/lessons/gb/badges/`), and UI rule-group fault icons (`content/ui/icons/`).

## Audio & Coach Voice (`content/audio/`)
- **Piper TTS Engine:** MIT Licence — https://github.com/rhasspy/piper
- **Coach Voice (`en_GB-cori-medium`):** **CC BY 4.0** (`rhasspy/piper-voices`, Hi-Fi TTS dataset OpenSLR 109, Cori Samuel public-domain LibriVox recordings).
- **Recorded Engine Loops:** `car idle` (2002 Mini Cooper 1.6L) by `seth-m` (Freesound `269771`, **CC0 1.0 Universal**) for `engine_low.wav` and `engine_high.wav`; `engine_idle.mp3` and `engine_mid.mp3` are owner-supplied.
- **Recorded Car & Street SFX:** Freesound CC0 recordings by `GiocoSound`, `j1987`, `boedie`, `audible-edge`, `roisin.gleeson`, and `szegvari` (**CC0 1.0 Universal**).
- **UI Tones:** Procedurally synthesised for L to Local (**CC0 1.0 Universal**).

## Engine & Unity Packages
- **Unity:** Built with Unity 6.
- *(Unity Asset Store / UPM package notices to be added by the Unity branch.)*

## Python Content Pipeline (`pipeline/uv.lock`)
- **osmium** (pyosmium) — BSD-2-Clause
- **shapely** — BSD-3-Clause
- **pyproj** — MIT
- **jsonschema** — MIT
- **PyYAML** — MIT
- **requests** — Apache-2.0
- **numpy** — BSD-3-Clause
- **pytest** / **ruff** (dev/CI) — MIT
