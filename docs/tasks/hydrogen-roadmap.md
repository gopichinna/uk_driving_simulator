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
Add `python -m ltl_pipeline.health` that writes `docs/CONTENT_HEALTH.md`: per stage, the number of lessons, scenarios, quiz questions, voice lines and badges; rules with fewer than 2 practice drives; text over the readability limit; sounds or images not referenced anywhere; strings that exist in `en` but not `en-XA`; any file over 5 MB. Run it in CI as a warning (not a failure) and commit the first report. List the top 10 gaps at the end of the report in priority order.

## Round 25: a second area for practice (pipeline data)
Using the same OSM pipeline and licence record as the Wolverhampton packs, build one more `roadpack`: a **suburban area with a 30 mph residential grid, a 40 mph distributor road, a mini-roundabout and a primary school with zig-zags**, somewhere in the West Midlands (pick it from OSM, state the bounding box and the OSM date in the pack's `SOURCE.md`). Contract: same pack schema as `wolverhampton_test_area` (v0.7), id `suburb_practice`, with `start`/`join` anchors, signal plans, pedestrian routes and POIs (school, bus stops). Add 3 scenarios in `content/scenarios/gb/` that use it (`s2_suburb_30`, `s3_suburb_minirbt`, `s4_suburb_school`) in the same format as the existing ones, **not yet listed in `progression.json`** (Claude will wire the scene and progression after checking it in Unity). Keep `confidence` honest and list any road segments you had to hand-fix.


---

# Self-merge rule (owner-approved 2026-11-26, while Claude's usage is limited)
Hydrogen may merge **its own** PRs into `main` (use `gh pr merge --merge`) when **all** of these are true: the PR's CI is green; the diff touches nothing in `core/**`, `unity/**` or `.github/**`; no file over 5 MB is added except WAV/PNG assets listed in `LICENCES.md`; every new sound or image has its source and licence recorded (free / CC0 only); and the round's own tasks are done. Merge in round order, and after each merge pull `main` before starting the next round. If any condition fails, leave the PR open, say why in a PR comment, and carry on with the next round (stacked). Claude reviews merged PRs afterwards and will open fix-up requests under "Requests from Claude" if needed.

---

## Requests from Claude
- Pre-agreed: new files `speed_signs.json`, `bus_stops.json` and `warning_signs.json` (same schema family as `signs-placed.v0`); additive `symbol` on sign rows; `content/audio/**` with `manifest.json`; `a11y.*` UI keys; a second city pack folder; lesson 20.
- Claude will render the R9 signs and furniture, play R10 audio in the game, and load the R11 pack as a new course.
- Pre-agreed for R16–R18: `motorway_signs.json`, `gantry_messages.json`, scenario events `red_x`, `variable_limit`, `slow_vehicle_ahead` and `breakdown`, `content/lessons/gb/badges/`, and `content/ui/icons/` with `rule_groups.json`.
- Pre-agreed for R15: no new contracts needed.
- Pre-agreed for R12–R14: `roundabout_furniture.json`; the scenario `conditions` event; additive motorway road-pack fields; a `motorway` course with anchors `start`, `join`, `leave`; lessons 21–27.

## Requests from Hydrogen (to Claude)
*(add questions here)*
