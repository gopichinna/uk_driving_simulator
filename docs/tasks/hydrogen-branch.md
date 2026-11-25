# Tasks: Google Hydrogen, round 6, branch `hydrogen/lessons-complete`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 6 and §3 ownership), then this file. You own:
- `pipeline/**`;
- `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/scenarios/**`, `content/legal/**`, `content/store/**`;
- `content/ATTRIBUTIONS.md`;
- the pipeline CI workflows;
- edits to `docs/02-rules-catalogue.md`.

Don't edit `core/**` or `unity/**`.

**Goal (round 6):** make **all 18 lessons complete and correct**:
- diagrams that teach the right thing;
- scenarios for lessons 11–18;
- a short knowledge check after each lesson;
- coach voice lines;
- lane-level turn data for Wolverhampton.

**Done when:**
- `pytest`, `ruff` and the readability linter pass;
- every new file validates;
- CI is green;
- you have **looked at** every diagram (see A2).

Tick each box with a one-line note.

## Done before
- Rounds 1–5 (PRs #1–#5, last merge `396fcbb`).

## A. Fix the learn-card diagrams (top priority: they teach)
Review of round 5 found two problems:
- **Roundabout lane-choice diagram (`gb-s3-12…`):** the blue path goes **straight through the central island**. Paths must go **clockwise round** the island. The diagram should show a 2-lane approach with lane arrows: left lane for left and ahead, right lane for right.
- **Zebra diagram:** the dashed centre line runs **through** the zebra. On the approaches it should be zig-zag lines (kerbs and centre), as in the town course.

- [x] A1. Fix the generator so every diagram meets these checks: (`pipeline/ltl_pipeline/diagrams.py`: clockwise roundabout arcs exiting into left lane, zebra zig-zags with no centre line through stripes, T-junction right turn into left lane.)
  1. Vehicles are on the **left** of each road.
  2. Every path follows lanes, with curves through junctions and **clockwise round roundabouts**, and never crosses an island, kerb or footway.
  3. Markings match the town course (cycle box at the lights, give-way lines and triangle, zebra with zig-zags, yellow box hatch).
  4. Turns finish in the **left** side of the new road.
- [x] A2. Generate a **contact sheet** (`content/lessons/gb/diagrams/_contact_sheet.png`, all 18 diagrams in a grid) and **look at it** yourself. In your PR description, list each diagram with one line: "checked: path clockwise / left lane / markings OK". Add tests for what code can check: path points never fall inside island polygons; for left-hand traffic, path points sit to the left of each road's centreline. (`_contact_sheet.png` visually inspected + `pipeline/tests/test_diagrams.py`.)

## B. Scenarios for lessons 11–18
- [ ] B1. Extend `scenario.v0` **additively** (pre-agreed) with two event types for courses without town anchors:
  - `take_exit` `{ exitNumber: 1–4 }`: the roundabout exit the player must take. The scenario ends when they leave by it.
  - `finish_after_s` `{ seconds }`: the scenario ends after this many seconds of driving.

  Use them together with the existing `traffic_density`, `cyclist_ahead` (anchor `start`) and `finish_at`.
- [ ] B2. Write a scenario file for every scenario in lessons 11–18, and extend the C3 tests from round 5 to cover them.
  - **Roundabout lessons:** vary `take_exit` across the scenarios (1st, 2nd, 3rd) so lane choice and signals are practised.
  - **Dual carriageway:** use `finish_after_s`.

## C. Knowledge check after each lesson (original questions only)
- [ ] C1. `content/schemas/quiz.v0.schema.json` (new, pre-agreed) and `content/lessons/gb/quiz/<lessonId>.json`. Each lesson gets 2–3 multiple-choice questions. Each question has:
  - `id`, `question`;
  - `options` (3–4);
  - `answerIndex`;
  - `explanation` (1–2 sentences);
  - `ruleId` (it must exist in `rules.yaml`);
  - `diagram` (optional).
- [ ] C2. **Write every question yourself from the Highway Code rule text** (our paraphrase). **Never copy or adapt DVSA theory-test questions or any question bank** (`AGENTS.md` §4, rule 2). Plain English B1, and the readability linter covers it.
- [ ] C3. Tests:
  - every lesson has a quiz;
  - `answerIndex` is in range;
  - the options are unique;
  - `ruleId` exists.

## D. Coach voice lines (for text-to-speech later)
- [ ] D1. Extend `strings.v0` additively (pre-agreed) with `kind: "voice"`: `entries` of `key → line` (≤ 12 words, spoken English, B1). Create `content/countries/gb/strings/en/voice.json` with:
  - `voice.<ruleId>.fault` and `voice.<ruleId>.good` for every implemented rule (see `rule_index.json`);
  - `voice.lesson.start`, `voice.lesson.passed`, `voice.lesson.try_again`.
- [ ] D2. The readability linter checks voice lines, including the 12-word limit. The en-XA pseudo-locale includes them.

## E. Wolverhampton lane connections (roadpack v0.5, additive, pre-agreed)
- [ ] E1. For links with `turn:lanes` or more than one lane, add top-level `laneConnections[]`: `{ nodeId, fromLink, fromLane, toLink, toLane, confidence }`.
  - Lane 0 is the **nearside** (left in the UK).
  - Derive them from `turn:lanes` arrows when tagged; otherwise use a default: nearside lane for left and ahead, offside lane for right and ahead.
  - Respect `restrictions[]`.
- [ ] E2. Bump `packFormatVersion` to `0.5`, update the sample with one example, and add a QA section with counts and confidence. **Don't rename or remove fields.**

## F. Hand-off
- [ ] F1. Add a dated note to `docs/08` §6, then open a PR on `gopichinna/uk_driving_simulator` titled "Hydrogen: correct diagrams, lessons 11–18 scenarios, quizzes, voice lines, lane connections". Include the A2 diagram checklist in the PR description. Do A first and open a draft PR early.

## Requests from Claude
- Pre-agreed for round 6:
  - `scenario.v0` event types `take_exit` and `finish_after_s`;
  - new schema `quiz.v0`;
  - an additive `strings.v0` branch, `kind: "voice"`;
  - additive roadpack **v0.5** `laneConnections[]`.

  Anything else needs a note here and in `docs/tasks/claude-branch.md` first.
- Claude is building the scenario player against `scenario.v0` now. Please don't change existing event fields.

## Requests from Hydrogen (to Claude)
*(none yet)*
