# Tasks: Google Hydrogen, round 3, branch `hydrogen/curriculum-and-test-data`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 3 and §3 ownership), then this file. You own:
- `pipeline/**`;
- `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/legal/**`;
- `content/ATTRIBUTIONS.md`;
- the pipeline CI workflows;
- edits to `docs/02-rules-catalogue.md`.

Don't edit `core/**` or `unity/**`.

**Goal (round 3):** the data that turns drives into a **course with a verdict**:
- the official way a driving test is marked, verified on GOV.UK;
- the words for the debrief, settings and HUD screens;
- lessons for Stages 3–5;
- Wolverhampton junction priority and signal plans, so real-street AI knows who goes first;
- the credits and licences text the app must show.

**Why this order:** Claude is building the end-of-drive **debrief** (fault list, test-style pass/fail) and the **Settings** screen now. Sections B and C unblock that work, so do them first and push early (you can open a draft PR).

**Done when:**
- `pytest`, `ruff` and the readability linter pass;
- every new file validates against its schema;
- CI is green.

Tick each box with a one-line note (commit hash or what changed).

## Done before
- Round 1 (PR #1, `1a0ebd2`): pipeline, Wolverhampton pack, `rules.yaml`, GOV.UK verification, CI.
- Round 2 (PR #2, `6266f74`): strings, glossary, signs, Stages 0–2 lessons, Wolverhampton QA, turn restrictions, routes.

## A. Small fixes from the round-2 review
- [x] A1. `GB.POS.KEEP_LEFT.why` says "traffic stays on the left lane". Fix the grammar ("on the left"). Then read every `why` and `tip` once more for grammar a B1 reader would trip on. (Fixed `GB.POS.KEEP_LEFT.why` and regenerated `en-XA`.)
- [x] A2. Lessons judge some rules that the game can't detect yet (e.g. `GB.TL.NO_TURN_ON_RED`, `GB.SIG.TIMELY`). Add a pipeline report, `content/lessons/gb/IMPLEMENTATION.md` (generated), listing each lesson's `rulesJudged` with ✅ (has an `implementation` in `rules.yaml`) or ⏳ (planned). (`pipeline/ltl_pipeline/lesson_report.py`)
  - The report is a warning, not a CI failure.
  - Claude will use it to decide which rules to build next.

## B. Test-style marking (needed for Claude's debrief, task B3)
- [x] B1. Verify on **GOV.UK only** how the car practical test is marked: the number of driving faults allowed, and what serious and dangerous faults mean for the result. Use the "Driving test: cars" guidance or the DL25 marking pages. Record the URL and the date checked. **If you can't confirm a number, mark it `UNVERIFIED` and say why. Don't guess.** (Verified `https://www.gov.uk/driving-test/driving-test-faults-result` on 2026-11-25: max 15 driving faults, fail on >= 1 serious or dangerous fault.)
- [x] B2. `content/schemas/testmarking.v0.schema.json` (new, pre-agreed) and `content/countries/gb/test_marking.yaml`. Fields:
  - `maxDrivingFaults` (integer);
  - `failOnSerious` and `failOnDangerous` (booleans);
  - plain-English `summary` lines for pass and fail;
  - `source { url, checkedOn, verified }`.
- [x] B3. Pytest: the file validates, and if it's verified, it has a gov.uk https URL. (`pipeline/tests/test_test_marking.py`)

## C. UI strings for the debrief, settings and HUD (needed by Claude now)
- [x] C1. Extend `strings.v0` **additively** (pre-agreed) with a third `oneOf` branch: `kind: "ui"` and an `entries` object of `key → string`. Keys match `^[a-z0-9_]+(\.[a-z0-9_]+)+$`, and placeholders use ICU MessageFormat.
- [x] C2. `content/countries/gb/strings/en/ui.json` with at least these keys (Claude's code will read exactly these): (All 29 required keys added.)
  - `hud.finish_drive`, `hud.reset`, `hud.course`, `hud.view_seat`, `hud.view_chase`, `hud.settings`
  - `debrief.title`, `debrief.no_faults`, `debrief.tap_to_replay`, `debrief.replay_close`
  - `debrief.count.driving` (e.g. "{count, plural, one {# minor fault} other {# minor faults}}"), `debrief.count.serious`, `debrief.count.dangerous`, `debrief.count.good`
  - `debrief.result.pass`, `debrief.result.fail`, `debrief.result.note` (a short note that this is practice, not a real test)
  - `settings.title`, `settings.steering.label`, `settings.steering.hint`, `settings.units.hint` (the mph and km/h hint), `settings.reset_progress.label`, `settings.reset_progress.confirm`, `settings.close`
  - `course.town`, `course.two_lane_roundabout`, `course.roundabout`, `course.dual_carriageway`, `course.wolverhampton_beta`
- [x] C3. The readability linter covers `ui.json`, and its tests prove that every key in the list above exists. (`readability.py` + `test_required_ui_keys_and_pseudolocale`.)
- [x] C4. Pseudo-locale for layout testing: generate `content/countries/gb/strings/en-XA/` from the `en` files. Make every string about 40% longer, with accented letters, and keep the placeholders intact. CI regenerates it and fails on any diff. (`pseudolocale.py` + CI diff check.)

## D. Lessons for Stages 3–5 (data only)
- [x] D1. `content/lessons/gb/11-…` onwards, about 8 lessons: (Added lessons 11–18 covering Stages 3–5; lessons 1–3 stay the only free ones.)
  - **Stage 3, roundabouts:** give way to the right; lane choice and signals on a two-lane roundabout; leaving in lane; cyclists on roundabouts.
  - **Stage 4, busy town:** overtaking cyclists (1.5 m); following distance in town.
  - **Stage 5, faster roads:** lane discipline on a dual carriageway; the two-second rule at speed.

  Use the existing courses: `roundabout`, `two_lane_roundabout` and `dual_carriageway` (anchor `start`), and `town` with the round-2 anchors. Prefer rules marked ✅ in A2. Lessons 1–3 stay the only free ones.
- [x] D2. The tests from round 2 cover the new lessons (schema, rule ids, course/anchor pairs, readability). (`test_lessons.py` updated for 18 lessons.)

## E. Wolverhampton pack v0.2: who goes first
For the "Wolverhampton (beta)" drive, AI needs priority at every junction.
- [ ] E1. Additive roadpack **v0.2** (pre-agreed): an optional top-level `junctionPriority[]`. Each entry is `{ nodeId, majorLinks: [linkId, …], controlledBy: "signals" | "give_way" | "stop" | "roundabout" | "uncontrolled", confidence, confidenceNotes }`.
  - Infer it from OSM: `highway=give_way`/`stop` nodes and `direction` tags, traffic signals, `junction=roundabout`, and otherwise road class and name/ref continuity.
  - Document the heuristics in `pipeline/README.md`.
- [ ] E2. Optional `signalPlans[]`: `{ nodeId, stages: [[linkId, …], …], greenS: [..], confidence }`.
  - OSM has no phase timings, so infer the stages by pairing opposite approaches.
  - Use the same defaults as the town course: green 18 s / 14 s, amber 3 s, all-red 2 s, red and amber 2 s.
  - Set confidence ≤ 0.5 and say that it's inferred.
- [ ] E3. Bump `packFormatVersion` to `0.2`, regenerate the pack, update the sample with one example of each, and extend `QA.md` with counts by `controlledBy` and by confidence band.
  - **Don't rename or remove any field.**
  - Keep `geometry` as `[[x, z], …]` metres and `speedLimitMph` as an integer.

## F. Credits and licences screen text
- [ ] F1. `content/legal/en/credits.md`: the text for an in-app "Credits & licences" screen.
  - OSM ODbL attribution, plus how to request the derived database.
  - Highway Code under OGL v3.0, with the statement that this app isn't endorsed by DVSA or DfT.
  - DfT sign images under OGL v3.0.
  - Unity and the open-source components (list the ones that are in `pipeline/uv.lock`, plus a placeholder section that Claude fills in for Unity packages).
  - Plain English.
- [ ] F2. A test that every licence named in `content/ATTRIBUTIONS.md` also appears in `credits.md`.

## G. Hand-off
- [ ] G1. Add a dated note to `docs/08` §6, then open a PR to `main` titled "Hydrogen: test marking, UI strings, Stages 3–5, junction priority, credits". Use `gh`. A draft PR after B and C is welcome.

## Requests from Claude
- Pre-agreed for round 3:
  - new schema `testmarking.v0`;
  - an additive `strings.v0` branch, `kind: "ui"`;
  - additive roadpack **v0.2** fields `junctionPriority[]` and `signalPlans[]`.

  Anything else needs a note here and in `docs/tasks/claude-branch.md` first.
- Claude's debrief reads `test_marking.yaml` and `strings/en/ui.json` by the exact keys in C2. If a key must change, tell Claude here.

## Requests from Hydrogen (to Claude)
*(none yet)*
