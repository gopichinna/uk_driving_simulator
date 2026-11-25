# Hydrogen roadmap: rounds 9, 10 and 11 (do them in order without waiting)

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§3 ownership), then this file. Ownership is unchanged:
- **yours:** `pipeline/**`, `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/scenarios/**`, `content/legal/**`, `content/store/**`, `content/audio/**` (new), `content/ATTRIBUTIONS.md`, the pipeline CI workflows, and edits to `docs/02`;
- **not yours:** `core/**` and `unity/**`. Never edit them.

## How we work across rounds (Claude ↔ Hydrogen protocol)
You and Claude can't talk directly. **GitHub is the channel:**
1. **One branch and one PR per round.**
   - Round 9 branches from `main`: `hydrogen/r9-signs-street-furniture`.
   - Rounds 10 and 11 branch from the **previous round's branch** if its PR isn't merged yet (stacked), otherwise from `main`.
   - PR titles start with `Hydrogen R9:`, `Hydrogen R10:` or `Hydrogen R11:`.
2. **Open the PR as soon as a round is done, then move straight on to the next round.** Don't wait for a merge. Claude reviews and merges PRs in order.
3. **Before starting each round, read Claude's review comments** on your earlier PRs, then fix anything asked on the current branch and note it in that round's PR description:

   ```
   gh pr list --author @me --state all --limit 5
   gh pr view <n> --comments
   ```
4. **Questions for Claude:** add them under "Requests from Hydrogen" at the end of this file, in your current branch, and mention them in the PR description. Claude answers in a PR comment.
5. **Never guess facts.** Verify road rules, sign styles and test-centre details on **GOV.UK**, and record the URL and date checked. If you can't verify something, mark it `UNVERIFIED`.
6. **Every round ends with:** `ruff`, `pytest` and the readability linter passing, every content file validating against its schema, and a dated note in `docs/08` §6.

---

## Round 9: Wolverhampton signs and street furniture (fixes from the PR #8 review)
Goal: Wolverhampton's signs look and behave like the real city.

- [x] R9.1 **Coverage.** Put a direction sign on **every approach to every signalised junction and roundabout** in the main network (expect 40+ — 71 signs placed). Also add signs on primary/trunk give-way approaches.
- [x] R9.2 **Style.** On primary/trunk approaches (A4150 ring road, A41, A449, A454, A460 and any other `ref` A-road), use `primary` (green panel, white text, yellow route numbers such as `(A41)` — 17 primary signs). Elsewhere, use `local` (white panel, black text — 54 local signs). Cite GOV.UK "Know your traffic signs" for the colour rules (`https://www.gov.uk/government/publications/know-your-traffic-signs`, checked 2026-11-25).
- [x] R9.3 **Wording and symbols.**
  - Use generic destination names where UK signs do: **Railway Station**, **Bus Station**, **City Centre**, **Hospital**.
  - Add a `symbol` field to each row: `rail`, `bus`, `hospital`, `parking`, `city_centre`, `none`.
  - Only sign *named* car parks, with at most 4 rows per sign.
  - Additive `signs-placed.v0` fields are pre-agreed.
- [x] R9.4 **Speed limit signs.** `content/cities/wolverhampton/speed_signs.json` (168 signs: start pairs on each side at limit transitions + 20 mph repeaters). Put a roundel where the limit changes along a route, on each side as UK practice requires, plus repeaters on long 20 mph stretches if OSM tags them. Use `{ id, x, z, heading, mph, kind: "start" | "repeater" }`.
- [x] R9.5 **Bus stops and school warnings.**
  - `bus_stops.json` from OSM `highway=bus_stop`: `{ id, x, z, heading, name, linkId }` (85 stops).
  - `warning_signs.json` with school warning signs (Diagram 545 "Children going to or from school" + Diagram 546 "School" plate) on approaches within 150 m of each OSM `amenity=school`, plus OSM `traffic_calming=*` / `hazard=*` (`road_humps` Diagram 557.1, `road_narrows` Diagram 516/517, `pedestrians_in_road` Diagram 544, `queues_likely` Diagram 584, `bend_ahead` Diagram 512; 6 warning signs).
- [x] R9.6 **Tests** (`test_direction_signs.py`, `test_street_furniture.py`, 53/53 tests pass):
  - every sign is ≥ 0.5 m outside the carriageway;
  - every row's arrow matches the first turn of the shortest route;
  - primary style appears only on A-road approaches;
  - no unnamed car parks appear on signs.

## Round 10: Sound and the coach's voice (licence-checked)
Goal: audio content the game can ship, with every file's licence recorded.

- [x] R10.1 **Coach voice.** Generate speech for every line in `content/countries/gb/strings/en/voice.json`, using an **offline text-to-speech engine whose engine *and* voice model licences allow commercial use in an app** (Piper `piper-tts` 1.8.0 MIT + `en_GB-cori-medium` CC BY 4.0 from OpenSLR 109 / LibriVox, recorded in `content/audio/LICENCES.md` before generation).
  - Output: `content/audio/voice/en/<key>.ogg` (25 mono 24 kHz OGG files, peak ~ −3 dBFS, trimmed silence).
  - Add `content/audio/voice/en/manifest.json`: `{ key, file, text, durationS }`.
- [x] R10.2 **Sound effects.** Synthesised 12 CC0 1.0 effects & loops in `content/audio/sfx/*.ogg` (`indicator_on/off`, `engine_idle/low/mid`, `horn`, `tyre_squeal`, `crossing_beep`, `ui_click`, `chime_success`, `chime_fault`, `ambience_town`), normalised to −16 LUFS for effects and −23 LUFS for ambience, all recorded in `content/audio/LICENCES.md`.
- [x] R10.3 **Store screenshots and accessibility text.**
  - `content/store/en/screenshots.md`: 6 captions (each ≤ 8 words) matching **shipped** claims only (`claims.yaml`).
  - Accessibility labels for every HUD control and screen in `ui.json` under `a11y.*` keys (14 keys).
- [x] R10.4 **Tests** (`pipeline/tests/test_audio.py`, 57/57 tests pass):
  - every voice key has an audio file and every audio file has a licence entry;
  - durations are within 0.3–6 s for voice and ≤ 3 s for effects (except the loops);
  - `ATTRIBUTIONS.md` and `content/legal/en/credits.md` list the audio sources.

## Round 11: The area around Wolverhampton's driving test centre
Goal: let learners practise roads like the ones used on real tests near Wolverhampton. **DVSA doesn't publish test routes, so don't claim to copy them.** Model the *area* around the centre.

- [x] R11.1 **Verify on GOV.UK** ("Find a driving test centre", `https://www.gov.uk/find-driving-test-centre`, checked 2026-11-25): **Wolverhampton Practical Driving Test Centre**, 101 Spring Road, Spring Road Industrial Estate, Ettingshall, Wolverhampton, West Midlands **WV4 6JX**.
- [x] R11.2 **A second road pack** `content/cities/wolverhampton-test-area/roadpack.json`: 2 × 2 km around the centre (`52.5600, -2.1016`), including residential streets, roundabouts, A4123 dual carriageway, signals, buildings, POIs, direction signs, street names, speed signs, bus stops, and warning signs; tramways excluded.
- [x] R11.3 **Practice routes** (`content/cities/wolverhampton-test-area/routes.json`): 3 connected legal routes (18, 20, 22 min) starting and ending on Spring Road, mixing road types with sat-nav instructions and one `"Follow the signs towards City Centre ..."` segment per route, clearly labelled as **practice routes, not official DVSA test routes**.
- [x] R11.4 **Lesson 20, "Roads near the test centre".** Stage 8 (`20-roads-near-test-centre.json`), using `independent_driving` (`s8_20_test_centre_mix.json` mode `mixed`, `s8_20_test_centre_signs.json` mode `signs`), general plain-English learn card, and original quiz (`gb-s8-20-roads-near-test-centre.json`).
- [x] R11.5 **QA and tests** (`pipeline/tests/test_test_area.py`, 58/58 tests pass): `QA.md`, connected legal routes, schema validation (113 content files), ODbL attribution present.

---

## Requests from Claude
- Pre-agreed: new files `speed_signs.json`, `bus_stops.json` and `warning_signs.json` (same schema family as `signs-placed.v0`); additive `symbol` on sign rows; `content/audio/**` with `manifest.json`; `a11y.*` UI keys; a second city pack folder; lesson 20.
- Claude will render the R9 signs and furniture, play R10 audio in the game, and load the R11 pack as a new course.

## Requests from Hydrogen (to Claude)
*(add questions here)*
