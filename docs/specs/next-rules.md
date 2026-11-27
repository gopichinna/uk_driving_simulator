# Next 20 Rules Specification Pack (Batch 1 — Stages 0–6)

**Checked against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Severity mapping:** Matches `content/countries/gb/test_marking.yaml`:
- Base severity is one of: `Driving` (minor fault, max 15), `Serious` (fail), or `Dangerous` (fail).
- Escalation conditions (when conflict, near-miss, or active hazard occurs) are listed separately under **Escalates to**.
- Numeric judgement-call thresholds not fixed by statute are marked **`PROPOSED`**.

## Ranking Rationale
All 20 rules below are **genuinely unimplemented** in `core/Rules.Core/*.cs` (verified against the 23 implemented `GB.*` constants on 2026-11-27; the 9 rules previously listed in R32 that are already implemented in `core/Rules.Core` — `GB.POS.KEEP_LEFT`, `GB.SCH.SLOW`, `GB.JCT.STOP_SIGN`, `GB.JCT.H2_PEDESTRIANS`, `GB.TL.RED`, `GB.TL.AMBER`, `GB.TL.RED_AMBER`, `GB.TL.ASL`, `GB.XNG.ZEBRA` — have been replaced with the next highest-value planned rules). Every rule in this batch is either taught in Stages 0–4/6 or pairs directly with existing town/dual-carriageway/motorway geometry in `RoadPack` v0.7 and `Sim.Core`.

---

## 1. `GB.POS.DUAL_KEEP_LEFT` — Keep left on dual carriageways
- **Highway Code:** Rule 137 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Use the left-hand lane on a dual carriageway unless overtaking or turning right.
- **Game detection & thresholds:** On `links[].lanes >= 2` and `links[].twoWay == false` (dual carriageway): flag if `PlayerState.laneIndex > 0` for `> 10.0 s` (`PROPOSED`) when no slower vehicle is ahead in lane 0 within `40.0 m` (`PROPOSED`) and no right-turn route junction is within `100.0 m` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if repeated 3+ times (`PROPOSED`) or forces faster traffic to undertake.
- **Pass cases:** (1) Stay in lane 0 at 40 mph with lane 0 clear. (2) Move to lane 1 to overtake a slower vehicle at 25 mph, then return to lane 0 within 10.0 s after passing.
- **Fail cases:** (1) Cruise in lane 1 for 15.0 s at 40 mph while lane 0 is empty for > 60 m. (2) Remain in lane 1 for 12.0 s after completing an overtake with a 50 m clear gap in lane 0.

## 2. `GB.POS.AFTER_TURN` — Join the left lane after turning
- **Highway Code:** Rule 160 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Keep to the left after turning into a two-way or multi-lane road.
- **Game detection & thresholds:** Within `30.0 m` (`PROPOSED`) downstream of exiting a junction `nodes[]` onto `links[]`: flag if `PlayerState.lateralOffsetM` places the vehicle across the centreline (`twoWay == true`) or in `laneIndex > 0` (`lanes >= 2` without immediate right turn within `40.0 m`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if an oncoming `TrafficAgent` is within `50.0 m` (`PROPOSED`) on the destination link.
- **Pass cases:** (1) Turn left at a T-junction and settle within `0.5 m` of lane 0 centreline within `15.0 m`. (2) Turn right into a two-way side road and enter the left lane cleanly.
- **Fail cases:** (1) Swing wide on a left turn and cross the centreline by `1.0 m` for `> 10.0 m`. (2) Turn right into a side road and drive on the right-hand side for `20.0 m`.

## 3. `GB.OBS.MOVE_OFF_CHECKS` — Pull away safely from the kerb
- **Highway Code:** Rule 159 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Move off from the kerb only when passing traffic will not have to slow down or swerve.
- **Game detection & thresholds:** When `PlayerState.speedMps` transitions from `< 0.5 m/s` (1.1 mph) to `>= 1.5 m/s` (3.4 mph) leaving a kerbside stop, monitor approaching/following `TrafficAgent` on the same or adjacent lane for `3.0 s` (`PROPOSED`): flag if `other_decel_req > 1.5 m/s²` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `other_decel_req > 3.0 m/s²` (`PROPOSED`) or `Dangerous` if `ttc < 1.5 s` (`PROPOSED`) with a passing cyclist or car.
- **Pass cases:** (1) Wait at the kerb while a car passes, then pull away with `other_decel_req == 0.0 m/s²`. (2) Pull away on an empty street with a right signal `>= 2.0 s` before moving.
- **Fail cases:** (1) Pull out `15.0 m` ahead of a 30 mph car, causing `other_decel_req = 3.6 m/s²`. (2) Move off into the path of a cyclist `8.0 m` behind at 15 mph.

## 4. `GB.OBS.MSM` — Mirror–Signal–Manoeuvre before slowing or turning
- **Highway Code:** Rule 161 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Give clear signals and brake smoothly in good time before turning or stopping.
- **Game detection & thresholds:** Outcome-based (per `AGENTS.md` §4.7): flag if player turns onto a different `links[]` arm at `nodes[]` without `indicator` active `>= 1.5 s` (`PROPOSED`) prior, or brakes sharply (`decel > 3.5 m/s²`, `PROPOSED`, with no red light or hazard within `25.0 m`) forcing a following `TrafficAgent` to brake (`other_decel_req > 2.0 m/s²`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `other_decel_req > 3.5 m/s²` (`PROPOSED`) or causes a turn conflict.
- **Pass cases:** (1) Signal left `3.0 s` before turning and decelerate smoothly at `<= 2.0 m/s²`. (2) Slow gradually for a side road with `other_decel_req <= 1.0 m/s²` behind.
- **Fail cases:** (1) Brake at `4.5 m/s²` and turn left with `0.0 s` prior indicator. (2) Swerve into a side road with `0.5 s` signal, forcing a following car to brake at `3.8 m/s²`.

## 5. `GB.SPD.APPROPRIATE` — Safe speed for road and hazards
- **Highway Code:** Rule 125 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Drive below the speed limit when parked cars, narrow streets, bends, or pedestrians reduce clearance or view.
- **Game detection & thresholds:** Within `25.0 m` (`PROPOSED`) of parked `TrafficAgent` (`speedMps < 0.2`), narrow link (`laneWidthM < 3.0 m`), or pedestrian within `2.0 m` of kerb: flag if `PlayerState.speedMps > 0.85 * links[].speedLimitMph * 0.44704` (`> 25.5 mph` in a 30 mph zone, `PROPOSED`) when lateral clearance `< 1.5 m` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `PlayerState.speedMps` exceeds `links[].speedLimitMph` or lateral clearance `< 0.8 m` (`PROPOSED`) past a pedestrian/cyclist.
- **Pass cases:** (1) Ease from 30 mph to 20 mph when passing parked cars with `1.2 m` clearance. (2) Slow to 22 mph approaching a blind town bend.
- **Fail cases:** (1) Hold 30 mph squeezing past parked cars with `1.0 m` clearance and pedestrians on the kerb. (2) Hold 30 mph past a narrow pinch point with oncoming traffic.

## 6. `GB.SPD.BUILT_UP` — 30 mph built-up speed limit
- **Highway Code:** Rule 124 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** A 30 mph (20 mph in Wales / signed 20 mph zones) limit applies where street lighting is present unless signs show otherwise.
- **Game detection & thresholds:** On links with `links[].speedLimitMph == 30` (or `20` where signed) and `links[].roadClass` urban: flag if `PlayerState.speedMph > links[].speedLimitMph + 2.0` (`PROPOSED` tolerance for dial/control jitter) for `> 1.5 s` (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `speedMph >= links[].speedLimitMph + 10.0` (`PROPOSED`, e.g., >= 40 mph in a 30 mph street).
- **Pass cases:** (1) Hold 28–30 mph along a lit urban street with no repeater signs. (2) Drop to 20 mph on entering a signed 20 mph residential zone.
- **Fail cases:** (1) Drive at 35 mph along a street-lit 30 mph town road for 3.0 s. (2) Drive at 30 mph through a signed 20 mph zone.

## 7. `GB.SPD.NATIONAL_LIMIT` — National speed limit by road type
- **Highway Code:** Rule 124 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** On national-speed-limit roads, cars are limited to 60 mph on single carriageways and 70 mph on dual carriageways and motorways.
- **Game detection & thresholds:** On links where `links[].speedLimitMph` is `60` (`twoWay == true`, single carriageway) or `70` (`twoWay == false`, dual carriageway / motorway): flag if `PlayerState.speedMph > links[].speedLimitMph + 2.0` (`PROPOSED`) for `> 1.5 s`.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `speedMph >= links[].speedLimitMph + 15.0` (`PROPOSED`).
- **Pass cases:** (1) Cruise at 58 mph on a 60 mph single-carriageway rural road. (2) Cruise at 68 mph on a 70 mph dual carriageway.
- **Fail cases:** (1) Drive at 67 mph on a 60 mph single-carriageway rural road for 3.0 s. (2) Drive at 78 mph on a 70 mph dual carriageway.

## 8. `GB.GAP.CUT_IN` — Do not cut in after overtaking
- **Highway Code:** Rule 168 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** After overtaking, move back only when well clear so the overtaken vehicle does not have to brake.
- **Game detection & thresholds:** When player changes lane back in front of a `TrafficAgent` (`laneIndex` transitions onto agent's lane ahead of it): flag if longitudinal gap at merge is `< 1.5 s` (`PROPOSED` time headway, i.e., `gapM < PlayerState.speedMps * 1.5`) or `other_decel_req > 2.0 m/s²` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `gapM < 0.8 s` (`PROPOSED`) or `other_decel_req > 3.5 m/s²` (`PROPOSED`).
- **Pass cases:** (1) Wait until `2.2 s` (`~39 m` at 40 mph) ahead of an overtaken car before returning left. (2) Return left smoothly with `other_decel_req == 0.0 m/s²`.
- **Fail cases:** (1) Pull back in `6.0 m` (`0.33 s`) ahead of an overtaken car at 40 mph. (2) Cut across a cyclist's front wheel within `3.0 m` after passing.

## 9. `GB.JCT.H3_CYCLISTS` — Do not cut across cyclists at junctions (H3)
- **Highway Code:** Rule H3 (`https://www.gov.uk/guidance/the-highway-code/introduction`, checked 2026-11-27)
- **Rule:** Do not turn across the path of a cyclist going straight ahead at a junction.
- **Game detection & thresholds:** When a `TrafficAgent` (`kind == "cyclist"`) is travelling straight on the nearside/cycle lane within `25.0 m` (`PROPOSED`) of a junction `nodes[]` and player turns across its path: flag if `cyclist_decel_req > 2.0 m/s²` (`PROPOSED`) or lateral/longitudinal clearance `< 1.5 m` (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `cyclist_decel_req > 4.0 m/s²` (`PROPOSED`) or collision occurs.
- **Pass cases:** (1) Hold back behind a cyclist for `2.0 s` until they pass the side road, then turn left. (2) Wait for an oncoming cyclist to clear the junction before turning right.
- **Fail cases:** (1) Overtake a cyclist and turn left immediately `4.0 m` ahead of them ("left hook"). (2) Turn right across an oncoming cyclist `10.0 m` away, forcing `cyclist_decel_req = 3.5 m/s²`.

## 10. `GB.JCT.UNMARKED` — Approach unmarked junctions with equal care
- **Highway Code:** Rule 170 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Nobody has priority at an unmarked junction; slow down, look both ways, and be ready to stop.
- **Game detection & thresholds:** At `junctionPriority[]` (`controlledBy == "uncontrolled"`) when another `TrafficAgent` is approaching on a crossing arm within `30.0 m` (`PROPOSED`): flag if player enters the junction at `> 15.0 mph` (`6.7 m/s`, `PROPOSED`) or forces `other_decel_req > 2.0 m/s²` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `other_decel_req > 3.5 m/s²` (`PROPOSED`) or `ttc < 1.5 s` (`PROPOSED`).
- **Pass cases:** (1) Slow to `12.0 mph` approaching an unmarked residential crossroads and check both ways. (2) Yield smoothly when another car reaches the unmarked junction first.
- **Fail cases:** (1) Cross an unmarked side-street junction at `25.0 mph` without slowing. (2) Pull across an approaching car at an unmarked junction (`other_decel_req = 3.8 m/s²`).

## 11. `GB.JCT.TURN_FAR_POSITION` — Right turn approach position
- **Highway Code:** Rules 179–180 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Position as close as is safe to the centre line (without crossing it) before turning right.
- **Game detection & thresholds:** Within `25.0 m` (`PROPOSED`) before a right turn at `nodes[]` on a two-way link (`twoWay == true`): flag if `PlayerState.lateralOffsetM` leaves `> 1.2 m` (`PROPOSED`) gap to the centreline (hugging the left kerb) or crosses the centreline (`> 0.3 m` into oncoming side, `PROPOSED`) before turning.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if crossing the centreline forces an oncoming `TrafficAgent` to brake or swerve.
- **Pass cases:** (1) Position `0.4 m` left of the centreline `20.0 m` before the right turn. (2) Enter a marked right-turn pocket lane cleanly.
- **Fail cases:** (1) Hug the far-left kerb (`2.0 m` from centreline) while signalling right. (2) Cross `0.8 m` over the centreline `15.0 m` before the junction.

## 12. `GB.JCT.TURN_FAR_GAP` — Right turn gap across oncoming traffic
- **Highway Code:** Rule 180 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Wait until oncoming traffic is clear before turning right across its path.
- **Game detection & thresholds:** When player crosses the centreline turning right at `nodes[]`: flag if any oncoming `TrafficAgent` has `ttc < 3.5 s` (`PROPOSED`) or experiences `other_decel_req > 2.0 m/s²` (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `ttc < 1.5 s` (`PROPOSED`) or `other_decel_req > 4.0 m/s²` (`PROPOSED`).
- **Pass cases:** (1) Wait at the centreline until two oncoming cars pass, then turn with `ttc > 5.0 s`. (2) Turn promptly when the oncoming road is clear for `6.0 s`.
- **Fail cases:** (1) Turn right across an oncoming car with `ttc = 2.2 s`, forcing `other_decel_req = 3.8 m/s²`. (2) Turn across an approaching cyclist with `ttc = 2.5 s`.

## 13. `GB.JCT.EMERGE_LOOK` — Creep and look when sightline is blocked
- **Highway Code:** Rule 170 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Approach junctions slowly and edge forward carefully if parked cars or buildings block your view.
- **Game detection & thresholds:** At `junctionPriority[]` (`give_way` / `stop`) when sightline along the major road is occluded within `30.0 m` (`PROPOSED`) by geometry/parked cars: flag if player crosses the give-way line at `> 8.0 mph` (`3.6 m/s`, `PROPOSED`) without slowing below `5.0 mph` (`2.2 m/s`, `PROPOSED`) in the `6.0 m` approach zone.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if emerging fast forces a major-road `TrafficAgent` to brake (`other_decel_req > 2.5 m/s²`).
- **Pass cases:** (1) Slow to `3.0 mph` at the line, edge out `1.5 m` to check, then join cleanly. (2) Pause behind the line for `1.5 s` before emerging past parked cars.
- **Fail cases:** (1) Emerge past a parked van blocking the left view at `14.0 mph` without slowing. (2) Shoot out of a blind side road at `12.0 mph` into a main road.

## 14. `GB.JCT.OPPOSITE_RIGHT` — Passing an opposite right-turner safely
- **Highway Code:** Rule 181 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** When both you and an oncoming driver are turning right, normally pass nearside to nearside (in front of each other) unless road markings show otherwise.
- **Game detection & thresholds:** At a 4-arm `nodes[]` junction when an oncoming `TrafficAgent` has right indicator active and is positioned in the junction turning right: flag if player cuts behind/offside (`ttc < 2.5 s`, `PROPOSED`, or lateral path conflict `< 1.5 m`, `PROPOSED`) instead of turning nearside-to-nearside.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if crossing paths creates `ttc < 1.5 s` (`PROPOSED`) or forces oncoming vehicle to stop inside the junction.
- **Pass cases:** (1) Pass nearside-to-nearside in front of an oncoming right-turning car. (2) Wait if the layout requires offside-to-offside and oncoming traffic is still moving.
- **Fail cases:** (1) Drive around behind an oncoming right-turner, blocking their exit and forcing a stop. (2) Cut across the front of an oncoming right-turner already turning offside-to-offside.

## 15. `GB.TL.NO_TURN_ON_RED` — No turning left on red
- **Highway Code:** Rule 175 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** You MUST wait behind the stop line on red even when turning left, unless a green filter arrow is lit.
- **Game detection & thresholds:** At `signalPlans[]` (`phase == "RED"`) with no active green left filter arrow: flag if player crosses `stopLine` while turning left (`indicator == "left"` or exit arm is left turn).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if a pedestrian or cross-traffic `TrafficAgent` is active on the junction (`ttc < 2.0 s`, `PROPOSED`).
- **Pass cases:** (1) Stop behind the white line on red with left indicator on and wait for green. (2) Wait for green, then check for H2 pedestrians before turning left.
- **Fail cases:** (1) Treat a red light like a US right-on-red and turn left while red is lit. (2) Roll across the stop line on red-and-amber to start a left turn early.

## 16. `GB.TL.FILTER_ARROW` — Obey green filter arrow lanes
- **Highway Code:** Rule 177 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Only enter a filter-arrow lane if you are going in the arrow's direction; go on the arrow only without blocking other legal traffic.
- **Game detection & thresholds:** On `links[]` with `laneConnections[]` marked as a filter lane: flag if player uses the filter lane to go straight/wrong direction, or proceeds on a filter arrow while obstructing legal cross-traffic (`other_decel_req > 2.0 m/s²`, `PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if exiting a filter lane across moving straight-ahead traffic (`ttc < 1.5 s`, `PROPOSED`).
- **Pass cases:** (1) Wait in the left filter lane and turn left cleanly when the green left arrow lights up. (2) Stay out of the filter lane when going straight ahead.
- **Fail cases:** (1) Sit in a green-arrow-only left lane while going straight ahead, blocking turning cars. (2) Turn straight ahead when only the left green filter arrow is lit.

## 17. `GB.TL.CLEAR_JUNCTION` — Do not block a green-light junction
- **Highway Code:** Rule 176 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Only go forward on green if there is room to clear the junction safely (or you are waiting to turn right).
- **Game detection & thresholds:** At `signalPlans[]` (`phase == "GREEN"`), when not turning right and the exit `links[]` has a stopped `TrafficAgent` within `12.0 m` (`PROPOSED`) of the junction exit: flag if player enters the junction polygon and remains stopped (`speedMps < 0.5 m/s`) inside it for `> 2.0 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if player is still inside the junction when the cross-traffic phase turns `GREEN`.
- **Pass cases:** (1) Wait behind the stop line on green while traffic queues beyond the junction, then cross once `15.0 m` opens. (2) Wait inside the junction only when turning right and exit arm is clear.
- **Fail cases:** (1) Enter on green behind a stopped queue and sit across the junction for `4.0 s`. (2) Block cross traffic after lights change to red.

## 18. `GB.XNG.KEEP_CLEAR` — Do not stop on a pedestrian crossing in a queue
- **Highway Code:** Rule 192 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** In traffic queues, leave pedestrian crossings clear even when lights are green.
- **Game detection & thresholds:** At any `crossings[]` polygon (`zebra` / `pelican` / `puffin`): flag if player stops (`speedMps < 0.5 m/s`) with any part of the vehicle footprint overlapping the crossing stripes for `> 2.0 s` (`PROPOSED`) due to queued traffic ahead.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if a pedestrian steps onto the crossing and is forced to walk around the player's car.
- **Pass cases:** (1) Hold `2.0 m` behind the crossing when traffic is queued ahead, then cross once a car-length (`6.0 m`) clears beyond it. (2) Keep zebra stripes completely clear in slow town traffic.
- **Fail cases:** (1) Roll forward onto zebra stripes behind a stopped bus and sit there for `5.0 s`. (2) Block a signalised crossing during a green phase queue.

## 19. `GB.XNG.PELICAN_AMBER` — Give way on flashing amber at pelican crossings
- **Highway Code:** Rule 196 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** At a pelican crossing, flashing amber means you MUST give way to any pedestrians still on the crossing, and may proceed only when it is clear.
- **Game detection & thresholds:** At `crossings[]` (`kind == "pelican"`) during `FLASHING_AMBER` phase: flag if player moves across the crossing (`speedMps > 0.5 m/s`) while any pedestrian agent is inside the crossing polygon or within `1.5 m` (`PROPOSED`) of the player's lane.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if pedestrian clearance `< 1.0 m` (`PROPOSED`) at `> 5.0 mph`.
- **Pass cases:** (1) Wait behind the stop line during flashing amber until the last pedestrian reaches the kerb, then move off. (2) Proceed smoothly on flashing amber when the crossing is completely empty.
- **Fail cases:** (1) Drive across on flashing amber while a slow pedestrian is still crossing the far half. (2) Rev and edge onto the crossing behind a pedestrian.

## 20. `GB.MWY.KEEP_LEFT` — Keep left on motorways
- **Highway Code:** Rule 264 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Keep in the left-hand lane on a motorway unless overtaking.
- **Game detection & thresholds:** On `links[].roadClass == "motorway"` (`lanes >= 3`): flag if `PlayerState.laneIndex > 0` for `> 10.0 s` (`PROPOSED`) when no slower `TrafficAgent` is ahead in `laneIndex - 1` within `60.0 m` (`PROPOSED`) and no lane-drop/exit fork is within `150.0 m` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if repeated 3+ times (`PROPOSED`) or lane-hogging causes an overtake/undertake conflict.
- **Pass cases:** (1) Cruise in lane 0 at 70 mph with clear road. (2) Move to lane 1 to pass a 56 mph lorry, then return to lane 0 within `10.0 s` after passing.
- **Fail cases:** (1) Cruise in lane 1 (middle lane) for `20.0 s` with lane 0 empty for `> 100 m`. (2) Stay in lane 2 for `15.0 s` after overtaking a single car.
