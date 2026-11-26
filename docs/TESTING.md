# Manual Testing Checklist — L to Local ("Keep Left") on OnePlus 12

This checklist is written for a non-developer testing the Android build on a **OnePlus 12** (or similar Android phone).

## Before You Start (Audio, Controls & UI)
- **Controls:** Steering sensitivity defaults to **60%**. Check you can steer smoothly, accelerate, brake, and toggle left/right indicators from both **seat view** (right-hand drive) and **chase view**.
- **Mirrors:** Verify the live rear-view mirror and both door mirrors update smoothly, and check the optional look-over-the-shoulder view.
- **Sound check:**
  - Engine loop (`engine_idle.wav` -> `engine_low.wav` -> `engine_mid.wav` -> `engine_high.wav`): should sound like a steady small petrol hatchback with no clicks at the loop join.
  - Indicator relay (`indicator_on.wav` / `indicator_off.wav`): clear click on/off.
  - Coach voice (`en_GB-cori-medium`): clear, plain English; listen for variety (`.v2` / `.v3`) when repeating faults or good drives.
  - Weather & street SFX (`rain_loop.wav`, `wipers.wav`, `ambience_town.wav`, `chime_success.wav`, `chime_fault.wav`).

## Star Targets (All Lessons)
- **3 stars:** 0 driving faults, 0 serious/dangerous faults.
- **2 stars:** Passed within `maxMinor` driving faults, 0 serious/dangerous faults.
- **1 star:** Passed with `maxMinor` reached, 0 serious/dangerous faults.
- **Fail:** Any serious/dangerous fault, or more driving faults than `maxMinor`.

---

## Stage 0 — Basics (Free Tier: Lessons 1–3)
- **Drive:** `Keeping left and the controls`, `Pulling away and MSM`, `Speed limits` (`town`, `dual_carriageway`).
- **Coach should say:** Clear tips on keeping left, early signalling (`GB.SIG.TIMELY`), and staying inside 20/30/40 mph limits (`GB.SPD.OVER_LIMIT`).
- **Faults:** Exceeding the signed limit or signalling too late counts as a fault; driving steadily at 28–30 mph in a 30 zone should **not** fault.

## Stage 1 — Junctions (Lessons 4–6)
- **Drive:** `Give way at T-junctions`, `Pedestrians at junctions (H2)`, `Road position after turning` (`town`).
- **Coach should say:** Give-way checks (`GB.JCT.GIVE_WAY`), patience for pedestrians, and clean left-side position.
- **Faults:** Pulling out in front of main-road traffic (`GB.JCT.GIVE_WAY`) counts as a fault; stopping cleanly behind the broken white lines to let traffic pass should **not** fault.

## Stage 2 — Signals & Crossings (Lessons 7–10)
- **Drive:** `Traffic lights and amber`, `Advanced stop lines`, `Zebra crossings and zig-zags`, `Box junctions` (`town`).
- **Coach should say:** Holding back before yellow boxes (`GB.JCT.BOX`) and never overtaking on crossing zig-zags (`GB.XNG.ZIGZAG_OVERTAKE`).
- **Faults:** Entering a box junction when your exit is blocked, or passing a stopped car on zig-zags; waiting behind the box on green when traffic queues ahead should **not** fault.

## Stage 3 — Roundabouts (Lessons 11–14)
- **Drive:** `Roundabout priority`, `Roundabout lanes and signals`, `Staying in lane on roundabouts`, `Cyclists on roundabouts` (`roundabout`, `two_lane_roundabout`, `wolverhampton`).
- **Coach should say:** Give way to the right (`GB.RBT.GIVE_WAY_RIGHT`), pick approach lane early (`GB.RBT.APPROACH_LANE`), signal left to exit (`GB.RBT.SIGNAL_EXIT`), and keep inside your lane (`GB.RBT.STAY_IN_LANE`).
- **Faults:** Cutting off traffic from the right, drifting across lanes, missing exit signals, or going anti-clockwise (`GB.RBT.WRONG_WAY`).

## Stage 4 — Town Traffic & Vulnerable Road Users (Lessons 15–16)
- **Drive:** `Overtaking cyclists`, `Following distance in town` (`town`, `dual_carriageway`).
- **Coach should say:** Leave at least 1.5 m past cyclists (`GB.VRU.PASS_CYCLIST`) and keep a 2-second gap (`GB.GAP.TWO_SECOND`).
- **Faults:** Passing closer than 1.5 m or tailgating under 2 seconds; waiting patiently behind a cyclist when oncoming traffic blocks a wide pass should **not** fault.

## Stage 5 — Dual Carriageways (Lessons 17–18)
- **Drive:** `Dual carriageway lane discipline`, `The two-second rule at speed` (`dual_carriageway`).
- **Coach should say:** Check mirrors & blind spot before changing lanes (`GB.OBS.LANE_CHANGE`) and hold 2 seconds at 40+ mph.
- **Faults:** Cutting in or forcing cars behind to brake (`GB.OBS.LANE_CHANGE`).

## Stage 6 — Motorways (Lessons 24–27)
- **Drive:** `Joining and leaving`, `Lane discipline`, `Red X and variable limits`, `Breakdowns and emergency areas` (`motorway` — M54 J2 / M6 corridor).
- **Check:** Blue advance direction signs (1 mile, ½ mile), 3/2/1 countdown markers, overhead gantry red X / variable speed limits, and SOS emergency areas.

## Stage 7 — Conditions (Lessons 21–23)
- **Drive:** `Night driving and headlights`, `Rain and stopping distances`, `Fog and low visibility` (`town`, `dual_carriageway`, `wolverhampton`).
- **Check:** Rain loop and wiper audio in rain; visibility drop in fog; coach feedback on lights and doubled 4-second wet gap.

## Stage 8 — Review Drives, Wolverhampton Test Area & Mock Test (Lessons 19–20)
- **Review Drive:** Trigger a review drive from the menu and confirm it targets your weakest rules.
- **Roads near the test centre (`wolverhampton-test-area`):** Check direction signs, bus stops, speed signs, and independent-driving prompts (`satnav` / `signs`).
- **Mock Driving Test (`gb-s8-19-mock-driving-test`):** 10-minute independent drive; verify end-of-drive debrief, fault replay, and pass/fail verdict.

## Known Rough Edges
- `road_noise_motorway.wav` and `brake_squeal_soft.wav` are not yet included (no suitable free CC0 source found in R28).
- Planned rules in `rules.yaml` without `Rules.Core` implementations yet (see `content/lessons/gb/IMPLEMENTATION.md`) are not judged live during drives.
- `suburb_practice`, `rural_practice`, and `ring_road_practice` OSM packs are generated in `content/cities/` and awaiting Unity scene wiring (`progression.json`).
