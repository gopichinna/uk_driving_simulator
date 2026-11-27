# Next 20 Rules Specification Pack (Batch 2 — Stages 3–8)

**Checked against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Severity mapping:** Matches `content/countries/gb/test_marking.yaml` (`Driving` = minor fault, max 15; `Serious` = fail; `Dangerous` = fail). Numeric judgement-call thresholds not fixed by statute are marked **`PROPOSED`**.

## Scope
Batch 2 covers 20 remaining unimplemented planned rules taught in **Stages 3–8**: roundabouts (`GB.RBT.*`), lane choice (`GB.POS.*`), weather & following gaps (`GB.GAP.WET_ICY`, `GB.WX.*`), motorways (`GB.MWY.*`), and parking/stopping (`GB.PRK.*`). None of these 20 rules are implemented in `core/Rules.Core/*.cs` as of 2026-11-27.

---

## 1. `GB.RBT.CYCLISTS` — Give cyclists room on roundabouts
- **Highway Code:** Rule 187 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Give cyclists extra room on roundabouts and do not cut across them when they keep to the outside lane.
- **Game detection & thresholds:** Inside a roundabout `nodes[]` (`kind == "roundabout"`) or within `15.0 m` (`PROPOSED`) of its exit: flag if player passes or turns across a `TrafficAgent` (`kind == "cyclist"`) with lateral/longitudinal clearance `< 1.5 m` (`PROPOSED`) or `cyclist_decel_req > 2.0 m/s²` (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `cyclist_decel_req > 4.0 m/s²` (`PROPOSED`) or collision occurs.
- **Pass cases:** (1) Hold behind a cyclist on the outer lane until exiting cleanly. (2) Leave `>= 1.8 m` clearance when passing a cyclist on a two-lane roundabout.
- **Fail cases:** (1) Exit from the inner lane directly across a cyclist staying on the outer lane (`clearance < 1.0 m`). (2) Squeeze past a cyclist on a single-lane roundabout with `0.8 m` clearance.

## 2. `GB.RBT.MINI` — Mini-roundabouts
- **Highway Code:** Rule 188 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Approach mini-roundabouts with the same priority as normal roundabouts and avoid driving over the painted central circle unless turning a large vehicle.
- **Game detection & thresholds:** At `nodes[]` (`kind == "mini_roundabout"`): flag if player fails to give way to traffic from the right (`other_decel_req > 2.0 m/s²`, `PROPOSED`) or drives across the painted central circle (`distanceToCentre < 1.5 m`, `PROPOSED`) in a standard car.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if driving over the centre conflicts with circulating traffic (`ttc < 1.5 s`, `PROPOSED`).
- **Pass cases:** (1) Give way to a car from the right, then drive clockwise around the white circle. (2) Signal left/right as for a normal roundabout.
- **Fail cases:** (1) Cut straight across the painted circle (`distanceToCentre = 0.4 m`) to turn right. (2) Pull out in front of a car approaching from the right (`other_decel_req = 3.2 m/s²`).

## 3. `GB.RBT.LONG_VEHICLES` — Give long vehicles room on roundabouts
- **Highway Code:** Rule 187 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Watch out for long vehicles (lorries/buses) on roundabouts as they may need extra room or straddle lanes.
- **Game detection & thresholds:** Inside a roundabout `nodes[]` when a `TrafficAgent` (`kind == "lorry"` / `"bus"`) is circulating within `20.0 m` (`PROPOSED`): flag if player pulls alongside inside the swept path (`clearance < 1.5 m`, `PROPOSED`) or overtakes it inside the roundabout.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if `clearance < 0.8 m` (`PROPOSED`) or `other_decel_req > 2.5 m/s²`.
- **Pass cases:** (1) Hold back behind a lorry as it swings wide across two lanes to turn right. (2) Wait until the lorry clears the exit before accelerating alongside.
- **Fail cases:** (1) Squeeze up the left side of a lorry indicating left inside the roundabout (`clearance = 0.7 m`). (2) Cut across the front of a slow lorry on exit.

## 4. `GB.POS.LANE_FOR_ROUTE` — Choose the right lane early from signs and arrows
- **Highway Code:** Rules 133 & 138 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Read direction signs and road arrows early and move into the lane for your route in good time.
- **Game detection & thresholds:** Within `40.0 m` (`PROPOSED`) before a multi-lane junction `nodes[]` with `laneConnections[]` restrictions: flag if `PlayerState.laneIndex` does not match the signed route lane (`other_decel_req > 1.5 m/s²` on late cut-in, or exits from wrong lane).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if a late swerve within `15.0 m` (`PROPOSED`) forces another `TrafficAgent` to brake (`other_decel_req > 3.0 m/s²`).
- **Pass cases:** (1) Move into the right-turn lane `60.0 m` before the junction following the direction sign. (2) Stay in the left straight-ahead lane cleanly.
- **Fail cases:** (1) Stay in the left-only lane until `10.0 m` before the line, then cut across to turn right. (2) Go straight ahead from a marked left-turn-only lane.

## 5. `GB.POS.THREE_LANE_SINGLE` — Three-lane single-carriageway / multi-lane positioning
- **Highway Code:** Rule 138 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** On a three-lane dual carriageway or multi-lane road, keep to the left lane unless overtaking; return left when clear.
- **Game detection & thresholds:** On `links[].lanes >= 3` (`twoWay == false`): flag if `PlayerState.laneIndex >= 1` for `> 10.0 s` (`PROPOSED`) with no slower `TrafficAgent` within `50.0 m` (`PROPOSED`) in `laneIndex - 1`.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if sitting in the outermost lane (`laneIndex == lanes - 1`) for `> 15.0 s` (`PROPOSED`) with empty lanes to the left.
- **Pass cases:** (1) Drive in lane 0, move to lane 1 to pass a slower car, then return to lane 0. (2) Use lane 2 briefly to pass a car in lane 1, then move back left.
- **Fail cases:** (1) Cruise in lane 1 (middle lane) for `20.0 s` with lane 0 empty. (2) Cruise in lane 2 for `18.0 s` while lanes 0 and 1 are clear.

## 6. `GB.GAP.WET_ICY` — Double following distance in rain (4 seconds)
- **Highway Code:** Rule 227 (`https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237`, checked 2026-11-27)
- **Rule:** Stopping distances are at least doubled in wet weather; leave at least a 4-second gap behind the vehicle ahead.
- **Game detection & thresholds:** When scenario `conditions.weather == "rain"` (or wet surface friction active): flag if time headway to the lead `TrafficAgent` (`gapM / max(PlayerState.speedMps, 1.0)`) is `< 4.0 s` for `> 3.0 s` (`PROPOSED`) at `speedMph >= 15.0`.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if headway `< 2.0 s` (`PROPOSED`) in rain at `>= 30.0 mph` or triggers emergency braking.
- **Pass cases:** (1) Hold a `4.5 s` gap behind a car at 40 mph in rain (`~80 m`). (2) Ease off smoothly to restore a `4.0 s` gap if another car merges ahead.
- **Fail cases:** (1) Follow `2.2 s` behind a car at 40 mph in heavy rain for `6.0 s`. (2) Tailgate at `1.2 s` on a wet dual carriageway.

## 7. `GB.MWY.JOIN` — Join a motorway safely from a slip road
- **Highway Code:** Rule 259 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Give priority to traffic already on the motorway; match speed on the slip road and merge into a safe gap in lane 0.
- **Game detection & thresholds:** Merging from `roadClass == "motorway_link"` onto `roadClass == "motorway"` (`laneIndex == 0`): flag if `PlayerState.speedMph < 45.0` (`PROPOSED`) at merge point when lane 0 is moving at `>= 60 mph`, or if merge causes `other_decel_req > 2.0 m/s²` (`PROPOSED`) or `ttc < 2.0 s` (`PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if stopping on the slip-road merge taper without an obstruction or forcing `other_decel_req > 4.0 m/s²`.
- **Pass cases:** (1) Accelerate to `62 mph` on the slip road, signal right `3.0 s` early, and merge into a `5.0 s` gap. (2) Adjust speed to slot smoothly behind a lorry in lane 0.
- **Fail cases:** (1) Merge onto a 70 mph motorway at `35 mph` directly ahead of a lorry (`other_decel_req = 3.5 m/s²`). (2) Force across the hatched chevron marking before the slip road ends.

## 8. `GB.MWY.LEAVE` — Leave a motorway cleanly
- **Highway Code:** Rule 273 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Move into the left-hand lane well before your exit, signal left in good time, and slow down only after entering the slip road.
- **Game detection & thresholds:** Approaching an active exit `motorway_link`: flag if player crosses from `laneIndex >= 1` directly onto the exit within `80.0 m` (`PROPOSED`) of the gore, omits left indicator (`< 2.0 s`, `PROPOSED`), or brakes `> 2.5 m/s²` (`PROPOSED`) while still on the main carriageway.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if cutting across lane 0 traffic (`other_decel_req > 2.5 m/s²`) or crossing painted chevrons.
- **Pass cases:** (1) Move to lane 0 before the 300-yard countdown marker, signal left `4.0 s` early, and brake on the slip road. (2) Hold `65 mph` until fully on the deceleration lane.
- **Fail cases:** (1) Dive from lane 2 across lane 0 and the chevrons `30.0 m` before the exit. (2) Slow to `40 mph` in lane 0 before entering the slip road.

## 9. `GB.MWY.OVERTAKE_RIGHT` — Overtake on the right on motorways
- **Highway Code:** Rule 267 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Overtake only when safe and always pass slower vehicles on the right.
- **Game detection & thresholds:** On `links[].roadClass == "motorway"` during an overtake manoeuvre: flag if player initiates an overtake with `< 2.5 s` (`PROPOSED`) closing headway to a faster vehicle approaching in the target right lane (`other_decel_req > 2.0 m/s²`, `PROPOSED`) or fails to signal `>= 2.0 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if pulling into a right lane forces an approaching car to brake hard (`other_decel_req > 3.5 m/s²`).
- **Pass cases:** (1) Check right lane is clear for `> 4.0 s`, signal right `2.5 s` early, pass a lorry, then return left. (2) Hold in lane 0 behind a slower car until a fast car in lane 1 passes.
- **Fail cases:** (1) Pull into lane 1 `15.0 m` ahead of a 70 mph car (`other_decel_req = 3.8 m/s²`). (2) Move right without signalling to overtake.

## 10. `GB.MWY.NO_UNDERTAKE` — Do not undertake on the left
- **Highway Code:** Rule 268 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Do not overtake on the left or weave into a left lane to pass faster traffic, except when congested lanes move slowly in queues.
- **Game detection & thresholds:** On `links[].roadClass == "motorway"` when average lane speeds `>= 35.0 mph` (`PROPOSED`, not congested queue): flag if player passes a `TrafficAgent` on its left (`laneIndex < agent.laneIndex`) at `> agent.speedMph + 5.0` (`PROPOSED`), especially after changing left specifically to pass.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if weaving left-then-right around a middle-lane vehicle with `< 1.5 s` gap.
- **Pass cases:** (1) Move right to pass a middle-lane hogger legally in lane 2. (2) Keep pace in lane 0 during a 15 mph congested queue when lane 1 slows to 10 mph.
- **Fail cases:** (1) Stay in lane 0 at 70 mph and undertake a car travelling 55 mph in lane 1. (2) Switch from lane 1 to lane 0 to pass a car, then pull back into lane 1.

## 11. `GB.MWY.RED_X` — Obey overhead red X closed-lane signals
- **Highway Code:** Rule 258 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** When a red X (with flashing red lights) shows above a lane, you MUST NOT drive beyond the gantry in that lane.
- **Game detection & thresholds:** Passing `gantries[]` when scenario `red_x` event marks `laneIndex` closed: flag if player passes the gantry coordinate (`x, z`) inside the closed `laneIndex` (even for `0.1 m`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if a stopped vehicle or roadworks hazard is present in that closed lane within `200.0 m` (`PROPOSED`).
- **Pass cases:** (1) Signal and move out of lane 0 into lane 1 `100.0 m` before the red X gantry. (2) Stay out of lane 2 until a green/clear gantry appears.
- **Fail cases:** (1) Drive under a red X gantry in the closed lane and merge afterwards. (2) Ignore a red X for `200.0 m` because the lane looks empty.

## 12. `GB.MWY.AMBER_FLASH` — Respond to flashing amber motorway warnings
- **Highway Code:** Rule 258 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Flashing amber lights on motorway signals warn of a hazard ahead; slow down and be prepared to change lanes or stop.
- **Game detection & thresholds:** After passing `gantries[]` with an active `variable_limit` or flashing amber warning (`50` / `40 mph` advisory): flag if `PlayerState.speedMph` remains above the displayed speed (`+ 3.0 mph` tolerance, `PROPOSED`) or exceeds `55.0 mph` (`PROPOSED` when amber-only warning active without numeric limit) for `> 5.0 s` (`PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if exceeding a mandatory red-ringed variable speed limit (`GB.SPD.VARIABLE`) or approaching queued traffic with `ttc < 2.0 s`.
- **Pass cases:** (1) Ease off smoothly from 70 mph to `<= 50 mph` before reaching the gantry showing flashing amber / 50. (2) Maintain `48 mph` through the warning section until `End` sign.
- **Fail cases:** (1) Hold 70 mph past two flashing amber warning gantries. (2) Brake late at `> 4.0 m/s²` upon reaching the queued traffic ahead.

## 13. `GB.MWY.HARD_SHOULDER` — Do not drive or stop on the hard shoulder
- **Highway Code:** Rules 263 & 271 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** You MUST NOT drive or stop on the hard shoulder except in an emergency or when signs specifically direct you to use it.
- **Game detection & thresholds:** On `links[].roadClass == "motorway"` with `hardShoulder == true` (when no active `all_lane_running` override): flag if player's vehicle crosses onto the hard shoulder (`lateralOffsetM` right/left outside lane 0 by `> 0.5 m`, `PROPOSED`) or stops there without an active `breakdown` event.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if overtaking on the hard shoulder or colliding with a stopped vehicle (`ttc < 1.5 s`).
- **Pass cases:** (1) Keep inside lane 0 lines past a broken-down car on the hard shoulder. (2) Pull onto the hard shoulder only when a `breakdown` event triggers.
- **Fail cases:** (1) Drive along the hard shoulder for `30.0 m` to reach an upcoming exit early. (2) Stop on the hard shoulder for no emergency.

## 14. `GB.MWY.EMERGENCY_AREA` — Use motorway emergency areas only in emergencies
- **Highway Code:** Rule 275 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** On smart motorways without a hard shoulder, use an emergency area only in a genuine emergency.
- **Game detection & thresholds:** At `emergencyAreas[]` polygon: flag if player enters/stops in an emergency area without an active `breakdown` scenario event, OR during `breakdown` fails to pull fully into the bay (`> 0.5 m` outside bay, `PROPOSED`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if stopping in a live lane `0` within `50.0 m` (`PROPOSED`) of an available emergency area during a controllable fault, or rejoining into `ttc < 2.0 s` traffic.
- **Pass cases:** (1) Drive past an orange emergency area cleanly when your car is healthy. (2) During a `breakdown` event, coast into the emergency area and stop inside the bay.
- **Fail cases:** (1) Pull into an emergency area during normal driving with no breakdown. (2) Stop in live lane 0 instead of rolling into the adjacent emergency area.

## 15. `GB.WX.HEADLIGHTS_NIGHT` — Use dipped headlights at night
- **Highway Code:** Rules 113 & 114 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** You MUST ensure all sidelights and rear registration plate lights are lit between sunset and sunrise, and use headlights at night (dipped in built-up areas or when meeting traffic).
- **Game detection & thresholds:** When `conditions.timeOfDay == "night"`: flag if `PlayerState.headlights == "off"` for `> 3.0 s` (`PROPOSED`) while moving (`speedMps > 0.5`), or `headlights == "full_beam"` within `150.0 m` (`PROPOSED`) of an oncoming or followed `TrafficAgent`.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if driving unlit at `>= 30.0 mph` or dazzling an oncoming vehicle within `60.0 m` (`PROPOSED`).
- **Pass cases:** (1) Switch on dipped headlights before moving off in a night scenario. (2) Dip full beams `>= 150.0 m` before meeting an oncoming car on an unlit road.
- **Fail cases:** (1) Drive for `10.0 s` at night with headlights switched off. (2) Keep full-beam headlights on while meeting an oncoming car `50.0 m` away.

## 16. `GB.WX.LOW_VISIBILITY` — Headlights in seriously reduced visibility (< 100 m)
- **Highway Code:** Rule 226 (`https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237`, checked 2026-11-27)
- **Rule:** You MUST use headlights when visibility is seriously reduced, generally when you cannot see for more than 100 metres.
- **Game detection & thresholds:** When `conditions.visibilityM <= 100.0` (`weather == "fog"` or heavy `"rain"`): flag if `PlayerState.headlights == "off"` for `> 3.0 s` (`PROPOSED`) while moving.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `visibilityM <= 50.0` (`PROPOSED`) and driving unlit at `>= 25.0 mph`.
- **Pass cases:** (1) Switch on dipped headlights as soon as `visibilityM` drops to `80.0 m`. (2) Use dipped headlights (not full beam, which reflects off fog).
- **Fail cases:** (1) Drive in `70.0 m` fog with headlights off. (2) Drive with sidelights only in `60.0 m` visibility.

## 17. `GB.WX.FOG_LIGHTS` — Switch off fog lights when visibility improves
- **Highway Code:** Rule 236 (`https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237`, checked 2026-11-27)
- **Rule:** You MUST NOT use front or rear fog lights unless visibility is seriously reduced (`<= 100 m`), and MUST switch them off when visibility improves.
- **Game detection & thresholds:** When `conditions.visibilityM > 100.0`: flag if `PlayerState.fogLights == true` for `> 8.0 s` (`PROPOSED` grace window after fog clears).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if rear fog lights dazzle a following `TrafficAgent` within `30.0 m` (`PROPOSED`) in clear weather.
- **Pass cases:** (1) Turn fog lights on at `60.0 m` fog, then turn them off within `5.0 s` after visibility rises above `150.0 m`. (2) Keep fog lights off in light rain (`visibilityM = 300.0 m`).
- **Fail cases:** (1) Leave rear fog lights on for `20.0 s` after exiting a fog bank into clear daylight. (2) Switch on fog lights in clear night traffic.

## 18. `GB.WX.FOG_STOPPING` — Drive within your visible stopping distance in fog
- **Highway Code:** Rule 235 (`https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237`, checked 2026-11-27)
- **Rule:** In fog, drive slowly enough that you can always pull up well within the clear distance you can see ahead.
- **Game detection & thresholds:** When `conditions.weather == "fog"` (`visibilityM <= 100.0`): compute Highway Code wet/fog stopping distance `d_stop = (v² / (2 * 4.5)) + v * 1.0` (`PROPOSED` conservative wet decel `4.5 m/s²` + `1.0 s` reaction); flag if `d_stop > 0.8 * conditions.visibilityM` (`PROPOSED`, e.g., `> 30 mph` in `50 m` fog) for `> 2.0 s`.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if `d_stop > conditions.visibilityM` (`PROPOSED`) or near-miss with a vehicle ahead (`ttc < 1.5 s`).
- **Pass cases:** (1) Hold `22 mph` (`d_stop ≈ 21 m`) when `visibilityM == 50.0 m`. (2) Slow to `15 mph` when fog thickens to `30.0 m`.
- **Fail cases:** (1) Drive at `45 mph` (`d_stop ≈ 65 m`) in `50.0 m` fog. (2) Hold `60 mph` on a foggy dual carriageway with `80.0 m` visibility.

## 19. `GB.PRK.YELLOW_LINES` — Do not wait or park on yellow lines
- **Highway Code:** Rule 238 (`https://www.gov.uk/guidance/the-highway-code/waiting-and-parking-238-to-252`, checked 2026-11-27)
- **Rule:** You MUST NOT wait or park on double yellow lines at any time, or on single yellow lines during signed restricted hours.
- **Game detection & thresholds:** On `links[]` with `parkingRestriction == "double_yellow"` (or active `"single_yellow"`): flag if player intentionally stops (`speedMps < 0.2` and `handbrake == true` or stopped `> 10.0 s` (`PROPOSED`) with no traffic queue, red light, or pedestrian hazard within `20.0 m`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if stopping on yellow lines forces following traffic into the oncoming lane (`other_decel_req > 2.5 m/s²`).
- **Pass cases:** (1) Pass double yellow lines without stopping and pull up in a marked bay ahead. (2) Pause briefly only if held by a traffic queue or pedestrian crossing.
- **Fail cases:** (1) Pull over and stop with handbrake on over double yellow lines for `12.0 s`. (2) Park on a single yellow line during active daytime restriction hours.

## 20. `GB.PRK.JUNCTION_10M` — Do not stop or park within 10 metres of a junction
- **Highway Code:** Rule 243 (`https://www.gov.uk/guidance/the-highway-code/waiting-and-parking-238-to-252`, checked 2026-11-27)
- **Rule:** Do not stop or park opposite or within 10 metres (32 feet) of a junction, except in an authorised parking space.
- **Game detection & thresholds:** When player parks/pulls over (`speedMps < 0.2` for `> 5.0 s`, `PROPOSED`, not waiting to turn/give way): flag if distance along `links[]` to the nearest junction `nodes[]` mouth is `< 10.0 m` (exact Highway Code Rule 243 threshold).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if parked within `5.0 m` (`PROPOSED`) of the junction mouth blocking turning sightlines.
- **Pass cases:** (1) Pull over at the kerb `15.0 m` clear past a T-junction. (2) Stop `12.0 m` before a side road to set down a passenger.
- **Fail cases:** (1) Park `4.0 m` from a T-junction corner, blocking emerging drivers' view. (2) Stop `8.0 m` opposite a side-road mouth.
