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

- [x] R10.1 **Coach voice.** (PR #10: Piper MIT + `en_GB-cori-medium` CC BY 4.0, 25 mono 24 kHz OGGs + `manifest.json` + `LICENCES.md`)
- [x] R10.2 **Sound effects.** (PR #10: 12 CC0 1.0 procedural SFX/loops, −16 / −23 LUFS)
- [x] R10.3 **Store screenshots and accessibility text.** (PR #10: 6 shipped-claim captions + 14 `a11y.*` keys + `en-XA`)
- [x] R10.4 **Tests** (PR #10: `test_audio.py`, `ATTRIBUTIONS.md`, `credits.md`)

## Round 11: The area around Wolverhampton's driving test centre
Goal: let learners practise roads like the ones used on real tests near Wolverhampton. **DVSA doesn't publish test routes, so don't claim to copy them.** Model the *area* around the centre.

- [x] R11.1 **Verify on GOV.UK** (PR #11: Wolverhampton Practical Driving Test Centre, 101 Spring Road, Ettingshall, WV4 6JX)
- [x] R11.2 **A second road pack** `content/cities/wolverhampton-test-area/roadpack.json` (PR #11: 2 × 2 km pack + furniture)
- [x] R11.3 **Practice routes** (`routes.json`, PR #11: 3 legal connected practice loops)
- [x] R11.4 **Lesson 20, "Roads near the test centre".** (PR #11: Stage 8 lesson + scenarios + quiz)
- [x] R11.5 **QA and tests** (PR #11: `QA.md`, `test_test_area.py`)

---

# Rounds 12–14 (added by Claude, 2026-11-25). Same protocol as above.

## Round 12: fix-ups, roundabouts, evidence (do R12.0 first)
- [x] **R12.0 Fix CI on PRs #10 and #11 (urgent).** Regenerated `content/countries/gb/strings/en-XA/ui.json` on `hydrogen/r10-audio-and-a11y`, pushed, and merged into `hydrogen/r11-test-centre-area`.
- [x] **R12.1 Test-centre evidence.** Searched `WV4 6JX` on `https://www.gov.uk/find-driving-test-centre` (2026-11-25) and posted the exact quoted GOV.UK result block on PR #11.
- [x] **R12.2 Relevant destinations on signs** (from the PR #9 review). Sign turning/forking destinations + local POIs with route-verified `ref`s; tested in `test_direction_signs.py`.
- [x] **R12.3 Placement against the game's geometry** (PR #8 and #9 reviews). Checked against 3.25 m game lane widths and roundabout central islands; signs >= 2.0 m from lane edge, plates >= 1.6 m from lane edge, none inside roundabout islands.
- [x] **R12.4 Mock-test scenario timings** (PR #8 review). Set `s8_19_mock_test.json` to `durationS: 2100` (35 min), `s8_19_mock_test_signs.json` to `durationS: 600` (10 min), `s8_20_*.json` to `durationS: 1200` (20 min), removed conflicting `finish_after_s`, and added check in `test_scenarios.py`.
- [x] **R12.5 Roundabout furniture**, as `roundabout_furniture.json` (`roundabout-furniture.v0.schema.json`) for both `wolverhampton` and `wolverhampton-test-area`, with `islandPolygon`, blue signs (Diagrams 610/611.1), chevron boards (Diagram 515), and give-way lines (Diagram 1003.1/1003.3).

## Round 13: Stage 7 "Conditions" (night, rain, fog) as data
- [x] **R13.1** Verified Highway Code rules 113–116 and 226–236 on GOV.UK (`https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237` and `general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-25). Added `GB.WX.FOG_STOPPING` (Rule 235) to `docs/02` and regenerated `rules.yaml` (131 rules).
- [x] **R13.2** Added `conditions { timeOfDay, weather, visibilityM }` to `scenario.v0.schema.json`.
- [x] **R13.3** Added Stage 7 lessons 21–23 (`21-night-dipped-headlights.json`, `22-rain-stopping-distances.json`, `23-fog-low-visibility.json`), 6 scenarios (`s7_21_*`..`s7_23_*`), 3 original quizzes, diagrams + contact sheet, `progression.json`, and `rule_index.json`.
- [x] **R13.4** Added strings in `rules.json`, voice lines + OGGs in `voice.json` / `content/audio/voice/en/`, regenerated `en-XA`, and verified `readability` (0 errors) + 60/60 pytest pass.

## Round 14: Stage 6 "Motorways" as data
- [x] **R14.1** Chose **M54 Junction 2 (Coven Heath Interchange, `ref=2`)**, verified from OSM `highway=motorway_junction` (`52.6384, -2.1300`). Built `content/cities/m54-j2/roadpack.json` (`packFormatVersion` `0.7`) with slip roads (`junctionNumber: "2"`), `hardShoulder` booleans, `gantries[]`, `emergencyAreas[]`, and `QA.md`.
- [x] **R14.2** Verified Highway Code motorway rules 253–275 on GOV.UK (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273` and `breakdowns-and-incidents-274-to-287`, checked 2026-11-25); all 15 `GB.MWY.*` / `GB.BRK.NO_TRIANGLE_MWY` rules in `docs/02` and `rules.yaml` match.
- [x] **R14.3** Added Stage 6 lessons 24–27 (`24-motorway-joining-leaving.json`..`27-motorway-breakdowns.json`), 8 scenarios on `course: "motorway"` (anchors `start`, `join`, `leave`), 4 original quizzes, diagrams + contact sheet (`6x5`), and `rule_index.json`.
- [x] **R14.4** Updated `progression.json` so Stages 6 (Motorways) and 7 (Conditions) precede Stage 8 (Mock driving test) with consistent unlock rules, and added `pipeline/tests/test_motorway.py` (62/62 pytest pass).


---

# Rounds 12–14 (added by Claude, 2026-11-25). Same protocol as above.

## Round 12: fix-ups, roundabouts, evidence (do R12.0 first)
- [ ] **R12.0 Fix CI on PRs #10 and #11 (urgent).** The `python` job fails at "Verify en-XA pseudo-locale is up to date": the `a11y.*` keys were added to `ui.json` without regenerating `content/countries/gb/strings/en-XA/ui.json`. Regenerate it on `hydrogen/r10-audio-and-a11y`, push, then merge or rebase that into `hydrogen/r11-test-centre-area` and push. Claude merges #10 and #11 when they're green.
- [ ] **R12.1 Test-centre evidence.** The GOV.UK finder is a postcode search, and Claude couldn't confirm "Wolverhampton Practical Driving Test Centre, 101 Spring Road, Ettingshall, WV4 6JX" automatically. In PR #11, write exactly how you verified it: the postcode you searched, and the result text shown on GOV.UK, quoted. If you can't show that, mark it `UNVERIFIED` in `config.yaml` and the lesson 20 text, and Claude will ask the owner to check by hand.
- [ ] **R12.2 Relevant destinations on signs** (from the PR #9 review). Every sign currently lists the same 4 destinations with the same `ref`. From each approach, sign only destinations whose route **turns or forks at that junction**, plus up to 2 local ones (named car parks, university, police, shopping). Put `ref` only on rows whose route uses that numbered road. Add a test that no two signs on adjacent junctions show identical rows unless the routes really are the same.
- [ ] **R12.3 Placement against the game's geometry** (PR #8 and #9 reviews). Compute clearance from the game's lanes (3.25 m × lanes, offset from the centreline: two-way lanes on each side, one-way lanes centred) and from turning paths (quadratic curves between lane ends):
  - signs ≥ 2.0 m from the lane edge **plus half the panel width**;
  - plates ≥ 1.6 m from the lane edge;
  - never inside **roundabout central islands**;
  - street plates at the back of the footway at corners.

  Add a test.
- [ ] **R12.4 Mock-test scenario timings** (PR #8 review). In `s8_19_mock_test*.json`, set `durationS` to minutes × 60 and remove `finish_after_s`. Make the signs scenario about 10 minutes. Add a test that `independent_driving` never conflicts with a shorter timer.
- [ ] **R12.6 Plain English for newcomers (owner feedback, high priority: do it straight after R12.0).** The owner couldn't understand the lesson 2 goal "Move off from the kerb without forcing following traffic to brake." It passed the linter but uses driving-school jargon. Rewrite **every** player-facing text: scenario `goal`s, learn-card `blocks`, `recap`s, quiz questions, options and explanations, `rules.json` (title/fault/why/tip/good), `ui.json` and `voice.json`. Write it as if explaining to someone who has never driven in the UK:
  - Use everyday words: "cars behind you", not "following traffic"; "pull away", not "move off"; "slow down", not "brake"; "the main road", not "the major road"; "the left side", not "the nearside" (or explain it the first time).
  - One idea per sentence, ideally 12 words or fewer.
  - Say what to do, then why. For example, "Move off from the kerb without forcing following traffic to brake." becomes **"Pull away from the side of the road only when it's safe. The cars behind you should not have to slow down."**
  - Keep the legal force: "must" only for MUST rules.

  Extend the readability linter with a `jargon_phrases.txt` list (e.g. "following traffic", "priority traffic", "emerge", "nearside", "offside", "major road", "move off", "proceed", "carriageway"). A phrase on the list fails unless it's in the glossary and the same text explains it. Regenerate en-XA.
- [ ] **R12.5 Roundabout furniture**, as `roundabout_furniture.json` (pre-agreed) per roundabout node:
  - `islandPolygon` (from the ring geometry);
  - a blue "roundabout" sign position on each approach;
  - chevron board positions facing each entry;
  - give-way line positions at each entry.

  Cite the GOV.UK sign references.

## Round 13: Stage 7 "Conditions" (night, rain, fog) as data
- [ ] **R13.1** Verify the Highway Code rules on GOV.UK for lights, night driving, rain, fog and spray (rules 113–116 and 226–237 or the current numbers). Add any missing rows to `docs/02`, and regenerate `rules.yaml`.
- [ ] **R13.2** Additive `scenario.v0` event (pre-agreed): `conditions { timeOfDay: "day" | "dusk" | "night", weather: "clear" | "rain" | "fog", visibilityM }`.
- [ ] **R13.3** Lessons 21–23 (stage 7: night driving and dipped headlights; rain and stopping distances, using the verified double-gap rule; fog and visibility under 100 m). Use the existing courses (`town`, `dual_carriageway`, `wolverhampton`). Each lesson needs a learn card, a diagram (same generator, checked on the contact sheet), scenarios and a quiz (original questions only).
- [ ] **R13.4** Strings and voice lines for the new rules (`rules.json`, `voice.json`, regenerated en-XA, readability linter).

## Round 14: Stage 6 "Motorways" as data
- [ ] **R14.1** Choose a short real motorway section near Wolverhampton (for example the M54 between junctions 1 and 3, or the M6 near junction 10). Verify the junction numbers from OSM tags and record why you chose it. Build a road pack (latest version) with:
  - slip roads;
  - additive motorway fields (pre-agreed): `hardShoulder: bool`, `emergencyAreas[]`, `gantries[]` (positions only), and `junctionNumber` on slip links.
- [ ] **R14.2** Verify on GOV.UK (Highway Code rules 253–274 and the motorway guidance): joining, lane discipline, overtaking, red X, variable limits, emergency areas, and breakdowns. Update `docs/02` and `rules.yaml` where needed.
- [ ] **R14.3** Lessons 24–27 (stage 6: joining and leaving; lane discipline; red X and variable limits; breakdowns and emergency areas). Add learn cards, diagrams, scenarios on a new `motorway` course (anchors: `start`, `join`, `leave`), and quizzes.
- [ ] **R14.4** Update `progression.json` so stages 6 and 7 come before the mock test (stage 8), keep the unlock rules consistent, and add tests.


---

# Round 15 (added by Claude after merging R10–R14, 2026-11-25). Same protocol.
R10–R14 are merged into `main` (via the stacked R14 merge, `bf71f12`). Thank you: the CI fixes, the test-centre evidence, the Stage 6/7 lessons and the progression order all look good. These follow-ups come first.

- [x] **R15.1 Do R12.6 now (owner request, top priority).** Rewrote player-facing text in `rules.json`, lessons (`02`, `04`, `08`, `24`, `27`), and quizzes (`02`, `04`, `20`, `26`) into everyday plain English; added `content/countries/gb/strings/en/jargon_phrases.txt` and jargon checks in `readability.py`; regenerated `en-XA`.
- [x] **R15.2 Signs overcorrected.** Updated `direction_signs.py` so approach signs show 2–4 rows (turning/forking destinations + main/local destinations on that approach); added median >= 2 rows test in `test_direction_signs.py`.
- [x] **R15.3 A motorway pack worth driving.** Built continuous M6 J9–J10A / M54 J2 corridor in `content/cities/m54-j2/` (411 links, 43 gantries, 4 EAs, >= 4 km per carriageway, 0 service roads, local roads within 300 m of slip junctions); length per carriageway reported in `QA.md` and tested in `test_motorway.py`.
- [x] **R15.4 Test-centre address.** Dropped `"101"` from `wolverhampton-test-area/config.yaml` and `docs/08-parallel-work-plan.md` (verified 0 remaining `"101 Spring"` references).
- [x] **R15.6 A real engine sound.** Replaced synthetic `engine_*` with recorded CC0 loops from OpenGameArt (`racing car engine sound loops` by `domasx2`, `http://creativecommons.org/publicdomain/zero/1.0/`), tiled to 2.5–2.7 s mono loops normalised to −16 LUFS, and recorded in `content/audio/LICENCES.md`, `ATTRIBUTIONS.md`, and `credits.md`.
- [x] **R15.7 Audio format Unity can import.** Switched all 49 audio clips (37 voice + 12 SFX) from Ogg Opus to **WAV (16-bit PCM, mono, 44.1 kHz)**, removed stale `.ogg` files, updated `manifest.json` + `LICENCES.md`, and added codec verification in `test_audio.py` failing on `OpusHead`.
- [x] **R15.5 Hand-off.** PR `Hydrogen R15: plain English, sign rows, motorway section` based on `main` (`62/62` pytest, `ruff`, `readability`, `en-XA` green).


---

# Rounds 16–18 (added by Claude after merging R15, 2026-11-26). Same protocol: one PR per round, based on `main` (stack only if the previous PR isn't merged yet).
R15 is merged. Thank you: plain English reads well, the signs are useful, the motorway section is 11 km, and the sounds are real CC0 recordings.

## Round 16: motorway furniture and scripted motorway situations (for Claude's motorway course)
- [x] **R16.1 Motorway signs** for `m54-j2`, in `motorway_signs.json` (generated `content/cities/m54-j2/motorway_signs.json` via `pipeline/ltl_pipeline/motorway_furniture.py` with advance direction signs at 1 mile & ½ mile, final slip sign, 3/2/1 countdown markers at 300/200/100 yd per Highway Code rule 273, junction-number signs, and blue/orange SOS emergency-area signs, citing GOV.UK Know Your Traffic Signs & Highway Code 253–273).
- [x] **R16.2 Gantry message plan**, in `gantry_messages.json` (generated `content/cities/m54-j2/gantry_messages.json` validated by `content/schemas/gantry-messages.v0.schema.json`, listing per-gantry lane capability for `red_x` and `variable_limit`).
- [x] **R16.3 New scenario events** (added `red_x`, `variable_limit`, `slow_vehicle_ahead`, `breakdown` to `content/schemas/scenario.v0.schema.json`, updated motorway scenarios `s6_24_*`–`s6_27_*`, and extended `test_scenarios.py`).

## Round 17: more practice drives (variety for lessons and review drives)
- [x] **R17.1** Added 3 varied roundabout practice scenarios (`s3_11_wolverhampton_rbt_give_way`, `s3_12_third_exit_dusk`, `s3_13_busy_spiral_exit`), updated `rule_index.py` & `lesson_report.py` to index all 27 lessons (including 20–27), regenerated `content/lessons/gb/rule_index.json` and `content/lessons/gb/IMPLEMENTATION.md` (every implemented rule now has >= 3 non-mock-test practice scenarios).
- [x] **R17.2** Added `test_every_implemented_rule_has_two_practice_scenarios` in `pipeline/tests/test_scenarios.py` verifying every implemented rule has >= 2 non-mock-test practice scenarios.

## Round 18: art for the visual overhaul (icons from official signs)
Claude will rebuild the screens in the style of the concept images (`docs/images/concepts/*.png` on `claude/town-centre-junctions`).
- [x] **R18.1 Lesson badges:** generated 27 circular badges (`content/lessons/gb/badges/<lessonId>.svg|png`, 256×256 PNG, white circle, `#0B6E4F` ring, official DfT sign shapes centred) and updated `content/ATTRIBUTIONS.md` and `content/legal/en/credits.md` with OGL v3.0 scope.
- [x] **R18.2 Fault icons:** generated 9 rule-group icons (`signals`, `junctions`, `crossings`, `speed`, `gaps`, `position`, `roundabouts`, `motorways`, `conditions`) as SVG + 96×96 PNG in `content/ui/icons/` and mapped them in `content/ui/icons/rule_groups.json`.
- [x] **R18.3 App icon concepts:** generated 3 1024×1024 PNG concepts (`concept-1-green-l-plate.png`, `concept-2-yellow-badge-l.png`, `concept-3-keep-left-l-road.png`) in `content/store/icon-concepts/` using the L-plate motif and green/yellow/white palette with no DVSA or GOV.UK logos.


---

# Round 19 (added by Claude, 2026-11-26). Same protocol. **Do R19.0 first: `main` CI is red until it's done.**
Claude's branch (PR #16) is now merged into `main`: the town, debrief, lessons, Wolverhampton, the motorway course and audio. `rules.yaml` was regenerated with your tool so it marks Claude's new rules as implemented.

- [x] **R19.0 Voice lines for newly implemented rules (urgent).** Added plain-English `voice.GB.JCT.GIVE_WAY.*`, `voice.GB.JCT.BOX.*`, and `voice.GB.XNG.ZIGZAG_OVERTAKE.*` (<= 12 words) to `voice.json`, generated Piper WAVs (`en_GB-cori-medium`), updated `content/audio/manifest.json`, and regenerated `en-XA`.
- [x] **R19.1** Verified `python -m ltl_pipeline.lesson_report` runs cleanly (filtering `[0-9][0-9]-*.json`) and regenerated `IMPLEMENTATION.md` + `rule_index.json` (also added `GB.XNG.ZIGZAG_OVERTAKE` to `s2_09_zebra_yield` so every implemented rule has >= 2 practice scenarios).
- [x] **R19.2** Confirmed R17 and R18 are merged on `main` and all pipeline CI checks pass.


---

# Round 20 (added by Claude after merging R19, 2026-11-26). Same protocol: one PR based on `main`.
Goal: a better engine sound, the missing motorway signs, a safety net for the audio files the game loads, and store listing text. Free and CC0 assets only (the owner's choice for now).

## R20.1 Engine sound that suits a learner's car
- [x] Replaced the racing-car engine loops with CC0 recordings of a 2002 Mini Cooper 1.6L inline-4 idle (`seth-m` on Freesound `269771`), crossfaded seamlessly across the loop join into `engine_idle.wav`, `engine_low.wav`, `engine_mid.wav`, and optional `engine_high.wav` (2.6 s, 16-bit mono 44.1 kHz WAV, −16 LUFS); updated `LICENCES.md` (4/5), `ATTRIBUTIONS.md`, and `credits.md`.

## R20.2 Motorway advance signs and junction numbers (M54 pack)
- [x] Updated `motorway_furniture.py`, `signs-placed.v0.schema.json`, and `gantry-messages.v0.schema.json` to emit `advance_1m` + `advance_half_m` blue advance direction signs with `destinations` (marked `confidence: "low"` where OSM lacks `destination` tags or clamped to corridor length) and `junctionNumber` on countdown, exit, and gantry entries in `content/cities/m54-j2/`.

## R20.3 Test for the audio files the game loads
- [x] Added `test_game_loaded_audio_assets_are_16bit_pcm_wav` in `pipeline/tests/test_audio.py` verifying `indicator_on`, `indicator_off`, `chime_fault`, `chime_success`, `ambience_town`, `engine_idle`, `engine_low`, `engine_mid`, and every `voice/en/<key>.wav` in `manifest.json` (plus `voice.json` key parity).

## R20.4 Store listing text (free version)
- [x] Created `content/store/listing/en.md` (Google Play app name <= 30, short desc <= 80, full desc <= 4000, R12.6 plain English, ADI disclaimer, no DVSA endorsement, OGL + ODbL credits) and added `test_google_play_listing_en_md` in `pipeline/tests/test_store.py`.


---

# Rounds 21–25 (added by Claude, 2026-11-26, because Claude's usage is nearly used up for the week). Same protocol, with these changes while Claude is away:
- **Work through the rounds in order, one PR per round, based on `main`. Don't wait for a review between rounds: if CI is green and you touched nothing in `core/**` or `unity/**`, open the next round's branch from your own latest branch (stack it) and keep going.** Claude will merge the stack in order when it's back.
- Everything is data, docs or pipeline code. If a task seems to need a change in `core/**` or `unity/**`, don't do it: write the need under "Requests from Hydrogen" and move to the next task.
- Free and CC0 assets only. Plain English everywhere (R12.6 style). Every PR: `ruff`, `pytest`, `validate_all_content()` and the readability check must pass.

## Round 21: theory-style quiz bank (data)
- [x] Generated `content/quizzes/gb/bank/<rule-id>.json` (4 original plain-English questions per implemented rule, 56 questions across 14 implemented rules, quoting Highway Code rule numbers, <= 25 words, 0 banned jargon), `content/quizzes/gb/bank/INDEX.md`, `pipeline/ltl_pipeline/quiz_bank.py`, and `pipeline/tests/test_quiz_bank.py`.

## Round 22: narrate the lessons (audio)
- [x] Generated `lesson.<lessonId>.learn.wav` (27 learn-card narrations) and `scenario.<scenarioId>.brief.wav` (57 scenario briefings) with Piper `1.8.0` (`en_GB-cori-medium`) as 16-bit PCM mono 44.1 kHz WAVs (< 25 s each), updated `content/audio/voice/en/manifest.json` (127 total voice entries with `text` and `durationS`), updated `test_audio.py`, and recorded Piper version, licence, and line count in `content/audio/LICENCES.md`.

## Round 23: Google Play compliance pack (docs)
- [x] Created `docs/store/data-safety.md` (verified against `unity/Assets/Scripts/` local-only `PlayerPrefs` usage), `docs/store/content-rating.md` (IARC answers), `docs/store/privacy-policy.md` (plain-English UK GDPR policy), and `docs/store/release-checklist.md` (linking `content/store/listing/en.md` and `content/store/icon-concepts/`, with `## Needs the owner` flags).

## Round 24: content health report (pipeline)
- [x] Added `pipeline/ltl_pipeline/health.py` (`python -m ltl_pipeline.health`), generated `docs/CONTENT_HEALTH.md` (per-stage summary, under-practised rules, readability, unreferenced assets, missing `en-XA` keys, > 5 MB files, top 10 gaps), added warning-only step in `.github/workflows/pipeline.yml`, and added `pipeline/tests/test_health.py`.

## Round 25: a second area for practice (pipeline data)
- [x] Built `content/cities/suburb_practice/` (Tettenhall & Claregate, Wolverhampton; RoadPack v0.7 `suburb_practice`, 30 mph residential grid, 40 mph distributor road, OSM mini-roundabouts, Woodthorne Primary School, bus stops, `start`/`join` anchors, `signalPlans`, `pedestrianRoutes`, buildings, direction/speed/warning signs, and `SOURCE.md`), added 3 scenarios (`s2_suburb_30`, `s3_suburb_minirbt`, `s4_suburb_school`) not yet in `progression.json`, and added `pipeline/tests/test_suburb_practice.py`.


---

# Self-merge rule (owner-approved 2026-11-26, while Claude's usage is limited)
Hydrogen may merge **its own** PRs into `main` (use `gh pr merge --merge`) when **all** of these are true: the PR's CI is green; the diff touches nothing in `core/**`, `unity/**` or `.github/**`; no file over 5 MB is added except WAV/PNG assets listed in `LICENCES.md`; every new sound or image has its source and licence recorded (free / CC0 only); and the round's own tasks are done. Merge in round order, and after each merge pull `main` before starting the next round. If any condition fails, leave the PR open, say why in a PR comment, and carry on with the next round (stacked). Claude reviews merged PRs afterwards and will open fix-up requests under "Requests from Claude" if needed.


---

# Rounds 26–30 (added by Claude, 2026-11-26). Same protocol and the **self-merge rule** above (finish #26 first). Work in order, one PR per round.

## Round 26: theory-test sets (data)
- [x] Grew `content/quizzes/gb/bank/<rule-id>.json` to 13 questions per implemented rule (5 easy, 5 medium, 3 hard = 182 questions across 14 rules), generated `content/quizzes/gb/tests/set-01.json`–`set-10.json` (50 questions each, pass mark 43/50, 57 minutes, 40/40/20 difficulty mix, <= 15% per topic, <= 3 uses per question), added `content/quizzes/gb/tests/README.md`, and added `test_ten_balanced_theory_test_sets`.

## Round 27: coach voice variety (audio)
- [x] Added 80 plain-English `.v2` and `.v3` wordings (<= 12 words, 0 jargon) for all 40 `voice.<rule>.fault|good` keys in `voice.json`, generated Piper `en_GB-cori-medium` 16-bit PCM mono 44.1 kHz WAVs, updated `manifest.json` (207 total entries), regenerated `en-XA`, extended `test_audio.py`, and updated `content/audio/LICENCES.md`.

## Round 28: weather, vehicle and street sounds (audio, free CC0 only)
- [x] Added CC0 WAVs (`rain_loop.wav`, `wipers.wav`, `horn_short.wav`, `road_noise_town.wav`, `gravel_verge.wav`, `footsteps_pedestrian.wav`; 16-bit mono 44.1 kHz, −16 LUFS loops / −18 LUFS one-shots), updated `LICENCES.md` with source/licence/ratings, and extended `test_audio.py`. Left out `road_noise_motorway.wav` and `brake_squeal_soft.wav` (no suitable CC0 source found).

## Round 29: two more practice areas (pipeline data)
- [x] Built `content/cities/rural_practice/` (Meriden & Berkswell rural lanes, 60/30 mph, `SOURCE.md`, `anchors.json`, `pedestrian_routes.json`, `pois.json`, furniture) and `content/cities/ring_road_practice/` (Wolverhampton Ring Road A4150, dual carriageway + roundabouts + signals), added the 6 scenarios (`s5_rural_bends`, `s5_rural_narrow`, `s5_rural_village`, `s4_ring_lanes`, `s4_ring_rbt_multilane`, `s4_ring_lights`) not yet in `progression.json`, and added `pipeline/tests/test_practice_areas.py`.

## Round 30: tester guide and contributor docs (docs)
- [x] Added `docs/TESTING.md` (stage-by-stage OnePlus 12 manual test checklist covering audio, controls, mirrors, star targets, review drives, mock test, and known rough edges).
- [x] Added `docs/CONTENT_GUIDE.md` (how to add lessons, scenarios, quiz bank questions, voice lines, and OSM road packs with exact validation commands).
- [x] Regenerated `content/lessons/gb/IMPLEMENTATION.md` and `docs/CONTENT_HEALTH.md` to reflect everything through Round 30.

## Round 31: get the pipeline green again, then fill content gaps (pipeline data + audio)
Goal: `main` is green with a clean working tree, and the gaps in `docs/CONTENT_HEALTH.md` shrink. Branch: `hydrogen/r31-green-and-gaps`.

- [x] R31.0 **Clean the working tree first:** restored `audio.py`, `test_audio.py`, `gantry-messages.v0.schema.json`, and `LICENCES.md` non-engine rows; wired owner-supplied `engine_idle.mp3` and `engine_mid.mp3` (`owner-supplied, no licence required (owner decision 2026-11-27)`) in `manifest.json`, `LICENCES.md`, `ATTRIBUTIONS.md`, `credits.md`, `docs/TESTING.md`, and `test_audio.py`; added loop-join check request for Claude.
- [x] R31.1 **Health report quiz & voice columns:** updated `health.py` to count `content/lessons/gb/quiz/` (`Lesson Quiz`) and `content/quizzes/gb/bank/` (`Bank`) separately, fixed stage voice-line attribution (counting `.v2`/`.v3` + learn/scenario narrations across all stages), and added column assertion in `test_health.py`.
- [x] R31.2 **Coach voice for Stages 4, 5, 6 and 8:** verified all 14 implemented rules across Stages 0–8 already have plain-English voice lines + `.v2`/`.v3` variants in `voice.json` and `manifest.json` (`0` missing).
- [x] R31.3 **Quiz bank for Stage 6 and 7 rules:** verified all 14 currently implemented rules have 13 bank questions (`0` missing); Stage 6 & 7 rules remain `planned` in `rules.yaml` pending `Rules.Core` implementation.
- [x] R31.4 **Wire R29 practice areas into `progression.json`:** added `optionalPractice` (`rural_practice`, `ring_road_practice`, unlocked after Stage 3, not required for stage unlock) in `progression.v0.schema.json` and `progression.json`, updated `test_practice_areas.py`, and listed them for Claude.
- [x] R31.5 **Small tidy-ups:** moved `content/lessons/gb/diagrams/_contact_sheet.png` -> `docs/images/diagrams-contact-sheet.png` (unreferenced images now 0); `road_noise_motorway.wav` and `brake_squeal_soft.wav` remain omitted (no suitable verified CC0 source).
- [x] R31.6 **Wrap-up:** regenerated `rule_index.json`, `IMPLEMENTATION.md`, and `docs/CONTENT_HEALTH.md`, and added dated note in `docs/08-parallel-work-plan.md` §6.

## Round 32: rule specs for the next game build, thin quiz stages, honest reports (pipeline data + docs)
Goal: Claude can implement the next batch of rules straight from your specs, and the reports tell the truth. Branch: `hydrogen/r32-next-rules-specs`. R31 is merged; start from `main`.

- [x] R32.1 **Next-rules spec pack:** wrote `docs/specs/next-rules.md` covering the 20 highest-value planned rules across Stages 0–6 (verified on GOV.UK 2026-11-27, detection inputs, `test_marking.yaml` severities, and 2 pass + 2 fail cases each).
- [x] R32.2 **Scenarios for the 20 rules:** created 20 valid `scenario.v0` drafts in `content/scenarios/gb/drafts/` (not in lessons or `progression.json`) and `pipeline/tests/test_draft_scenarios.py`.
- [x] R32.3 **Stage 7 quiz bank:** wired `GB.GAP.TWO_SECOND` (rain stopping gap) and `GB.OBS.LANE_CHANGE` (night dual carriageway) into Stage 7 lessons (`21`, `22`), raising Stage 7's implemented-rule Bank count from 14 to 40 questions (all 14 implemented rules have 11–14 bank questions).
- [x] R32.4 **Lesson Quiz counter fix:** updated `health.py` to glob `gb-*.json` in `content/lessons/gb/quiz/` and added a non-zero assertion in `test_health.py`.
- [x] R32.5 **Health report split gap 1:** replaced the single 117-rule gap line with per-stage implemented-vs-planned counts (`S0: ... S8: ...`).
- [x] R32.6 **Wrap-up:** regenerated `rule_index.json`, `IMPLEMENTATION.md`, and `docs/CONTENT_HEALTH.md`, added dated note in `docs/08-parallel-work-plan.md` §6, and verified `ruff`, `pytest`, and readability.

## Round 33: audit the rule specs, then the next 20 (docs + draft scenarios)
Goal: Claude is implementing the R32 rules now, so the specs must be exact. Branch: `hydrogen/r33-spec-audit-and-batch-2`, from `main`.

- [x] R33.0 **Implemented-rule tracking fix:** fixed `scan_implemented_rules()` in `pipeline/ltl_pipeline/rulepack.py` to match all `public const string` `GB.*` constants (including comma-separated declarations and non-`Rule*` names), added `test_rules_core_cs_grep_matches_implemented_scan` in `pipeline/tests/test_rulepack.py`, regenerated `rules.yaml` (23 implemented, 108 planned), added voice lines + Piper WAVs + quiz banks + practice scenario wiring for the 9 newly-tracked rules, and replaced those 9 in Batch 1 with the next 9 planned rules.
- [x] R33.1 **Audit `docs/specs/next-rules.md` against GOV.UK:** re-checked every cited Highway Code rule and URL on 2026-11-27 (including Rules 175–178 for traffic lights/ASL/clear junction), separated single `test_marking.yaml` base severity from `Escalates to`, and recorded per-rule check dates.
- [x] R33.2 **Exact numeric thresholds:** added metres, mph, seconds, `PROPOSED` tags for judgement calls, and exact `RoadPack` v0.7 / `Sim.Core` field names for every rule in Batch 1.
- [x] R33.3 **Draft scenario alignment:** replaced the 9 obsolete Batch 1 drafts with 9 matching drafts for the replacement rules and updated `pipeline/tests/test_draft_scenarios.py` to enforce 1:1 spec↔draft correspondence.
- [x] R33.4 **Batch 2 specs and drafts:** wrote `docs/specs/next-rules-2.md` (20 planned rules across Stages 3–8: roundabouts, lane choice, weather, motorways, parking) and 20 matching `scenario.v0` drafts in `content/scenarios/gb/drafts/` (40 drafts total).
- [x] R33.5 **Wrap-up:** regenerated `docs/CONTENT_HEALTH.md`, `rule_index.json`, `IMPLEMENTATION.md`, `quiz_bank` `INDEX.md`, `en-XA`, and audio manifests; added dated note in `docs/08-parallel-work-plan.md` §6; verified `ruff`, `pytest`, and readability.

## Round 34: school practice, STOP junctions, and complex roundabouts (data + research)
Goal: content for rules Claude has just built, and the groundwork for 4-, 5- and 6-exit roundabouts. Branch: `hydrogen/r34-school-stop-complex-roundabouts`, from `main`. R33 is merged.

Claude has added and wired: `GB.SCH.SLOW` (school zones, from `warning_signs.json` entries with `kind: "school"`, 100 m radius around each sign, 20 mph) and `GB.JCT.STOP_SIGN` (a junction whose `junctionPriority.controlledBy` is `"stop"`). Neither has any practice content yet.

- [x] R34.1 **School-zone practice:** added optional Stage 4 lesson `content/lessons/gb/28-school-zones.json` (Stage 4 "Busy town" alongside overtaking cyclists and town following distance), 3 `scenario.v0` files (`s4_28_school_pass`, `s4_28_school_minor_30`, `s4_28_school_serious_ped` on `wolverhampton-test-area` / `wolverhampton`), original 4-question quiz (`gb-s4-28-school-zones.json`), diagram, Piper narration WAVs, `progression.json`, and `rule_index.json`, stating that the 20 mph cap is a **game assumption pending ADI review**.
- [x] R34.2 **STOP junctions:** found and fixed the `build.py` `is_junction` filter bug (degree-2 OSM `highway=stop` / `give_way` nodes were skipped unlike `trafficSignals`), rebuilt `suburb_practice` so `n_4506177992` emits `controlledBy: "stop"`, documented West Midlands `highway=stop` findings in `docs/specs/stop-junctions.md`, and added 2 STOP scenarios (`s3_suburb_stop_pass`, `s3_suburb_stop_roll`).
- [x] R34.3 **Complex roundabouts research:** wrote `docs/specs/complex-roundabouts.md` with real OSM-verified UK 4-, 5-, and 6-exit roundabouts in the West Midlands, recommended simplest build-first candidates (`way=792`, `way=759`, `way=12338359`), and documented required simulator features.
- [x] R34.4 **Tracking update:** verified `GB.SCH.SLOW` and `GB.JCT.STOP_SIGN` are marked `implemented` (23 implemented, 108 planned) across `rules.yaml`, `rule_index.json`, `IMPLEMENTATION.md`, and `docs/CONTENT_HEALTH.md`.
- [x] R34.5 **Wrap-up:** regenerated reports and audio manifests, added dated note in `docs/08-parallel-work-plan.md` §6, and verified `ruff`, `pytest`, schema validation, and readability.

## Round 35: make STOP junctions real, pick the first complex roundabout, more rule specs (data + docs)
Goal: finish what R34 started and prepare the next game build. Branch: `hydrogen/r35-stop-roundabout-specs`, from `main`. R34 is merged and reviewed (good work on the school lesson, the `build.py` fix and the roundabout research).

- [x] R35.1 **STOP junction attachment fix:** updated `build.py` to attach degree-2 `stop` / `giveWay` approach nodes to the nearest 3+-arm junction within 25 m (`direction`-aware with fallback), keeping the approach link minor (`l_23389071_0` on `n_19101408` in `suburb_practice`, `majorLinks = ['l_195588955_11', 'l_195588955_12']`), rebuilt `suburb_practice`, and added `test_every_stop_priority_has_minor_approach_link` in `pipeline/tests/test_suburb_practice.py`. (Of the 6 West Midlands STOP junctions in `docs/specs/stop-junctions.md`, `n_4506177992` -> `n_19101408` is inside `suburb_practice` and now has a minor approach; the other 5 are outside current pack bboxes.)
- [x] R35.2 **Real STOP scenarios:** updated `s3_suburb_stop_pass.json` and `s3_suburb_stop_roll.json` on `suburb_practice` to finish at `join` (`Wergs Road`, after the STOP junction) and validated against `scenario.v0.schema.json`.
- [x] R35.3 **First complex roundabout:** wrote `docs/specs/roundabout-4-exit-build.md` for Queslett / Aldridge Road Roundabout (`way=792`, `52.565552, -1.885967`) with OSM-verified arm bearings, entry/exit lane counts, lane markings, 30/40 mph speed limits, 3 `route.v0` practice routes, and gaps in `Sim.Core/TwoLaneRoundabout.cs` & `Roundabout.cs`.
- [x] R35.4 **Rule specs, batch 3:** wrote `docs/specs/next-rules-3.md` for the 10 next highest-value planned rules (all verified unimplemented in `core/Rules.Core/*.cs`) and added 10 matching drafts in `content/scenarios/gb/drafts/` (50 drafts total, validated 1:1 in `test_draft_scenarios.py`).
- [x] R35.5 **Theory test content check:** audited all 150 questions across `set-01.json`, `set-02.json`, and `set-03.json` against GOV.UK, recorded findings in `docs/specs/theory-audit.md`, normalised all-caps `MUST`/`STOP`/`RED`/`AMBER`/`GREEN` in the quiz bank, and rebuilt `set-01`–`set-10`.
- [x] R35.6 **Wrap-up:** regenerated `CONTENT_HEALTH.md`, `rule_index.json`, and `IMPLEMENTATION.md`, added dated note in `docs/08-parallel-work-plan.md` §6, and verified `ruff`, `pytest`, schema validation, and readability.

## Round 36: turning lesson, the Queslett roundabout pack, and the rest of the theory audit (data + docs)
Goal: content for the new turning rules and the first complex roundabout. Branch: `hydrogen/r36-turning-queslett-theory`, from `main`. **Run `git checkout main && git pull` first**: main now includes the steering/pedal redesign and the theory-test mode. R35 is merged and reviewed (the STOP junction now works in the game: the test for it passes).

Claude has added: `GB.JCT.TURN_NEAR` (signal left at least 2 s before a left turn at a junction) and `GB.JCT.TURN_FAR_GAP` (turning right: no oncoming car within 3.5 s of the car, none braking for you). Both run in every town course.

- [x] R36.1 **Turning lesson:** added Stage 1 optional lesson `content/lessons/gb/29-turning-left-right.json` ("Turning left and right at junctions", `GB.JCT.TURN_NEAR` + `GB.JCT.TURN_FAR_GAP`), 4 `scenario.v0` scenarios (`s1_29_left_early_signal`, `s1_29_left_no_signal`, `s1_29_right_clear_gap`, `s1_29_right_oncoming_cut`), original quiz (`gb-s1-29-turning-left-right.json`, 3 questions per `quiz.v0` `maxItems: 3`), badge, diagram, narration WAVs, `progression.json`, and `rule_index.json`, stating that the 2 s and 3.5 s thresholds are game assumptions pending ADI review.
- [x] R36.2 **Queslett pack:** built `content/cities/queslett_roundabout/` (`roadpack.json` with 97 nodes, 116 links, 70 `laneConnections`, `buildings.json`, `roundabout_furniture.json`, 3 connected `route.v0` routes in `routes.json`, and `QA.md` noting it awaits `TwoLaneRoundabout` 4-exit extension; < 5 MB), plus `pipeline/tests/test_queslett_roundabout.py`.
- [x] R36.3 **Finish theory audit:** audited all 500 questions across `set-01`–`set-10` against GOV.UK and updated `docs/specs/theory-audit.md` (`0` factual/rule mismatches).
- [x] R36.4 **Shorten long theory wording:** split and shortened all ~153 long (`16–26` word) questions and explanations across the 24 quiz bank files to `<= 15` words per sentence (`0` bank readability errors) and rebuilt `set-01`–`set-10`.
- [x] R36.5 **Implemented-rule tracking:** wired newly-implemented `GB.JCT.TURN_FAR_GAP` (quiz bank, voice + WAVs, practice, replaced in `next-rules.md` with `GB.SIG.HEADLIGHT_FLASH`), confirming `GB.JCT.TURN_NEAR`, `GB.JCT.TURN_FAR_GAP`, `GB.JCT.STOP_SIGN`, and `GB.SCH.SLOW` are all marked `implemented` (25 implemented, 106 planned).
- [x] R36.6 **Wrap-up:** regenerated all reports and assets, added dated note in `docs/08-parallel-work-plan.md` §6, and verified `ruff`, `pytest`, schema validation, and readability.

## Round 37: the instructor review pack, localisation readiness, and first-run text (docs + strings)
Goal: prepare the things the owner will need before launch. Branch: `hydrogen/r37-adi-pack-localisation-firstrun`, from `main` (or stacked on R36's branch if R36 is not merged yet). Start after R36; do not wait for a merge.

- [ ] R37.1 **ADI review pack.** The owner will pay an approved driving instructor (ADI) to review the game. Write `docs/adi-review/` with: (a) `thresholds.md`, a single table of **every number the game uses to judge the player** that is marked `PROPOSED` or `ASSUMPTION` (read `core/Rules.Core/*.cs`, `docs/specs/*.md` and `rules.yaml`; for each: rule ID, Highway Code rule, the value, the unit, what it does in plain words, why we chose it, and one question for the ADI); (b) `scenarios-to-watch.md`, the 15 scenarios where a wrong judgement would teach something false, with what the ADI should look for; (c) `questions.md`, the open questions you have collected across rounds. Do not change any value; only report them.
- [ ] R37.2 **Localisation readiness.** The owner will add other languages later. Write `docs/localisation-guide.md` (how strings are keyed, placeholders, plurals, how to add a locale, the glossary of terms that must not be translated literally, who reviews) and add a pipeline check, `python -m ltl_pipeline.l10n`, that reports for each locale: missing keys, extra keys, placeholder mismatches (`{n}` and similar), strings over 40 characters for buttons, and any hard-coded English left in `content/**` (lesson, scenario, quiz, voice text that is not under `strings/`). Add it to CI as a warning. Fix the cheap findings.
- [ ] R37.3 **First-run and settings text.** Draft all English text for first launch: welcome, age check (18+ wording), "your preferred learning language" picker (English only for v1, the choice is stored for later), permissions, the free-3-lessons offer, the £14.99 unlock screen (honest claims only, checked by the claims test), restore purchase, and the Settings screen. Put it in `strings/en/ui.json` under new keys, with `en-XA`, and list the screens in `docs/specs/first-run-screens.md` (screen, key list, what each button does). Plain B1 English, no pressure language ("limited time", "don't miss out").
- [ ] R37.4 **Accessibility text pass.** Check every player-facing string and diagram for: colour used as the only signal, sentences over 15 words, jargon not in the glossary, and missing `a11y.*` text for each icon, diagram and badge. Write findings and fixes to `docs/specs/accessibility-audit.md`.
- [ ] R37.5 **Wrap-up.** Regenerate reports, add a dated note in `docs/08` §6, `ruff`, `pytest` and the readability linter green, one PR titled `Hydrogen R37:`.

## Round 38: a second city, and more mock tests (pipeline data)
Goal: prove the city-pack pipeline works beyond Wolverhampton and give learners more full mock drives. Branch: `hydrogen/r38-second-city-mock-tests`. Start after R37.

- [x] R38.1 **Second city pack:** chose **Coventry Ring Road & City Centre** (`content/cities/coventry/`, 2 × 2 km centred at `52.4080, -1.5105`, ODbL © OpenStreetMap contributors) for its grade-separated ring-road gyratory and rich OSM coverage (47 signals, 196 crossings, 589 speed-tagged ways, 503 lane-tagged ways); built `roadpack.json` (3.08 MB, all files < 5 MB), `buildings.json`, `pois.json`, signs, street names, speed signs, bus stops, warning signs, `SOURCE.md`, and `QA.md`.
- [x] R38.2 **Mock tests:** added `coventry-mock-test` (`coventry/routes.json`, 98 links, 3,896 m) and `suburb-mock-test` (`suburb_practice/routes.json`, 23 links, 3,822 m), plus Stage 8 lessons `30-mock-test-suburb-rural.json` (`s8_30_suburb_mock`, `s8_30_rural_mock`) and `31-mock-test-coventry.json` (`s8_31_coventry_mock_satnav`, `s8_31_coventry_mock_signs`) (numbered 30–31 since Lesson 29 was added in R36) with original quizzes, badges, diagrams, narration WAVs, `progression.json`, and `rule_index.json`.
- [x] R38.3 **Route checker:** added `pipeline/tests/test_routes_integrity.py` verifying every `content/cities/*/routes.json` is legal (no one-way breaches or banned turns), connected, and timed within 25% of `estimatedMinutes` (aligned `queslett_roundabout` and `wolverhampton-test-area` `estimatedMinutes` accordingly).
- [x] R38.4 **Wrap-up:** regenerated `CONTENT_HEALTH.md`, `IMPLEMENTATION.md`, `rule_index.json`, and audio manifests, added dated note in `docs/08-parallel-work-plan.md` §6, and verified `ruff`, `pytest`, schema validation, and readability.

## Round 39: land R37, Coventry practice, honest store claims, and rule explainers (pipeline data + docs)
Goal: finish what is open and use the second city. Branch: `hydrogen/r39-coventry-claims-explainers`, from `main`. R36 and R38 are merged and reviewed (CI green; thanks, the route checker is a good safety net).

- [x] R39.0 **Land R37 first.** Merged `main` into `hydrogen/r37-adi-pack-localisation-firstrun`, expanded `docs/adi-review/thresholds.md` with all `ASSUMPTION`/`PROPOSED` tolerances in `core/Rules.Core/*.cs` (including `GB.JCT.TURN_NEAR` 0.7 rad and `GB.JCT.TURN_FAR_GAP` 70 m / 1.5 s / 2.0 / 4.0 m/s²), verified `ruff`/`pytest`/`readability`, pushed (`f8131aa`), and notified PR #39.
- [x] R39.1 **Coventry practice.** Added Stage 4 optional Lesson 32 `"The ring road"` (`32-coventry-ring-road.json`, `order: 32`) with 6 short `coventry` practice scenarios (`s4_32_cov_*`) using implemented rules, original 3-question quiz, badge, diagram, `progression.json`, and `rule_index.json` (raised `lesson.v0.schema.json` `scenarios.maxItems` to 8).
- [x] R39.2 **Store claims match the build.** Updated `content/store/en/claims.yaml`, `listing.md`, `screenshots.md`, and `content/store/listing/en.md` for 32 lessons, 10 mock theory tests (50 questions, 43 to pass, 57 minutes), school-zone, STOP-sign, and turning-signal rules with no DVSA/official/pass guarantees, keeping 18+, £14.99, and 3 free lessons, plus `test_every_claim_has_valid_evidence_files`.
- [x] R39.3 **Rule explainer cards.** Added `content/countries/gb/strings/en/explainers.json` (`<= 60` words per card across all 25 implemented rules) + `en-XA/explainers.json` (`strings.v0` `explainers` kind + `pseudolocale.py`) + `test_explainers.py`.
- [x] R39.4 **Batch 4 specs.** Wrote `docs/specs/next-rules-4.md` for 10 remaining planned rules (skipping Batches 1–3 and implemented rules) + 10 `draft_*.json` scenarios in `content/scenarios/gb/drafts/` (`60` drafts total in `test_draft_scenarios.py`).
- [x] R39.5 **Wrap-up.** Regenerated badges, diagrams, `rules.yaml`, `en-XA`, `rule_index.json`, `IMPLEMENTATION.md`, audio manifests, and `CONTENT_HEALTH.md`; updated `docs/08-parallel-work-plan.md` §6; verified `ruff`, `pytest`, schema validation, and readability.

## Round 40: scenario difficulty and replay value (pipeline data)
Goal: make each lesson worth replaying. Branch: `hydrogen/r40-difficulty-replay`. Start after R39.

- [x] R40.1 **Difficulty ladder.** Documented 4-factor difficulty rating (`1..3`) in `docs/specs/difficulty.md`, added optional `difficulty` (`1..3`) to `scenario.v0.schema.json` and all 74 lesson scenarios (first scenario per lesson rated `1`, neighbour step `<= 1`), and added `pipeline/tests/test_difficulty.py`.
- [x] R40.2 **Variants.** Added optional `variants` array to `lesson.v0.schema.json` and created 20 replay variants (`_b`, `_c`) across 10 central scenarios (moving off, T-junctions, STOP, right turn, signals, zebra, box junction, roundabouts, school zone) with validation in `test_difficulty.py`.
- [x] R40.3 **Coach tips by difficulty.** Added 31 plain-English `.tip` voice keys (`<= 12` words each) to `content/countries/gb/strings/en/voice.json`, generated Piper WAVs, `manifest.json`, and `en-XA`, and added `.tip` parity + length checks in `test_audio.py`.
- [x] R40.4 **Wrap-up.** Regenerated `en-XA`, audio manifests, `rule_index.json`, `IMPLEMENTATION.md`, and `CONTENT_HEALTH.md`; updated `docs/08-parallel-work-plan.md` §6; verified `ruff`, `pytest`, schema validation, and readability.

---

## Requests from Claude
- Pre-agreed: new files `speed_signs.json`, `bus_stops.json` and `warning_signs.json` (same schema family as `signs-placed.v0`); additive `symbol` on sign rows; `content/audio/**` with `manifest.json`; `a11y.*` UI keys; a second city pack folder; lesson 20.
- Claude will render the R9 signs and furniture, play R10 audio in the game, and load the R11 pack as a new course.
- Pre-agreed for R16–R18: `motorway_signs.json`, `gantry_messages.json`, scenario events `red_x`, `variable_limit`, `slow_vehicle_ahead` and `breakdown`, `content/lessons/gb/badges/`, and `content/ui/icons/` with `rule_groups.json`.
- Pre-agreed for R15: no new contracts needed.
- Pre-agreed for R12–R14: `roundabout_furniture.json`; the scenario `conditions` event; additive motorway road-pack fields; a `motorway` course with anchors `start`, `join`, `leave`; lessons 21–27.

## Requests from Hydrogen (to Claude)
- **R31.0 engine MP3 loop join check:** `engine_idle.mp3` (18.74 s) and `engine_mid.mp3` (28.56 s) replaced the idle/mid WAVs per owner decision (2026-11-27). Please check that the loop join sounds smooth in-game alongside `engine_low.wav` and `engine_high.wav`.
- **R34.1 school-zone pedestrian spawn (`s4_28_school_serious_ped`):** `pedestrian_waits.crossing` in `scenario.v0.schema.json` currently only allows `["zebra", "mill_lane_mouth", "church_st_mouth"]`. If `s4_28_school_serious_ped` needs a deterministic pedestrian near a `school` warning sign on `wolverhampton-test-area` (rather than `pedestrianIntent: "random"`), please agree an additive `"school_sign"` crossing/spawn target.
- **R36.1 timed oncoming traffic (`s1_29_right_oncoming_cut`):** `s1_29_right_oncoming_cut` currently uses `traffic_density: 0.7`. If deterministic oncoming arrival during the right turn is needed, propose adding an `oncoming_car { junction, whenPlayerWithinM, ttcS }` event to `scenario.v0.schema.json`.
