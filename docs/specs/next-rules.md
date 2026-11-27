# Next 20 Rules Specification Pack (Stages 0–6)

**Checked against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Severity mapping:** Matches `content/countries/gb/test_marking.yaml` (`Driving` = minor fault, max 15; `Serious` = fail; `Dangerous` = fail).

## Ranking Rationale
These 20 planned rules were chosen from the 117 unimplemented rules in `content/countries/gb/rules.yaml` because every one of them is **already taught in Stages 0–6** (`content/lessons/gb/01–18, 24–27`) and can be judged using geometry and traffic state already present in `Sim.Core` and `RoadPack` v0.7 (lane centreline, speed limit, indicators, stop/give-way lines, traffic-light state, crossings, and nearby agents). Implementing them unlocks full live judging for Stages 0–2 (basics, junctions, traffic lights, crossings) and motorway lane discipline without inventing new content contracts.

---

## 1. `GB.POS.KEEP_LEFT` — Keep to the left lane
- **Highway Code:** Rule 160 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Keep to the left on two-way roads unless overtaking or turning right.
- **Game detection:** Player lateral offset relative to the nearside (`left`) lane centreline on two-way links (`twoWay == true`). No new inputs needed.
- **Fault & severity:** Driving (`Serious` / `Dangerous` if oncoming traffic is present).
- **Pass cases:** (1) Drive centred in the left lane along a straight town road. (2) Move right briefly to pass a parked car when clear, then return left.
- **Fail cases:** (1) Cruise in the oncoming right-hand lane with no obstruction. (2) Straddle the centre line as an oncoming car approaches.

## 2. `GB.POS.DUAL_KEEP_LEFT` — Keep left on dual carriageways
- **Highway Code:** Rule 137 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Use the left-hand lane on a dual carriageway unless overtaking or turning right.
- **Game detection:** Player lane index (`laneIndex > 0`) on `lanes >= 2` dual-carriageway links when no slower vehicle is ahead within 40 m and no right turn is within 100 m. No new inputs needed.
- **Fault & severity:** Driving (`Serious` if repeated).
- **Pass cases:** (1) Stay in lane 0 (left) at 40 mph with clear road. (2) Move to lane 1 to overtake a slow lorry, then return to lane 0.
- **Fail cases:** (1) Cruise in lane 1 for > 10 s with lane 0 empty. (2) Stay in lane 1 after passing a vehicle with a clear gap on the left.

## 3. `GB.POS.AFTER_TURN` — Position left after turning
- **Highway Code:** Rule 160 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Join the left-hand lane after turning into a new road.
- **Game detection:** Player lane index / lateral offset within 30 m after exiting a junction node onto a two-way or multi-lane link. No new inputs needed.
- **Fault & severity:** `Serious` (`Dangerous` if oncoming traffic is present).
- **Pass cases:** (1) Turn left at a T-junction cleanly into the left lane. (2) Turn right into a two-way side road and settle in the left lane.
- **Fail cases:** (1) Swing wide on a left turn into the oncoming lane. (2) Turn right and continue driving on the right-hand side.

## 4. `GB.OBS.MOVE_OFF_CHECKS` — Pull away safely
- **Highway Code:** Rule 159 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Pull away from the kerb only when passing traffic will not have to slow down.
- **Game detection:** Following/approaching vehicle `other_decel_req` (`> 1.5 m/s²` minor, `> 3.0 m/s²` serious) within 3 s after player speed transitions from `< 0.5 m/s` to moving from the kerb. No new inputs needed.
- **Fault & severity:** Driving (`Serious` if a following vehicle must brake hard, `other_decel_req > 3 m/s²`).
- **Pass cases:** (1) Wait at the kerb until a passing car clears, then pull away smoothly. (2) Pull away on an empty street with a timely right signal.
- **Fail cases:** (1) Pull out directly in front of a car 15 m behind, forcing it to brake. (2) Pull away into a cyclist passing alongside.

## 5. `GB.OBS.MSM` — Mirror–Signal–Manoeuvre
- **Highway Code:** Rule 161 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Signal and check for traffic behind before braking sharply or turning.
- **Game detection:** Outcome-based (per `AGENTS.md` §4.7, no mirror-tap busywork): flag if player turns or slows sharply (`decel > 3.5 m/s²` without hazard ahead) without signalling >= 1.5 s prior or forcing behind traffic to brake. No new inputs needed.
- **Fault & severity:** Driving (`Serious` on conflict).
- **Pass cases:** (1) Signal left 3 s before slowing smoothly for a side road. (2) Slow gradually for a red light with brake lights visible early.
- **Fail cases:** (1) Brake suddenly and turn left with no prior signal. (2) Swerve into a side road, forcing the car behind to brake.

## 6. `GB.SPD.APPROPRIATE` — Safe speed for hazards
- **Highway Code:** Rule 125 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Drive slower than the speed limit when parked cars, bends, or crossings reduce your view.
- **Game detection:** Player speed vs hazard-adjusted target speed within 25 m of parked cars, tight bends, or pedestrian crossings (`speed > 0.85 * limit` when clearance `< 1.5 m` or visibility `< 40 m`). Needs hazard-zone flag or parked-car proximity (already in `Sim.Core`).
- **Fault & severity:** Driving (`Serious` if near-miss occurs).
- **Pass cases:** (1) Ease from 30 mph to 20 mph passing a row of parked cars. (2) Slow below 25 mph approaching a blind bend.
- **Fail cases:** (1) Hold 30 mph squeezing past parked cars with pedestrians nearby. (2) Enter a tight rural bend at 60 mph.

## 7. `GB.SCH.SLOW` — Slow down near schools
- **Highway Code:** Rule 208 (`https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225`, checked 2026-11-27)
- **Rule:** Slow down and watch for children outside schools.
- **Game detection:** Player speed inside `school_zone` / `warning_signs` (`kind == "school"`) radius (`> 20 mph` when school zone active). Needs `warning_signs` / `school` zone loaded into `Sim.Core`.
- **Fault & severity:** Driving (`Serious` if children/pedestrians are present).
- **Pass cases:** (1) Drop to 20 mph past the school warning sign. (2) Hold <= 18 mph while pedestrians wait on the school pavement.
- **Fail cases:** (1) Drive at 30 mph past a signed school zone. (2) Fail to slow when pedestrians stand near the school gate.

## 8. `GB.JCT.STOP_SIGN` — Stop at a STOP line
- **Highway Code:** Rule 171 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Come to a complete stop behind the solid white line at every STOP sign.
- **Game detection:** Minimum player speed (`minSpeed < 0.2 m/s`) within 4 m before a `stop` line node (`RoadPack` node/sign tag). Needs `stop` line flag on `RoadPack` junction arm.
- **Fault & severity:** `Serious`.
- **Pass cases:** (1) Stop completely behind the solid line for 1 s even when the cross road is empty. (2) Stop, check both ways, then pull out cleanly.
- **Fail cases:** (1) Roll through the STOP line at 5 mph because the road looks clear. (2) Stop with front wheels past the solid line.

## 9. `GB.JCT.H2_PEDESTRIANS` — Give way to pedestrians at junctions (H2)
- **Highway Code:** Rule H2 & Rule 170 (`https://www.gov.uk/guidance/the-highway-code/introduction`, checked 2026-11-27)
- **Rule:** Give way to pedestrians crossing or waiting to cross a side road you are turning into.
- **Game detection:** Active pedestrian agent at junction mouth (`pedestrianRoutes` `mouthCrossings`) + player turning into that arm (`playerSpeed > 0.5 m/s` within 3 m of crossing pedestrian). No new inputs needed.
- **Fault & severity:** Driving (`Serious` if pedestrian must stop/step back; `Dangerous` if clearance `< 1 m` at `> 10 mph`).
- **Pass cases:** (1) Stop before turning left to let a waiting pedestrian cross the mouth. (2) Wait until the pedestrian clears the road before completing a right turn.
- **Fail cases:** (1) Turn across in front of a pedestrian waiting at the mouth. (2) Force a crossing pedestrian to step back.

## 10. `GB.JCT.H3_CYCLISTS` — Do not cut across cyclists at junctions (H3)
- **Highway Code:** Rule H3 (`https://www.gov.uk/guidance/the-highway-code/introduction`, checked 2026-11-27)
- **Rule:** Give way to cyclists going straight ahead before you turn across their path.
- **Game detection:** Cyclist agent travelling straight on nearside/cycle lane within 25 m of junction + player turning left/right across cyclist's trajectory (`cyclist_decel_req > 2.0 m/s²` or lateral clearance `< 1.5 m`). No new inputs needed.
- **Fault & severity:** `Serious` (`Dangerous` if the cyclist brakes or swerves).
- **Pass cases:** (1) Hold back behind a cyclist going straight before turning left. (2) Wait for an oncoming cyclist to clear before turning right.
- **Fail cases:** (1) Overtake a cyclist and immediately turn left across them ("left hook"). (2) Turn right across an oncoming cyclist's path.

## 11. `GB.JCT.TURN_NEAR` — Left turn from the left
- **Highway Code:** Rule 182–183 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Keep close to the left kerb and signal left in good time before turning left.
- **Game detection:** Player lateral offset from left kerb (`> 1.5 m` clear of kerb) and left indicator state within 30 m of a left turn. No new inputs needed.
- **Fault & severity:** Driving (`Serious` if cutting across a nearside cyclist/lane).
- **Pass cases:** (1) Signal left 30 m early and hug the left lane into the turn. (2) Slow smoothly in the left lane before a tight left corner.
- **Fail cases:** (1) Swing wide to the right before turning left. (2) Turn left from the right-hand approach lane.

## 12. `GB.JCT.TURN_FAR_POSITION` — Right turn position
- **Highway Code:** Rule 179–180 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Move close to the centre line (without crossing it) before turning right.
- **Game detection:** Player lateral offset from road centreline (`< 1.0 m` left of centreline) within 25 m before a right turn at a junction. No new inputs needed.
- **Fault & severity:** Driving.
- **Pass cases:** (1) Position just left of the centre line so cars behind can pass on the left if room allows. (2) Use a marked right-turn pocket lane.
- **Fail cases:** (1) Hug the far-left kerb while signalling right. (2) Cross over the centre line into oncoming traffic before turning.

## 13. `GB.JCT.TURN_FAR_GAP` — Right turn gap across oncoming traffic
- **Highway Code:** Rule 180 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Wait until oncoming traffic is clear before turning right across its lane.
- **Game detection:** Oncoming vehicle `ttc` (`< 3.5 s`) or `other_decel_req > 2.0 m/s²` when player crosses the centreline turning right. No new inputs needed.
- **Fault & severity:** `Serious` (`Dangerous` on near-miss or collision).
- **Pass cases:** (1) Wait at the centre line until two oncoming cars pass, then turn smoothly. (2) Turn promptly when the oncoming lane is clear for > 5 s.
- **Fail cases:** (1) Turn right across an oncoming car, forcing it to brake hard. (2) Misjudge the speed of an approaching motorcycle.

## 14. `GB.TL.RED` — Stop at a red traffic light
- **Highway Code:** Rule 175–176 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** You must stop behind the white stop line when the traffic light shows red.
- **Game detection:** Signal phase (`RED`) + player front bumper crossing the link's `stopLine` position. No new inputs needed.
- **Fault & severity:** `Serious` (`Dangerous` if a crossing vehicle or pedestrian is present).
- **Pass cases:** (1) Stop cleanly behind the white line on red and wait for green. (2) Creep only after the light turns green and junction is clear.
- **Fail cases:** (1) Drive straight through a red light. (2) Stop past the white stop line inside the pedestrian crossing.

## 15. `GB.TL.AMBER` — Stop at an amber traffic light if safe
- **Highway Code:** Rule 175 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Stop behind the line on amber unless you are so close that stopping could cause a crash.
- **Game detection:** Signal phase (`AMBER`) + player stopping distance (`v² / (2 * 4.5)`) <= distance to stop line when amber begins, yet player crosses stop line. No new inputs needed.
- **Fault & severity:** Driving.
- **Pass cases:** (1) Brake smoothly to a stop behind the line when amber lights up 35 m ahead. (2) Continue cleanly when amber appears 4 m from the line at 30 mph.
- **Fail cases:** (1) Accelerate through an amber light from 40 m away. (2) Cross the stop line after amber has been lit for > 2.5 s.

## 16. `GB.TL.RED_AMBER` — Wait until green (no jumping red-amber)
- **Highway Code:** Light signals (`https://www.gov.uk/guidance/the-highway-code/light-signals-controlling-traffic`, checked 2026-11-27)
- **Rule:** Do not cross the stop line while red and amber are lit together; wait for green.
- **Game detection:** Signal phase (`RED_AMBER`) + player crossing the stop line before `GREEN`. No new inputs needed.
- **Fault & severity:** `Serious`.
- **Pass cases:** (1) Hold behind the line during red-and-amber and move off on green. (2) Wait an extra moment on green if a pedestrian is still crossing.
- **Fail cases:** (1) Move off and cross the white line during red-and-amber. (2) Jump the lights 0.5 s before green.

## 17. `GB.TL.ASL` — Advanced stop line (cycle box)
- **Highway Code:** Rule 178 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Stop behind the first white line on red so cyclists have the marked box ahead.
- **Game detection:** `asl == true` on signalised link + player front bumper inside the ASL box (`stopLine` to `aslLine`) during `RED` or `RED_AMBER`. Needs `aslLine` offset in `RoadPack` (or 4 m standard offset).
- **Fault & severity:** `Serious`.
- **Pass cases:** (1) Stop behind the first white line on red, leaving the green cycle box empty. (2) Wait behind the box until green, then let cyclists clear.
- **Fail cases:** (1) Pull forward into the cycle box on a red light. (2) Block a cyclist waiting inside the ASL.

## 18. `GB.TL.CLEAR_JUNCTION` — Do not block a green-light junction
- **Highway Code:** Rule 176 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Even when the light is green, wait behind the line if queued traffic leaves no room beyond the junction.
- **Game detection:** Signal phase `GREEN` + exit link blocked by stopped vehicle within 12 m of junction exit + player stops inside junction polygon for `> 2 s`. No new inputs needed.
- **Fault & severity:** Driving (`Serious` if blocking cross traffic when lights change).
- **Pass cases:** (1) Hold behind the stop line on green until space opens beyond the junction. (2) Cross smoothly once the exit lane clears.
- **Fail cases:** (1) Drive onto the junction behind a stopped queue and sit across the crossing when lights turn red.

## 19. `GB.XNG.ZEBRA` — Give way at zebra crossings
- **Highway Code:** Rule 195 & Rule H2 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Slow down and give way when someone is waiting or crossing at a zebra crossing.
- **Game detection:** Zebra crossing node (`crossings[]`) + pedestrian waiting/stepping onto stripes + player fails to stop before the crossing (`speed > 0.5 m/s` across stripes while pedestrian active). No new inputs needed.
- **Fault & severity:** Driving (if pedestrian waiting) / `Serious` → `Dangerous` (if pedestrian on crossing).
- **Pass cases:** (1) Stop smoothly before the stripes when a pedestrian steps out. (2) Wait until the pedestrian reaches the far pavement before moving off.
- **Fail cases:** (1) Drive over the stripes while a pedestrian is crossing. (2) Fail to slow down for a pedestrian waiting at the beacons.

## 20. `GB.MWY.KEEP_LEFT` — Keep left on motorways
- **Highway Code:** Rule 264 (`https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273`, checked 2026-11-27)
- **Rule:** Always drive in the left-hand motorway lane unless overtaking.
- **Game detection:** Player lane index (`laneIndex > 0`) on `roadClass == "motorway"` with `hardShoulder == true` when no slower vehicle is ahead within 60 m (`duration > 10 s`). No new inputs needed.
- **Fault & severity:** Driving (`Serious` if repeated).
- **Pass cases:** (1) Cruise in lane 0 (left) at 70 mph. (2) Move to lane 1 to pass a 56 mph lorry, then return to lane 0.
- **Fail cases:** (1)Hog lane 1 (middle lane) with lane 0 empty for 20 s. (2) Stay in lane 2 after overtaking a single car.
