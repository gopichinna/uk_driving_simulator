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
- [ ] **R14.1** Choose a short real motorway section near Wolverhampton (for example the M54 between junctions 1 and 3, or the M6 near junction 10). Verify the junction numbers from OSM tags and record why you chose it. Build a road pack (latest version) with:
  - slip roads;
  - additive motorway fields (pre-agreed): `hardShoulder: bool`, `emergencyAreas[]`, `gantries[]` (positions only), and `junctionNumber` on slip links.
- [ ] **R14.2** Verify on GOV.UK (Highway Code rules 253–274 and the motorway guidance): joining, lane discipline, overtaking, red X, variable limits, emergency areas, and breakdowns. Update `docs/02` and `rules.yaml` where needed.
- [ ] **R14.3** Lessons 24–27 (stage 6: joining and leaving; lane discipline; red X and variable limits; breakdowns and emergency areas). Add learn cards, diagrams, scenarios on a new `motorway` course (anchors: `start`, `join`, `leave`), and quizzes.
- [ ] **R14.4** Update `progression.json` so stages 6 and 7 come before the mock test (stage 8), keep the unlock rules consistent, and add tests.

---

## Requests from Claude
- Pre-agreed: new files `speed_signs.json`, `bus_stops.json` and `warning_signs.json` (same schema family as `signs-placed.v0`); additive `symbol` on sign rows; `content/audio/**` with `manifest.json`; `a11y.*` UI keys; a second city pack folder; lesson 20.
- Claude will render the R9 signs and furniture, play R10 audio in the game, and load the R11 pack as a new course.
- Pre-agreed for R12–R14: `roundabout_furniture.json`; the scenario `conditions` event; additive motorway road-pack fields; a `motorway` course with anchors `start`, `join`, `leave`; lessons 21–27.

## Requests from Hydrogen (to Claude)
*(add questions here)*
