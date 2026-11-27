# Next 10 Rules Specification Pack (Batch 3 — Test-Pass Learner Essentials)

**Checked against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Severity mapping:** Matches `content/countries/gb/test_marking.yaml` (`Driving` = minor fault, max 15; `Serious` = fail; `Dangerous` = fail). Numeric judgement-call thresholds not fixed by statute are marked **`PROPOSED`**.

## Scope
Batch 3 covers the 10 highest-value remaining unimplemented rules for a UK practical driving test learner (verified against `core/Rules.Core/*.cs` on 2026-11-27; none are in Batch 1 or Batch 2).

---

## 1. `GB.SIG.MISLEADING` — Avoid misleading or cancelled-late signals
- **Highway Code:** Rule 103 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Cancel indicators promptly after a manoeuvre and do not signal when it could mislead another road user.
- **Game detection & thresholds:** On `links[]` after completing a turn or lane change (`> 40.0 m` or `> 5.0 s` (`PROPOSED`) past junction/lane completion): flag if `PlayerState.indicator != "off"`, OR if player signals past an earlier side road within `20.0 m` (`PROPOSED`) when continuing straight.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if a waiting `TrafficAgent` pulls out because of the misleading signal (`other_decel_req > 2.5 m/s²` or `ttc < 2.5 s`, `PROPOSED`).
- **Pass cases:** (1) Cancel the left indicator within `2.0 s` after turning into a side road. (2) Wait until passing an earlier side road before signalling left for the next turn.
- **Fail cases:** (1) Leave the right indicator flashing for `10.0 s` (`120.0 m`) after a roundabout exit. (2) Signal left `15.0 m` before an earlier side road while driving straight past it.

## 2. `GB.SIG.HORN` — Proper horn use only
- **Highway Code:** Rule 112 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Use the horn only while moving to warn other road users of your presence; never sound it aggressively or when stationary (except to warn of danger), and not in built-up areas between 11:30 pm and 7:00 am.
- **Game detection & thresholds:** Monitor `PlayerState.hornActive`: flag if horn is sounded while `speedMps < 0.5` with no `ttc < 2.0 s` (`PROPOSED`) hazard, or held `> 1.5 s` (`PROPOSED`) in a queue.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if used aggressively at a pedestrian/cyclist (`< 5.0 m`, `PROPOSED`) at a crossing.
- **Pass cases:** (1) Tap the horn briefly (`0.5 s`) while moving when a car begins pulling out blindly (`ttc = 2.5 s`). (2) Wait patiently without sounding the horn behind a learner or cyclist.
- **Fail cases:** (1) Sound the horn for `2.0 s` while stopped in a traffic-light queue. (2) Blast the horn at a pedestrian waiting on a zebra crossing.

## 3. `GB.SPD.PROGRESS` — Make reasonable progress without undue hesitation
- **Highway Code:** Rules 125 & 144 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Drive at an appropriate pace for the road and conditions without holding up traffic unnecessarily or hesitating at clear junctions.
- **Game detection & thresholds:** On clear `links[]` (`no TrafficAgent within 40.0 m ahead`, `visibilityM > 150.0`, no warning signs within `100.0 m`): flag if `PlayerState.speedMph < 0.65 * links[].speedLimitMph` (`PROPOSED`, e.g. `< 19.5 mph` in 30 mph, `< 26 mph` in 40 mph) for `> 12.0 s` (`PROPOSED`), or if stopped `> 6.0 s` (`PROPOSED`) at an empty give-way/roundabout with `> 6.0 s` clear gap.
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if repeated 3+ times (`PROPOSED`) or causes a queue of 3+ `TrafficAgent` vehicles behind.
- **Pass cases:** (1) Hold 27–29 mph on a clear 30 mph town road. (2) Enter a roundabout promptly when the right approach is clear for `> 5.0 s`.
- **Fail cases:** (1) Cruise at `15 mph` for `20.0 s` on a clear 40 mph dual carriageway. (2) Sit stopped at an empty roundabout for `8.0 s` with no traffic approaching.

## 4. `GB.JCT.DUAL_CROSS` — Crossing a dual carriageway central reservation
- **Highway Code:** Rule 173 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** Before crossing or turning right across a dual carriageway, check whether the central reservation is wide enough to protect your vehicle; if it is narrow, wait until both carriageways are clear.
- **Game detection & thresholds:** At `nodes[]` crossing a dual carriageway (`twoWay == false`, `centralReserveWidthM < 5.0 m`, `PROPOSED`): flag if player enters the first carriageway while the second carriageway has oncoming `TrafficAgent` with `ttc < 4.0 s` (`PROPOSED`), leaving the car protruding (`> 0.5 m`, `PROPOSED`) into a live lane.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if overhanging traffic forces `other_decel_req > 3.5 m/s²` (`PROPOSED`).
- **Pass cases:** (1) Wait at the give-way line until both carriageways have `> 5.0 s` clear gaps, then cross in one smooth move. (2) Pause in a wide (`>= 6.0 m`) central reservation only when your vehicle fits completely clear.
- **Fail cases:** (1) Cross the first carriageway and stop across a narrow `2.5 m` gap with the rear protruding into lane 1. (2) Pull into the gap forcing a 50 mph car in the second carriageway to brake hard.

## 5. `GB.XNG.LIGHT_CROSSINGS` — Stop on red at pelican, puffin, and toucan crossings
- **Highway Code:** Rule 196 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27)
- **Rule:** You MUST stop behind the white line when signal-controlled pedestrian/cycle crossings (pelican, puffin, toucan) show a red light.
- **Game detection & thresholds:** At `crossings[]` (`kind in {"pelican", "puffin", "toucan"}`) during `RED` phase: flag if player's front bumper crosses the crossing's `stopLine` (`> 0.0 m`).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if a pedestrian or cyclist is on the crossing (`clearance < 2.0 m`, `PROPOSED`).
- **Pass cases:** (1) Stop behind the white line on red and wait for green (or clear flashing amber at a pelican). (2) Leave the crossing clear if traffic queues beyond it.
- **Fail cases:** (1) Drive across a puffin crossing while the red signal is showing. (2) Creep forward onto the crossing studs before green.

## 6. `GB.XNG.SCHOOL_PATROL` — Stop for a school crossing patrol
- **Highway Code:** Rule 210 (`https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225`, checked 2026-11-27)
- **Rule:** You MUST stop when a school crossing patrol shows a red-and-yellow 'Stop — Children' sign and wait until they lower it.
- **Game detection & thresholds:** When a school crossing patrol agent is active (`patrolActive == true`) at `crossings[]` or `school_zone`: flag if player passes the patrol line (`speedMps > 0.2`) before `patrolActive` returns to `false`.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if children/pedestrians are in the carriageway (`clearance < 3.0 m`, `PROPOSED`).
- **Pass cases:** (1) Stop smoothly `3.0 m` before the patrol when the lollipop sign is raised and wait until they step back. (2) Move off gently only after the crossing is clear.
- **Fail cases:** (1) Drive past a school crossing patrol while the Stop sign is held up. (2) Edge forward before the patrol has returned to the pavement.

## 7. `GB.VRU.PASS_HORSE` — Pass horses slowly and wide (max 10 mph, >= 2 m)
- **Highway Code:** Rule 215 (`https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225`, checked 2026-11-27)
- **Rule:** Pass horses at under 10 mph and leave at least 2 metres (6.5 feet) of space; be ready to stop if a rider signals.
- **Game detection & thresholds:** Passing a `TrafficAgent` (`kind == "horse"`) within `15.0 m` (`PROPOSED`): flag if `PlayerState.speedMph > 10.0` (exact Highway Code Rule 215 figure) or lateral clearance `< 2.0 m` (exact Rule 215 figure).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if passing at `> 20.0 mph` (`PROPOSED`) or clearance `< 1.0 m` (`PROPOSED`).
- **Pass cases:** (1) Slow to `8 mph` and leave `2.5 m` clearance when passing a horse on a rural lane. (2) Wait behind until a straight stretch gives `>= 2.0 m` room.
- **Fail cases:** (1) Pass a horse at `25 mph` with `2.2 m` space. (2) Squeeze past a horse at `9 mph` with only `1.1 m` clearance.

## 8. `GB.VRU.CYCLE_LANE_SOLID` — Do not enter mandatory solid-line cycle lanes
- **Highway Code:** Rule 140 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** You MUST NOT drive or park in a cycle lane marked by a solid white line during its hours of operation.
- **Game detection & thresholds:** On `links[]` with `cycleLane == "mandatory"` (solid white line): flag if player's vehicle footprint overlaps the cycle lane (`> 0.2 m`, `PROPOSED`) during active hours when not turning across a dashed gap.
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if a cyclist is inside the cycle lane within `20.0 m` (`PROPOSED`).
- **Pass cases:** (1) Keep wholly to the right of the solid white cycle-lane line along the street. (2) Cross only at a dashed turn-in gap after checking nearside mirror/blind spot.
- **Fail cases:** (1) Straddle the solid white line by `0.5 m` for `20.0 m` to pass a right-turning car. (2) Pull over and stop inside a mandatory cycle lane.

## 9. `GB.BUS.PULLING_OUT` — Give way to buses pulling out from stops
- **Highway Code:** Rule 223 (`https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225`, checked 2026-11-27)
- **Rule:** Give priority to buses, coaches, and trams when you can do so safely, especially when they signal to pull away from a stop.
- **Game detection & thresholds:** Approaching a `TrafficAgent` (`kind == "bus"`) at `bus_stops.json` with right indicator active (`bus.indicator == "right"`): flag if player accelerates past instead of yielding when safe (`stoppingDistance <= distanceToBus`, `PROPOSED`) and forces the bus to wait/brake (`bus_decel_req > 1.5 m/s²`, `PROPOSED`).
- **Base severity:** `Driving`
- **Escalates to:** `Serious` if squeezing past an already-moving bus (`clearance < 1.0 m`, `PROPOSED`) or forcing an emergency brake.
- **Pass cases:** (1) Ease off smoothly and let a signalling bus pull out from a lay-by. (2) Hold behind a bus at a stop until it moves off cleanly.
- **Fail cases:** (1) Speed up to overtake a bus that is already signalling right to pull out (`35 m` ahead). (2) Cut across the front of a pulling-out bus.

## 10. `GB.OVT.DOUBLE_WHITE` — Do not cross solid double white lines
- **Highway Code:** Rule 129 (`https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158`, checked 2026-11-27)
- **Rule:** Where the white line nearest to you is solid, you MUST NOT cross or straddle it unless entering a side road/premises or passing a stationary vehicle or cyclist/horse at `<= 10 mph`.
- **Game detection & thresholds:** On `links[]` with `centreLine == "double_solid"` (or `solid_nearside`): flag if player crosses the centreline (`lateralOffsetM > 0.1 m` across centreline) while overtaking a vehicle moving `> 10.0 mph` (exact Rule 129 exception threshold).
- **Base severity:** `Serious`
- **Escalates to:** `Dangerous` if an oncoming `TrafficAgent` is within `80.0 m` (`PROPOSED`) or `ttc < 3.0 s`.
- **Pass cases:** (1) Hold behind a `25 mph` tractor along a solid double white line until dashed markings resume. (2) Pass a `7 mph` cyclist cleanly only when clear and safe.
- **Fail cases:** (1) Overtake a `25 mph` van across a solid double white line. (2) Straddle the solid centreline around a blind rural bend.
