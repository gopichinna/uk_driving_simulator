# Next 10 Rules Specification Pack (Batch 4 — Town, Roadworks & Vulnerable Road Users)

**Checked against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Severity mapping:** Matches `content/countries/gb/test_marking.yaml` (`Driving` = minor fault, max 15; `Serious` = fail; `Dangerous` = fail). Numeric judgement-call thresholds not fixed by statute are marked **`PROPOSED`**.

## Scope
Batch 4 covers 10 high-value remaining unimplemented rules across street-lit speed limits, roadworks, double mini-roundabouts, parallel crossings, cycle/bus lanes, emergency vehicles, and safe overtaking (verified against `core/Rules.Core/*.cs` and Batches 1–3 on 2026-11-27).

---

## 1. `GB.SPD.STREET_LIGHTING` — Street-lit built-up 30 mph limit
- **Highway Code:** Rule 124 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** A system of street lights (not more than 183 m / 200 yards apart) with no other speed limit signs means a 30 mph limit in England and Scotland (20 mph in Wales).
- **Game detection & thresholds:** On `links[]` with `hasStreetLighting == true` and `signedLimitMph == null`: apply `effectiveLimitMph = 30.0`; flag if `PlayerState.speedMph > 33.0` (`PROPOSED` tolerance) for `> 2.0 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `speedMph >= 40.0` (`PROPOSED`) or sustained `> 5.0 s`.
- **Pass cases:** (1) Hold 28–30 mph along a street-lit residential road with no repeater signs. (2) Slow to 30 mph before entering a street-lit town boundary.
- **Fail cases:** (1) Drive at 40 mph down a street-lit road assuming national speed limit applies. (2) Accelerate to 36 mph for 6.0 s between street lights.

## 2. `GB.SPD.WORKS_TEMP` — Obey temporary speed limits at roadworks
- **Highway Code:** Rule 288 (`https://www.gov.uk/guidance/the-highway-code/road-works-level-crossings-and-tramways-288-to-307`, checked 2026-11-27)
- **Rule:** You MUST obey temporary speed limits shown at roadworks until you pass the end-of-roadworks or new limit sign.
- **Game detection & thresholds:** Inside `roadworksZones[]` (`tempLimitMph` active between `startM` and `endM`): flag if `PlayerState.speedMph > tempLimitMph + 3.0` (`PROPOSED`) for `> 1.5 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `speedMph >= tempLimitMph + 10.0` (`PROPOSED`) or workers/cones within `2.0 m` (`PROPOSED`).
- **Pass cases:** (1) Slow to 40 mph before passing the temporary 40 mph roadworks sign. (2) Maintain 50 mph through coned motorway lanes until the derestriction sign.
- **Fail cases:** (1) Pass the temporary 40 mph sign at 55 mph. (2) Speed up early before reaching the end-of-roadworks sign.

## 3. `GB.SPD.VARIABLE` — Obey red-circle variable limits on gantries
- **Highway Code:** Rule 261 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** You MUST NOT exceed a mandatory variable speed limit shown inside a red circle on an overhead motorway gantry.
- **Game detection & thresholds:** On `motorway` `links[]` downstream of a gantry with `mandatoryLimitMph != null`: flag if `PlayerState.speedMph > mandatoryLimitMph + 3.0` (`PROPOSED`) after passing the gantry (`> 0.0 m`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `speedMph >= mandatoryLimitMph + 15.0` (`PROPOSED`) approaching queued traffic (`ttc < 3.0 s`).
- **Pass cases:** (1) Ease off before a gantry showing a red-ringed 50 mph limit and hold 48–50 mph. (2) Adjust down to 40 mph when the next gantry drops again.
- **Fail cases:** (1) Cruise at 68 mph under a gantry displaying a red-ringed 50 mph sign. (2) Accelerate past 60 mph before passing a National Speed Limit gantry.

## 4. `GB.RBT.DOUBLE_MINI` — Treat double mini-roundabouts separately
- **Highway Code:** Rules 189–190 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** At double mini-roundabouts, treat each roundabout separately and give way to traffic from your right at each one.
- **Game detection & thresholds:** At `roundabouts[]` with `kind == "double_mini"` (two sequential islands `islandA`, `islandB`): run give-way-to-right check (`ttc < 3.0 s` or `other_decel_req > 2.0 m/s²`, `PROPOSED`) independently at entry to `islandA` and entry to `islandB`.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if forcing `TrafficAgent` on `islandB` to brake (`other_decel_req > 2.5 m/s²`, `PROPOSED`).
- **Pass cases:** (1) Give way at the first mini-roundabout, cross it, then pause and give way to traffic from the right at the second. (2) Keep wheels off the painted central discs where avoidable.
- **Fail cases:** (1) Clear the first mini-roundabout and drive straight across the second in front of a car from the right. (2) Cut straight over both painted islands without yielding.

## 5. `GB.XNG.PARALLEL` — Give way to cyclists and pedestrians at parallel crossings
- **Highway Code:** Rule 195 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** A parallel crossing has a zebra crossing alongside a cycle track; give way to both pedestrians and cyclists waiting to cross or already crossing.
- **Game detection & thresholds:** At `crossings[]` with `kind == "parallel"`: flag if a pedestrian or `CyclistAgent` is within `2.0 m` (`PROPOSED`) of the kerb or on the crossing and player fails to yield (`PlayerState.speedMps > 0.5` past `stopLineM`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if clearance to crossing cyclist/pedestrian `< 1.5 m` (`PROPOSED`).
- **Pass cases:** (1) Stop behind the give-way line when a cyclist approaches the parallel cycle track. (2) Wait until both pedestrians and cyclists clear the crossing.
- **Fail cases:** (1) Stop for a pedestrian on the zebra half, then drive across in front of a cyclist entering the parallel track. (2) Fail to slow when a cyclist waits at the crossing edge.

## 6. `GB.VRU.PASS_PEDESTRIAN` — Leave 2 m when passing pedestrians in the road
- **Highway Code:** Rule 163 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Allow at least 2 metres of space and keep to a low speed when passing pedestrians who are walking in the road (for example where there is no pavement).
- **Game detection & thresholds:** When overtaking a `PedestrianAgent` on `links[]` (`onCarriageway == true`): flag if minimum lateral clearance `< 2.0 m` OR `PlayerState.speedMph > 20.0` (`PROPOSED`) while within `10.0 m` longitudinally (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if lateral clearance `< 1.0 m` (`PROPOSED`) or `speedMph > 30.0`.
- **Pass cases:** (1) Slow to 15 mph and move wide to leave `2.2 m` clearance past pedestrians on a country lane. (2) Wait behind pedestrians until an oncoming car passes.
- **Fail cases:** (1) Pass pedestrians walking in the road at 30 mph with `1.2 m` clearance. (2) Squeeze between pedestrians and an oncoming vehicle.

## 7. `GB.VRU.CYCLE_LANE_BROKEN` — Keep out of broken-line advisory cycle lanes
- **Highway Code:** Rule 140 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Do not drive or park in a cycle lane marked by a broken white line unless it is unavoidable.
- **Game detection & thresholds:** On `links[]` with `advisoryCycleLane == true`: flag if player's nearside tyre enters the cycle lane (`latOffset < laneBoundaryM`) for `> 2.0 s` (`PROPOSED`) when `adjacentLaneClear == true` and no parked obstruction requires moving over.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if a `CyclistAgent` is within `20.0 m` (`PROPOSED`) in the cycle lane and forced to slow or swerve.
- **Pass cases:** (1) Keep your car to the right of the broken white cycle-lane line along the street. (2) Only enter briefly if needed to pass a wide stationary obstruction when clear of cyclists.
- **Fail cases:** (1) Drive with your left wheels inside the advisory cycle lane for `5.0 s` on a clear road. (2) Cut into the cycle lane right behind a cyclist.

## 8. `GB.BUS.LANE_HOURS` — Stay out of bus lanes during operating hours
- **Highway Code:** Rule 141 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Do not drive in a bus lane during its signed operating hours unless signs allow it (such as turning left just before a junction where permitted).
- **Game detection & thresholds:** On `links[]` with `busLaneActive == true` (outside permitted left-turn taper `< 20.0 m` before junction): flag if player enters the bus lane (`inBusLane == true`) for `> 1.5 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if undertaking moving traffic or obstructing a `BusAgent` (`ttc < 3.0 s`, `PROPOSED`).
- **Pass cases:** (1) Stay in the right-hand general traffic lane past an active bus lane. (2) Move into the dashed left-turn taper only at the junction when signed.
- **Fail cases:** (1) Drive 80 m down an active 7 am–7 pm bus lane to skip a queue. (2) Cross the solid white bus-lane line early before a left turn.

## 9. `GB.EMG.LET_PASS` — Let emergency vehicles pass safely and legally
- **Highway Code:** Rule 219 (`https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225`, checked 2026-11-27)
- **Rule:** Look and listen for emergency vehicles with flashing lights and sirens, and give way safely without breaking other rules such as running red lights, entering bus lanes, or stopping on blind bends.
- **Game detection & thresholds:** When an `EmergencyAgent` (`sirenActive == true`) approaches within `60.0 m` behind (`PROPOSED`): flag if player fails to pull left/clear within `5.0 s` (`PROPOSED`) when safe, OR if player crosses a red stop line / mounts kerb (`offCarriageway == true`) to make room.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if player runs a red light (`GB.TL.RED` violation) or blocks the emergency vehicle for `> 8.0 s` (`PROPOSED`).
- **Pass cases:** (1) Check mirrors, signal left, and pull over smoothly on a straight clear stretch to let an ambulance pass. (2) Wait calmly behind a red light until green, then move left safely.
- **Fail cases:** (1) Ignore a flashing fire engine behind you for 10.0 s while continuing in the right lane. (2) Drive through a red traffic light to get out of the way.

## 10. `GB.OVT.UNSAFE_PLACE` — Do not overtake where sightlines are limited or near hazards
- **Highway Code:** Rules 166–167 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Do not overtake if you cannot see far enough ahead (such as bends, hill brows, or hump bridges), or where you would come into conflict with junctions, pedestrian crossings, or level crossings.
- **Game detection & thresholds:** When player enters the opposing lane (`latOffset > centrelineM`) to pass a `TrafficAgent`: flag if `forwardVisibilityM < 100.0` (`PROPOSED`) OR within `30.0 m` (`PROPOSED`) of an unmarked/side junction, pedestrian crossing, or brow/bend zone.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if an oncoming `TrafficAgent` appears with `ttc < 4.0 s` (`PROPOSED`) or forces emergency braking.
- **Pass cases:** (1) Hold back behind a slow tractor before a blind bend and overtake only on the straight with a clear view. (2) Wait until clear of a side junction before passing a parked van.
- **Fail cases:** (1) Pull into the oncoming lane to overtake before a hill brow. (2) Overtake a slow car within 15 m of a pedestrian crossing.
