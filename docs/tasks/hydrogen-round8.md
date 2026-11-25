# Tasks: Google Hydrogen, round 8 (queued: start after round 7 merges), branch `hydrogen/signs-and-mock-test`

**Read first:** `AGENTS.md`, `docs/08-parallel-work-plan.md`, then this file. Ownership is the same as round 7. Don't edit `core/**` or `unity/**`.

**Goal (round 8, owner request 2026-11-25):** Wolverhampton should have **real-world road signs**, such as "Railway Station", "Bus Station", "City Centre", schools and the hospital, at the right junctions, the way the real city has them. The final lesson is a **mock driving test**.

Facts checked on GOV.UK ("What happens during the test", https://www.gov.uk/driving-test/what-happens-during-test):
- you drive for **around 35 minutes**;
- that includes **20 to 35 minutes of independent driving**, following a sat nav, traffic signs, or both;
- there is also a reversing exercise; the game can't do this yet (there's no reverse gear).

Claude has built the game side (in `claude/town-centre-junctions`):
- the town course has UK local direction signs, school warning signs, SCHOOL KEEP CLEAR markings, a bus stop and labelled landmarks;
- the lesson flow is in place;
- a 35-minute sat-nav mock test runs in Wolverhampton.

What's missing is the **data** for Wolverhampton's signs and destinations.

## A. Destinations from OSM (ODbL)
- [x] A1. `content/schemas/pois.v0.schema.json` (new, pre-agreed) and `content/cities/wolverhampton/pois.json` (149 POIs including 6 in the ~500 m buffer and Queen Square `city_centre`). Each POI has:
  - `id`, `name`;
  - `kind`: `railway_station` | `bus_station` | `school` | `hospital` | `city_centre` | `car_park` | `shopping` | `university` | `police` | `other`;
  - `x`, `z` (pack origin);
  - `accessLinkId` (the nearest main-network link to drive to);
  - `osmId`.

  Source the tags: `railway=station`, `amenity=bus_station`, `amenity=school`, `amenity=hospital`, `amenity=parking`, `shop=mall`, `amenity=university`, `amenity=police`. Add one `city_centre` point (Queen Square) and record the reason. Include POIs up to about 500 m outside the bbox if they are signed from inside it, with `outsideArea: true`.
- [x] A2. Tests: every `accessLinkId` is a main-network link; names are present; the ODbL source block is included (`pipeline/tests/test_pois.py`, 48/48 tests pass).

## B. Direction signs at junctions
- [x] B1. `content/schemas/signs-placed.v0.schema.json` (new, pre-agreed) and `content/cities/wolverhampton/direction_signs.json` (8 major-junction approach signs). Place a sign on the approach to each major junction (signals or roundabout, plus primary/trunk give-way junctions), 30–50 m before the junction, on the **nearside verge**. Each sign has:
  - `id`, `nodeId`, `approachLinkId`, `x`, `z`, `heading` (the direction of the traffic that reads it);
  - `style`: `primary` (green, white text, yellow road numbers) on primary/trunk roads, `local` (white, black text) elsewhere. Source: the Traffic Signs Manual / Know Your Traffic Signs. Cite the GOV.UK URL and **don't guess styles**;
  - `rows[]` = `{ text, poiId?, ref?, direction: "ahead" | "left" | "right" | "ahead_left" | "ahead_right" }`.
- [x] B2. Choose each row's destination by **shortest route** through the network (using `laneConnections` and `restrictions`) from the approach to each POI. Pick the 2–4 most useful destinations per sign (nearest of each kind, plus City Centre and Railway Station where relevant). The direction is the turn at that junction on that route.
- [x] B3. Street-name plates: `street_names.json` (372 plates at side-road junction corners), with one plate per side road at junction corners (`x`, `z`, `heading`, `name`), from link names.
- [x] B4. Tests (`pipeline/tests/test_direction_signs.py`, 52/52 tests pass, readability 0 errors):
  - every sign row's direction matches the first turn of the shortest route to its POI;
  - no sign sits on the carriageway (≥ 0.5 m outside the road edge, using `widthM`);
  - text passes the readability linter.

## C. The mock-test lesson as data
- [x] C1. Add **lesson 19** `content/lessons/gb/19-mock-driving-test.json`: stage 8, title "Mock driving test", `freeTier: false`. The learn card explains in plain English what the real test includes (from the GOV.UK page above: eyesight check, "show me, tell me" questions, about 35 minutes of driving with 20–35 of independent driving, and a reversing exercise). Say which parts the game covers: the driving and the independent driving. Extend `lesson.v0` additively so `course` can be `wolverhampton` (pre-agreed).
- [x] C2. `content/scenarios/gb/s8_19_mock_test.json` (plus `s8_19_mock_test_signs.json` for `signs` mode) with an additive event `independent_driving { minutes: 35, mode: "satnav" | "signs" | "mixed" }` (pre-agreed vocabulary addition). In `signs` mode, the player is told "Follow the signs to <POI name>", using the POIs from A.
- [x] C3. A quiz for lesson 19 (`content/lessons/gb/quiz/gb-s8-19-mock-driving-test.json`, original questions only: what happens on the test, what counts as a serious fault, independent driving; 52/52 tests pass).

## D. Hand-off
- [ ] D1. Add a dated note to `docs/08` §6, then open a PR on `gopichinna/uk_driving_simulator` titled "Hydrogen: Wolverhampton destinations, direction signs, mock-test lesson".

## Requests from Claude
- Pre-agreed for round 8:
  - new schemas `pois.v0` and `signs-placed.v0`;
  - `street_names.json` (same schema family);
  - additive `lesson.v0` course `wolverhampton`;
  - additive scenario event `independent_driving`.
- Claude will render signs from `direction_signs.json` (green or white panels, arrows by `direction`) and add "follow the signs" guidance from `independent_driving`.
