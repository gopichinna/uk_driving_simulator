# 02: UK Rules Catalogue (Great Britain, v0.1)

*Rule numbers and wording were checked against GOV.UK on **2026-11-25**. The latest entry in the Highway Code updates log was **22 October 2025**.*

## 1. Conventions

### 1.1 Sources (short codes used in the tables)
| Code | Section | URL |
|---|---|---|
| INTRO | Introduction (hierarchy of road users, H1–H3) | https://www.gov.uk/guidance/the-highway-code/introduction |
| DRV | Rules for drivers and motorcyclists (89–102) | https://www.gov.uk/guidance/the-highway-code/rules-for-drivers-and-motorcyclists-89-to-102 |
| GEN | General rules, techniques and advice (103–158) | https://www.gov.uk/guidance/the-highway-code/general-rules-techniques-and-advice-for-all-drivers-and-riders-103-to-158 |
| ROAD | Using the road (159–203) | https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203 |
| CARE | Road users requiring extra care (204–225) | https://www.gov.uk/guidance/the-highway-code/road-users-requiring-extra-care-204-to-225 |
| WX | Driving in adverse weather (226–237) | https://www.gov.uk/guidance/the-highway-code/driving-in-adverse-weather-conditions-226-to-237 |
| PARK | Waiting and parking (238–252) | https://www.gov.uk/guidance/the-highway-code/waiting-and-parking-238-to-252 |
| MWAY | Motorways (253–274) | https://www.gov.uk/guidance/the-highway-code/motorways-253-to-273 |
| BRK | Breakdowns and incidents (275–287) | https://www.gov.uk/guidance/the-highway-code/breakdowns-and-incidents-274-to-287 |
| WORKS | Road works, level crossings, tramways (288–307) | https://www.gov.uk/guidance/the-highway-code/road-works-level-crossings-and-tramways-288-to-307 |
| LIGHTS | Light signals controlling traffic | https://www.gov.uk/guidance/the-highway-code/light-signals-controlling-traffic |
| MARK | Road markings | https://www.gov.uk/guidance/the-highway-code/road-markings |
| SIGNS | Traffic signs (*not yet read*) | https://www.gov.uk/guidance/the-highway-code/traffic-signs |
| UPD | Updates log | https://www.gov.uk/guidance/the-highway-code/updates |
| SD-PDF | Typical stopping distances (PDF) | https://assets.publishing.service.gov.uk/media/65f97642aa9b760011fbda4e/the-highway-code-stopping-distances.pdf |

**Licence:** GOV.UK Highway Code pages carry the footer *"All content is available under the Open Government Licence v3.0, except where otherwise stated"*. Traffic-sign images are available under the OGL with a Crown-copyright statement ([gov.uk/traffic-sign-images](https://www.gov.uk/traffic-sign-images)). They must not be used as manufacturing templates. Our rule explanations are **our own plain-English paraphrases**, not copied text.

### 1.2 Fields
- **`rule_id`**: `GB.<AREA>.<NAME>`. The `GB` country pack covers England, Scotland and Wales; regional overlays are `GB-ENG`, `GB-SCT` and `GB-WLS`. Northern Ireland has its own Highway Code and gets its own pack later.
- **Force**: **MUST** means a legal requirement (the Code says MUST / MUST NOT). **should** means advice. The severity model and the coach's wording both depend on this: "the law says…" versus "the Highway Code advises…".
- **V (verified)**: ✅ means the rule number and content were seen on GOV.UK on 2026-11-25. ⚠️ means partly verified (number or detail not confirmed). ❌ means `UNVERIFIED`.
- **Severity** follows the three fault categories of the practical driving test:
  - **D** = driving fault (minor)
  - **S** = serious (potentially dangerous)
  - **X** = dangerous (actual danger)

  The form is `base → escalation`. Mapping thresholds are `ASSUMPTION`s until an ADI reviews them (doc 06, R3). We *model* the categories; we do not reproduce DVSA's marking guidance.
- **Phase**: **P** prototype candidate (the 8 core prototype rules are listed in doc 07; the other P rules are stretch goals and fall back to MVP), **M** MVP, **V1**, **L** later, **i** information only (taught, never detected).

### 1.3 What the simulation can detect
Each tick, the rules engine receives a `DriverFrame` (doc 04, section 6). These are the signals the *Detection* column refers to:

| Signal | Meaning |
|---|---|
| `speed`, `accel`, `jerk` | Ego vehicle kinematics |
| `lane_id`, `lateral_offset`, `footprint` | Position on the lane graph; footprint polygon for zone tests |
| `zone[*]` | Regulatory zones the car is in: speed limit, bus lane with schedule, cycle lane solid or broken, box junction, zig-zag, school, clearway, red route, works, motorway |
| `connector` | Lane-to-lane path taken through a junction (`near_turn`, `ahead`, `far_turn`, `u_turn`, plus the roundabout exit index) |
| `indicator`, `hazards`, `headlights`, `high_beam`, `fog_rear` | Driver controls |
| `obs_events` | Observation actions: `mirror_interior`, `mirror_offside`, `mirror_nearside`, `blindspot_offside`, `blindspot_nearside`, each time-stamped (doc 03, section 4) |
| `conflicts[*]` | For each priority conflict zone the ego enters: the other agent, who has priority, time-to-collision (TTC), and the other agent's required deceleration (`other_decel_req`) |
| `headway_s`, `gap_m` | Time and distance to the lead vehicle in the same lane |
| `lateral_clear_m` | Minimum side clearance while passing an agent (cyclist, pedestrian, horse) |
| `signal_state[group]` | Traffic-light and crossing phase at the ego's stop line |
| `sight_dist_m` | Forward visible road length from geometry and occluders |
| `env` | Time of day, visibility in metres, road surface (dry, wet, icy), region (ENG/SCT/WLS) |
| `script_events` | Scenario director cues (e.g. `child_step_out`, `ambulance_approach`, `breakdown_start`) |

## 2. Catalogue

### A. Observation, signalling, road position (OBS / SIG / POS)
| rule_id | Rule (plain English) | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.OBS.MOVE_OFF_CHECKS | Before moving off: check all mirrors, look round your blind spot, signal if it would help someone, and only go when safe | ROAD 159 · should · ✅ | Moving from parked to >2 mph without `mirror_offside` + `blindspot_offside` in the previous 5 s | D → S if a following vehicle must brake (`other_decel_req` > 3 m/s²) | "Traffic comes from behind on your right. Check over your right shoulder." | P |
| GB.OBS.MSM | Mirrors – Signal – Manoeuvre: check mirrors before signalling or changing speed or direction | ROAD 161 · should · ✅ | Indicator on, lane change, or braking > 2.5 m/s² with no relevant mirror check in the previous 3 s | D → S on conflict | Replay highlights the vehicle that was in your mirror | P |
| GB.OBS.LANE_CHANGE | Before changing lane: mirrors, a glance sideways, signal, then move when safe | GEN 133 · should · ✅ | Lane-boundary crossing without mirror + blind-spot check in 3 s, or signal < 1.5 s before crossing | D → S if another vehicle must brake; X if a collision is only avoided by AI emergency braking | Shows the car hidden in the blind spot | M |
| GB.SIG.TIMELY | Signal clearly and in good time; cancel afterwards; a signal does not give you priority | GEN 103 · should · ✅ | Signal onset < 2 s (or < 30 m) before a manoeuvre with road users present; indicator left on > 8 s after it | D | "Signal early enough for others to react." | P |
| GB.SIG.MISLEADING | Don't give misleading signals | GEN 103 · should · ✅ | Indicator direction ≠ executed connector; indicator left on near a junction and an AI vehicle emerges because of it | D → S if an AI emerges | Shows the side-road driver's view of your signal | M |
| GB.SIG.HEADLIGHT_FLASH | Flash headlights only to show you are there. Never assume someone's flash means "go" | GEN 110–111 · should · ✅ | Scripted: an AI flashes, and the player proceeds without an observation check | D → S on conflict | Big habit gap for persona P1 | V1 |
| GB.SIG.HORN | No horn while stationary, or in a built-up area 23:30–07:00, unless avoiding danger | GEN 112 · MUST NOT · ✅ | Horn use while stationary, or in a built-up area at night with no hazard present | D | Habit gap for persona P1 | V1 |
| GB.POS.KEEP_LEFT | Drive on the left unless signs or markings say otherwise | ROAD 160 · should · ⚠️ (number not seen) | Ego in an opposing lane outside an overtake or obstruction context | S → X if oncoming traffic is present | Core rule for persona P2 | P |
| GB.POS.AFTER_TURN | After turning, end up on the left side of the new road | derived from ROAD 160 · ⚠️ | After a turn, ego enters the offside half of the new road for > 1 s | S → X if oncoming traffic | Persona P2's most common mistake; replay is a bird's-eye diagram | P |
| GB.POS.LANE_FOR_ROUTE | Choose your lane early and follow lane arrows | GEN 133–134, MARK · should · ⚠️ | Connector taken ≠ lane-arrow permission; late lane change over a solid line or hatching | D → S if it causes a conflict | Shows the arrows you missed 100 m back | M |
| GB.POS.DUAL_KEEP_LEFT | On a 2-lane dual carriageway use the left lane; the right lane is for overtaking or turning right | GEN 137 · should · ✅ | In the right lane > 15 s with no slower vehicle within 150 m ahead in the left lane and no right turn within 500 m | D | "Overtake, then move back left." | M |
| GB.POS.THREE_LANE_SINGLE | On a 3-lane single carriageway, the middle lane is only for overtaking or turning right | GEN 135 · should · ✅ | In the middle lane > 10 s with no overtake or right turn | D → S if oncoming traffic is in the middle lane | | V1 |

### B. Speed (SPD)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.SPD.OVER_LIMIT | Never exceed the road's limit or your vehicle's limit | GEN 124 · MUST · ✅ | `speed > limit` for > 1.5 s (with hysteresis) | D (≤ +4 mph, < 3 s) → S (≥ +5 mph or sustained) → X (≥ +15 mph, or ≥ +10 mph in a 20 zone or with pedestrians present) `ASSUMPTION` | Shows the last sign you passed and its distance | P |
| GB.SPD.NATIONAL_LIMIT | National speed limit sign for cars: 60 mph single carriageway, 70 mph dual carriageway and motorway | GEN 124 · MUST · ✅ | Same detector; the country pack resolves the limit from road class and vehicle type | as OVER_LIMIT | Persona P2 doesn't know the white-circle-with-black-diagonal sign | M |
| GB.SPD.BUILT_UP | Built-up area: 30 mph in England and Scotland, **20 mph in Wales** (since 17 Sep 2023) | GEN 124, UPD · MUST · ✅ | Region overlay sets the default for `restricted_road` | as OVER_LIMIT | A regional difference worth teaching explicitly | M |
| GB.SPD.STREET_LIGHTING | Street lights with no other signs usually means the built-up limit applies | GEN 124 · ⚠️ (wording not confirmed) | Teaching via an in-drive prompt: "What's the limit here?" | i / D | Repeater-sign logic | M |
| GB.SPD.APPROPRIATE | The limit is a maximum, not a target. Slow down for bends, pedestrians, cyclists, weather and the dark | GEN 125 · should · ✅ | `speed` > safe speed, where stopping distance must fit inside `sight_dist_m`, adjusted for wet roads, pedestrians near the kerb and night | D → S | Shows a visibility cone against the stopping distance | M |
| GB.SPD.PROGRESS | Don't hold up traffic without reason (undue hesitation) | DVSA practical-test assessment · ❌ `UNVERIFIED` source URL | Speed < 60% of the limit for > 20 s on a clear road; waiting at a clear junction > 5 s | D | Balances the "too cautious" habits of persona P3 | M |
| GB.SPD.WORKS_TEMP | Obey temporary speed limits at roadworks | WORKS 288 · MUST · ✅ | OVER_LIMIT detector in a `works` zone | S | | M |
| GB.SPD.VARIABLE | Obey red-circle variable limits on gantries | MWAY 261 · MUST · ✅ | OVER_LIMIT detector with the limit taken from the active gantry state | S | Smart motorway lesson | M |

### C. Following distance and stopping (GAP)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.GAP.TWO_SECOND | Never closer than your stopping distance; at least 2 s on high-speed roads and in tunnels | GEN 126 · should · ✅ | Speed ≥ 40 mph and `headway_s` < 2.0 for > 3 s; in town, `headway_s` < 1.0 for > 3 s | D → S (< 1 s at ≥ 50 mph) | Live "2-second" timer overlay at the lead car's marker point | P |
| GB.GAP.WET_ICY | Double the gap in the wet; up to 10× on ice | GEN 126, WX 227/230 · should · ✅ | Threshold × 2 when `surface = wet`, × 5 (`ASSUMPTION`) when icy | D → S | | V1 |
| GB.GAP.STOPPING_TABLE | Typical stopping distances | GEN 126 + SD-PDF · i · ⚠️ | Learn card and a braking challenge. Values 12 / 23 / 36 / 53 / 73 / 96 m at 20–70 mph are `UNVERIFIED` until read from the PDF | i | | M |
| GB.GAP.CUT_IN | If someone cuts in ahead, drop back to a 2-second gap | ROAD 168 · should · ✅ | Scripted `car_cut_in`: headway not restored to ≥ 2 s within 6 s, or the player accelerates | D | | M |

### D. Junctions and priority (JCT)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.JCT.GIVE_WAY | At broken double white lines, give way to traffic on the major road | ROAD 172 · MUST · ✅ | Entering a conflict zone where a priority agent's TTC < 3 s | S if `other_decel_req` > 3 m/s² → X if > 6 m/s² or contact | Replay from the other driver's point of view | P |
| GB.JCT.STOP_SIGN | At a Stop sign, stop completely at the solid line | ROAD 171 · MUST · ✅ | Minimum speed in the last 10 m before the line > 0.5 mph | S | "Stop means wheels stopped, even if it's clear." | M |
| GB.JCT.H2_PEDESTRIANS | Give way to pedestrians crossing, or waiting to cross, a road you turn into or out of | INTRO H2, ROAD 170 · should · ✅ | Pedestrian agent with `intent=cross` at the junction mouth while the ego passes | D → S if the pedestrian must stop or step back → X if clearance < 1 m at > 10 mph | 2022 change; big gap for personas P1, P2 and P4 | P |
| GB.JCT.H3_CYCLISTS | Don't cut across cyclists, horse riders or horse-drawn vehicles going ahead when you turn or change lane | INTRO H3 · should · ✅ | Near turn while a cyclist going ahead would reach the conflict within 3 s | S → X if the cyclist brakes or swerves | Scripted "left hook" scenario | M |
| GB.JCT.BOX | Don't enter a yellow box unless your exit is clear (except a right turn blocked only by oncoming or right-turning traffic) | ROAD 174 · MUST · ✅ | Ego stationary inside the box polygon and the blocker isn't an oncoming or right-turning vehicle on a far-turn connector | S | Bird's-eye view shows the queue that trapped you | M |
| GB.JCT.TURN_FAR_POSITION | Right turn: mirrors, signal, position just left of the centre line, wait for a safe gap | ROAD 179–180 · should · ✅ | Lateral position and signal timing at the junction mouth | D | Country pack maps the far turn to **right** in the UK | M |
| GB.JCT.TURN_FAR_GAP | Turning right, only go when there is a safe gap in oncoming traffic | ROAD 180 · should · ✅ | Same conflict logic as GIVE_WAY | S → X | | P |
| GB.JCT.TURN_NEAR | Left turn: keep to the left; give way to cycle, bus and tram lanes you cross | ROAD 182–183 · should · ✅ | Wide swing (offset > 1 m into another lane); conflict with a cycle or bus lane user | D → S | | M |
| GB.JCT.EMERGE_LOOK | Look all around before emerging; slow down or stop where sightlines are limited | ROAD 170 · should · ✅ | Emerging at > 10 mph where `sight_dist_m` < required | D → S | | M |
| GB.JCT.OPPOSITE_RIGHT | Two opposite vehicles both turning right: usually pass offside to offside | ROAD 181 · should · ⚠️ | Path-crossing pattern at a crossroads | D | | V1 |
| GB.JCT.UNMARKED | At unmarked junctions and broken traffic lights, nobody has priority | ROAD 176 (broken lights) · ⚠️ | Approach speed > 15 mph at an unmarked crossroads | D → S | | V1 |
| GB.JCT.DUAL_CROSS | Crossing a dual carriageway: if the central gap fits your whole car, treat it as two roads; otherwise cross in one go | ROAD 173 · should · ✅ | Stopping in the central reservation when the gap is shorter than the car | S | | V1 |

### E. Traffic lights (TL)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.TL.RED | Stop behind the white stop line on red | ROAD 175–176, LIGHTS · MUST · ✅ | Front bumper crosses the line during red | S → X if a crossing agent or pedestrian is present | | P |
| GB.TL.AMBER | Amber means stop, unless you've crossed the line or are too close to stop safely | ROAD 175, LIGHTS · MUST · ✅ | At amber onset, the car could stop at 3 m/s² and didn't; *or* braked > 6 m/s² with a close follower | D | "Point of no return" visual | P |
| GB.TL.RED_AMBER | Red and amber together still mean stop; wait for green | LIGHTS · MUST · ✅ | Crossing the line before green | S | | M |
| GB.TL.NO_TURN_ON_RED | There is no turning on red in the UK | derived from ROAD 176 · MUST · ✅ | Near-turn crossing on red | S | Specific to persona P2 (US) | M |
| GB.TL.CLEAR_JUNCTION | On green, only go if you can clear the junction | ROAD 176 · MUST · ✅ | Stationary inside the junction conflict area while blocking a crossing phase | D → S | | M |
| GB.TL.FILTER_ARROW | A green filter arrow lets you go only in the arrow's direction | ROAD 177, LIGHTS · MUST · ✅ | Connector ≠ arrow direction during the filter phase | S | | M |
| GB.TL.ASL | Advanced stop line: stop at the first line on amber or red; if you crossed it as red came on, stop at the second | ROAD 178 · MUST · ✅ | Ego entered the ASL box after red onset | S | Explains why cyclists get the box | M |

### F. Roundabouts (RBT)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.RBT.GIVE_WAY_RIGHT | Give priority to traffic coming from your right (offside), unless signs, markings or lights say otherwise | ROAD 185 (+172 lines) · MUST at give-way lines · ✅ | Entry while a circulating agent from the offside has TTC < 3 s to the entry conflict | S → X if `other_decel_req` > 6 m/s² | Clockwise flow animation for persona P2 | P |
| GB.RBT.APPROACH_LANE | Left exit: left lane. Right or full circle: right lane. Middle exits: the lane that fits; follow lane markings | ROAD 186 · should · ✅ | Approach lane at the give-way line vs exit index, with lane arrows overriding | D → S if a late lane change conflicts | "Clock face" overlay: exits mapped to 12 / 3 / 6 / 9 | P |
| GB.RBT.SIGNAL_APPROACH | Signal left for a left exit, right for exits past 12 o'clock or full circle; usually no signal for straight ahead | ROAD 186 · should · ✅ | Indicator state in the last 50 m before the give-way line | D | | P |
| GB.RBT.SIGNAL_EXIT | Signal left after passing the exit before yours | ROAD 186 · should · ✅ | Left indicator from passing the previous exit until taking yours | D | | P |
| GB.RBT.STAY_IN_LANE | On multi-lane and spiral roundabouts, keep in your lane and follow the markings | ROAD 186, MARK · should · ⚠️ | Lane-boundary crossing on the circulatory road without a check, or with conflict | S → X | Spiral roundabout lesson | M |
| GB.RBT.CYCLISTS | Give priority to cyclists on the roundabout; they may stay left while going right | ROAD 186–187 · should · ✅ | Cutting across a cyclist at an exit | S → X | Scripted cyclist | M |
| GB.RBT.LONG_VEHICLES | Long vehicles may take a different course; give them room | ROAD 187 · should · ✅ | Entering beside an articulated lorry on the circulatory road | D → S | | V1 |
| GB.RBT.MINI | Drive around the central marking of a mini-roundabout (unless your vehicle is too big); avoid U-turns | ROAD 188 · MUST · ✅ | Wheel path over the central disc polygon | S | | M |
| GB.RBT.DOUBLE_MINI | Double or multiple mini-roundabouts: treat each separately and give way to the right at each | ROAD 189–190 · should · ✅ | GIVE_WAY_RIGHT applied per node | S | | M |

### G. Pedestrian crossings (XNG)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.XNG.ZEBRA | Give way to people waiting at a zebra; you **must** give way once someone has stepped on | ROAD 195, INTRO H2 · MUST (on crossing) · ✅ | Pedestrian on the crossing polygon when the ego enters → MUST breach. Pedestrian waiting and ego doesn't slow → should breach | D (waiting) / S → X (on crossing) | | P |
| GB.XNG.PARALLEL | A parallel crossing (zebra plus cycle track): also give way to cyclists | ROAD 195 · MUST · ✅ | As ZEBRA, with cyclist agents | S | | V1 |
| GB.XNG.PELICAN_AMBER | Pelican crossing with flashing amber: give way to anyone on it; go only if it's clear | ROAD 196 · MUST · ✅ | Entering during flashing amber with a pedestrian on the crossing | S → X | | M |
| GB.XNG.PELICAN_ISLAND | A straight pelican with a central island is one crossing | ROAD 197 · ✅ | As PELICAN_AMBER across both halves | S | | V1 |
| GB.XNG.LIGHT_CROSSINGS | Puffin, toucan and equestrian ("pegasus") crossings have no flashing amber; follow the lights | ROAD 199 · MUST · ✅ | TL.RED detector on crossing signals | S | The Code says "equestrian"; "pegasus" is common usage | M |
| GB.XNG.ZIGZAG_OVERTAKE | In zig-zag areas, don't overtake the moving vehicle nearest the crossing, or one stopped to give way | ROAD 191 · MUST NOT · ✅ | Overtake event inside the zig-zag polygon | S | | M |
| GB.XNG.KEEP_CLEAR | Don't stop on a crossing in queuing traffic | ROAD 192 · should · ✅ | Stationary with the footprint overlapping the crossing | D → S | | M |
| GB.XNG.SCHOOL_PATROL | Stop when a school crossing patrol shows "Stop for children" | GEN 105, CARE 210 · MUST · ✅ | Passing the patrol's stop point while the sign is raised | S → X | | M |

### H. Cyclists, horses, pedestrians in the road (VRU)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.VRU.HIERARCHY | Hierarchy of road users: those who can do the most harm have the most responsibility | INTRO H1 · should · ✅ | No detector; the coach uses it as a framing line in debriefs | i | | M |
| GB.VRU.PASS_CYCLIST | Leave at least 1.5 m when overtaking cyclists at up to 30 mph, more at higher speeds | ROAD 163 · should · ✅ | `lateral_clear_m` < 1.5 (≤ 30 mph) or < 2.0 (> 30 mph, `ASSUMPTION`) | S → X (< 1.0 m) | Side-on replay with a distance ruler | P |
| GB.VRU.PASS_HORSE | Horses: slow to 10 mph or less and leave at least 2 m | ROAD 163, CARE 215 · should · ✅ | Speed > 10 mph or clearance < 2 m while passing | S → X | | V1 |
| GB.VRU.PASS_PEDESTRIAN | Leave 2 m when passing pedestrians walking in the road | ROAD 163 · should · ✅ | `lateral_clear_m` < 2 m | D → S | Country lane lesson | V1 |
| GB.VRU.CYCLE_LANE_SOLID | Don't drive or park in a cycle lane marked with a solid white line during its hours ("mandatory") | GEN 140 · MUST NOT · ✅ | > 50% of the footprint inside the lane polygon in operating hours, other than a legal crossing | S | The Code doesn't say "mandatory"; it describes the solid line | M |
| GB.VRU.CYCLE_LANE_BROKEN | Broken-line ("advisory") cycle lane: don't drive or park in it unless it's unavoidable | GEN 140 · should · ✅ | Same, when no obstruction makes it necessary | D | | M |
| GB.VRU.CHILDREN | Watch for children, older and disabled people; at 20 mph a pedestrian has about a 1-in-20 chance of death if hit | CARE 204–207 · should · ⚠️ (207 ✅) | Scripted hazards (see School area) | via SCH rules | | M |
| GB.VRU.DUTCH_REACH | Open your door with the hand furthest from it, and check for cyclists | PARK 239 · ❌ `UNVERIFIED` number | Post-parking choice prompt | i | | L |

### I. Bus lanes and buses (BUS)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.BUS.LANE_HOURS | Don't drive in a bus lane during its operating times unless signs allow it; you may enter to stop, load or unload where permitted | GEN 141 · should (+ signed TRO) · ✅ | Ego centre in a bus-lane polygon > 20 m during the schedule, excluding a permitted near-turn entry | S | Sign-reading drill: the time plate decides | M |
| GB.BUS.ONLY_ROADS | "Buses and cycles only" signs (the "only" plate is no longer needed, Oct 2025) | UPD 2025-10-22, SIGNS · MUST · ✅ (update) / ⚠️ (sign text) | Entering a bus-gate link | S | | V1 |
| GB.BUS.PULLING_OUT | Give priority to buses signalling to pull out from stops, when it's safe | CARE 223 · should · ⚠️ | Scripted bus indicates; ego closes the gap | D | | V1 |

### J. School areas (SCH)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.SCH.SLOW | Drive slowly near schools; flashing amber under a school sign means children may be crossing, so drive very slowly | CARE 208 · should · ✅ | Active school zone with lights flashing and speed > 20 mph (`ASSUMPTION` threshold) | D → S if children are present | | M |
| GB.SCH.KEEP_CLEAR | Don't stop or park on "school keep clear" markings when an upright sign prohibits it | PARK 238 · MUST NOT · ✅ | Stationary > 3 s in the marking polygon | S | | M |
| GB.SCH.ENTRANCE_PARK | Don't park near a school entrance | PARK 243 · should · ✅ | Park action within 15 m (`ASSUMPTION`) of an entrance | D | | V1 |
| GB.SCH.CHILD_STEPS_OUT | Anticipate a child stepping out between parked cars | CARE 206–207 · should · ⚠️ | Scripted `child_step_out`: speed when the hazard appears vs safe speed, reaction time, and outcome | S (late, but stopped) → X (contact) | Our own **hazard-anticipation** scenario (not a DVSA clip) | P |
| GB.SCH.SCHOOL_BUS | Pass a stationary school bus slowly and carefully | CARE 209 · should · ✅ | Speed > 15 mph while passing (`ASSUMPTION`) | D | | V1 |

### K. Emergency vehicles (EMG)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.EMG.LET_PASS | Let emergency vehicles pass, **without breaking other rules**: don't run red lights, use bus lanes or mount kerbs to do it; avoid stopping on a brow or bend; no harsh braking at junctions | CARE 219 · should · ✅ | Scripted `ambulance_approach`: fails to make room within 10 s when a safe option exists → D. Breaks another rule to make room → that rule fires and is tagged "emergency vehicle" | D → S | Very common confusion for persona P1 | M |
| GB.EMG.AMBER_BEACON | Flashing amber beacons mean traffic officers or slow vehicles | CARE 219 · ✅ | Info card | i | | V1 |

### L. Overtaking (OVT)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.OVT.UNSAFE_PLACE | Don't overtake where you can't see far enough (bends, hill brows, hump bridges), or near junctions, crossings or level crossings | ROAD 166–167 · should · ✅ | Overtake started with `sight_dist_m` < required overtaking distance, or a junction or crossing within 50 m | S → X if oncoming traffic must brake | | M |
| GB.OVT.DOUBLE_WHITE | Don't cross double white lines where the line nearest you is solid | ROAD 165 · MUST NOT · ✅ (exceptions, e.g. passing a slow cyclist at ≤ 10 mph, ⚠️ rule 129 not read) | Crossing the line polygon outside the exception conditions | S | | M |
| GB.OVT.NO_OVERTAKING | Don't overtake after a No Overtaking sign until it's cancelled | ROAD 165 · MUST NOT · ✅ | Overtake event inside the zone | S | | V1 |
| GB.OVT.INTO_LANE | Don't overtake into a bus, tram or cycle lane while it's in operation | ROAD 165 · MUST NOT · ✅ | Overtake path through an active lane polygon | S | | M |

### M. Motorways (MWY)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.MWY.LEARNERS | Learners may use motorways only with an ADI in a dual-control car | MWAY 253 · MUST · ✅ | Curriculum framing | i | | M |
| GB.MWY.JOIN | Give way to motorway traffic; match speed on the slip road; don't cross solid white lines or use the hard shoulder to join | MWAY 259 · should/MUST · ✅ | Merging at > 20 mph below lane-1 traffic speed → D. Merge causes a brake → S. Crossing a solid line → S. Stopping at the end of the slip when gaps were available → D | D → S | Slip-road acceleration drill | P |
| GB.MWY.KEEP_LEFT | Keep in the left lane unless overtaking | MWAY 264 · should · ✅ | In lane ≥ 2 for > 20 s while lane 1 is clear for 300 m ahead | D (repeated → S) | "Middle-lane hogging" | M |
| GB.MWY.OVERTAKE_RIGHT | Overtake only on the right, and move back left after | MWAY 267 · should · ✅ | Overtake via the nearside | S | | M |
| GB.MWY.NO_UNDERTAKE | Don't overtake on the left, except in congested queues | MWAY 268 · should · ✅ | Passing on the nearside at > 30 mph, not in congestion | S | | M |
| GB.MWY.RED_X | Don't drive in a lane closed by a red X; move to an open lane; if all lanes are closed, stop before the sign | MWAY 258 · MUST NOT · ✅ | Passing a gantry with a red X over the ego's lane, or staying in that lane | S → X if a hazard is in the lane | Camera enforcement, £100 + 3 points (GOV.UK press release; `UNVERIFIED` current amount) | P |
| GB.MWY.AMBER_FLASH | Amber flashing lights: slow down until past a non-flashing signal, national limit sign or END | MWAY 257 · should · ✅ | Speed not reduced by ≥ 10% within 300 m | D | | M |
| GB.MWY.HARD_SHOULDER | Don't use the hard shoulder except in an emergency or when directed; it's a running lane only when a speed limit is shown above it | MWAY 269 · MUST NOT · ✅ | Driving on the hard shoulder without an active gantry limit over it | S | Dynamic hard-shoulder lesson | M |
| GB.MWY.EMERGENCY_AREA | Emergency areas (blue sign, orange SOS) are only for emergencies | MWAY 270–271 · MUST · ✅ | Stopping in an emergency area outside a breakdown script | S | 150+ extra emergency areas completed by March 2025 (National Highways) | M |
| GB.MWY.NO_STOP | Don't stop on the carriageway or hard shoulder except in an emergency | MWAY 271 · MUST NOT · ✅ | Speed < 5 mph in a running lane without congestion or a red X | X | | M |
| GB.MWY.NO_REVERSE | Never reverse, cross the central reservation or drive against traffic; if you miss your exit, go to the next one | MWAY 263 · MUST NOT · ✅ | Negative speed or a wrong-way heading on motorway links | X | | M |
| GB.MWY.LEAVE | Leave by the left slip road, signal in good time, and check your speed on the slip | MWAY 273–274 · should · ✅ | Indicator onset < 300 yd marker (`ASSUMPTION`); slip-road speed above the advisory | D | | M |
| GB.MWY.RIGHT_LANE_LIMITS | No towing or heavy vehicles in the right lane of 3+ lanes | MWAY 265 · MUST · ✅ | Vehicle-class constraint | — | Vans and towing pack | L |
| GB.MWY.SMART_TYPES | Context card: all-lane running, dynamic hard shoulder, controlled motorways; new smart motorway schemes were cancelled in 2023 | MWAY 258/261/269/270; the Code doesn't use the term "smart motorway" · ❌ `UNVERIFIED` (2023 cancellation not checked this session) | Info card | i | | M |

### N. Breakdowns (BRK)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.BRK.GO_LEFT | **Go left, get safe, get help**: hazard lights on, move to the left, reach an emergency area or hard shoulder if you can, stop far left, leave by the nearside door, get behind the barrier; if you can't, stay in with your belt on and call 999 | BRK 277 · should · ✅ | Scripted `breakdown_start`: hazards within 3 s; reached a refuge if reachable; stopped far left; post-stop choice UI | S (no hazards) → X (stopped in a live lane when a refuge was reachable) | Signature scenario for the MVP motorway module | M |
| GB.BRK.NO_TRIANGLE_MWY | Never put a warning triangle on a motorway | BRK 276–277 · ✅ | Choice in the post-stop UI | S | Differs from home habits in many countries | M |
| GB.BRK.REJOIN | From the hard shoulder, build speed and find a gap. From an emergency area, you **must** use the emergency phone so a lane can be closed | BRK 278 · MUST (emergency area phone) · ✅ | Rejoin flow and speed delta; phone-step choice | S | | M |
| GB.BRK.SAFE_PLACE | Stop somewhere relatively safe; on a motorway, services are best | BRK 275 · should · ✅ | Learn card | i | | M |
| GB.BRK.NO_OBSTRUCTION | On a motorway, don't try to remove an obstruction; call 999 | BRK 280 · should · ✅ | Choice UI | i | | L |

### O. Roadworks (WRK)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.WRK.NO_LANE_JUMP | Don't switch lanes to jump queues; never drive through a coned-off area | WORKS 288 · should · ✅ | Lane change to overtake a queue in a works zone; footprint inside a coned polygon | D → S | | M |
| GB.WRK.MERGE_IN_TURN | Merge in turn when lanes close at very low speed | GEN 134 · should · ✅ | In a queue (< 20 mph), not letting an alternate merger in, or forcing in | D | | M |
| GB.WRK.CONTRAFLOW | Keep your distance and slow down in narrow lanes and contraflows | WORKS 290 · should · ✅ | Headway and speed in a contraflow zone | D | | V1 |
| GB.WRK.WORKS_VEHICLE | Pass a works vehicle on the side it shows ("Keep Left/Right"); never overtake a "Convoy vehicle – no overtaking" | WORKS 289 · MUST · ✅ | Pass side ≠ shown side; overtaking a convoy vehicle | S | | V1 |

### P. Parking and waiting (PRK)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.PRK.YELLOW_LINES | No waiting or parking on yellow lines during the times shown; double yellow means no waiting at any time | PARK 238, MARK · MUST NOT · ✅ | Park action (stationary + park state) in the polygon during the restriction | D (in sim) | Kerb-mark and time-plate drill | M |
| GB.PRK.RED_ROUTE | Red routes: double red means no stopping at any time; single red has signed times | PARK 240, MARK · MUST NOT · ✅ | Stationary > 3 s outside a marked bay | S | | V1 |
| GB.PRK.CLEARWAY | No stopping on a clearway; on an urban clearway in its hours, only to set down or pick up | PARK 240 · MUST NOT · ✅ | Stationary > 3 s | S | | M |
| GB.PRK.JUNCTION_10M | Don't park within 10 m of a junction (unless in an authorised space), at a bus stop, or on a bend | PARK 243 · should · ✅ | Park action within 10 m of a junction node | D | | M |
| GB.PRK.NIGHT_FACING | At night, don't park facing against the traffic flow unless in a recognised space | PARK 248 · MUST NOT · ✅ | Park heading against the lane direction at night | D | | V1 |
| GB.PRK.SCOTLAND_PAVEMENT | **Scotland**: no parking on pavements or at dropped kerbs | PARK 243–244, UPD 2025-04-10 · MUST NOT · ✅ | Region overlay `GB-SCT`; footprint over the footway polygon | D | A regional difference | V1 |
| GB.PRK.BAYS | Permit, disabled, loading and pay-and-display bays follow their signs | SIGNS · ❌ `UNVERIFIED` | Sign-reading quiz | i | | V1 |

### Q. Lights and weather (WX)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.WX.HEADLIGHTS_NIGHT | Use headlights at night (30 min after sunset to 30 min before sunrise) except on lit roads; always use sidelights | GEN 113 · MUST · ✅ | `env.night` and headlights off on an unlit road | S | | M |
| GB.WX.LOW_VISIBILITY | Use headlights when visibility is seriously reduced (generally < 100 m) | WX 226 · MUST · ✅ | `env.visibility` < 100 m and headlights off | S | | V1 |
| GB.WX.FOG_LIGHTS | Use fog lights only when visibility is seriously reduced; switch them off when it improves | WX 236 · MUST NOT · ✅ | Fog light on with visibility > 100 m for > 20 s (`ASSUMPTION`) | D | | V1 |
| GB.WX.NO_DAZZLE | Don't dazzle others; dip your lights | GEN 114 · MUST NOT · ✅ | High beam with oncoming traffic < 150 m, or a lead car < 100 m | D | | M |

### R. Rural and rare (RUR)
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.RUR.SINGLE_TRACK | Pull into a passing place on your left, or wait opposite one on your right; give way to uphill traffic; never park in passing places | GEN 155–156 · should · ✅ | Scripted oncoming car; who reverses and who waits | D → S | | V1 |
| GB.RUR.COUNTRY_BENDS | Slow down for bends, junctions and village entries on country roads | GEN 154 · should · ✅ | APPROPRIATE speed detector in a rural context | D | | V1 |
| GB.RUR.LEVEL_CROSSING | Never go onto a level crossing until the far side is clear; stop at the white line when red lights flash; never zig-zag round half barriers | WORKS 291, 293 · MUST · ✅ | Passing the stop line during the flashing phase; stopping on the crossing | X | | V1 |
| GB.RUR.LC_BREAKDOWN | Breakdown on a level crossing: get everyone out, then use the railway phone | WORKS 299 · should · ✅ | Choice UI | i | | L |

### S. Driver conduct (DRV), mostly information
| rule_id | Rule | Source · Force · V | Detection | Severity | Teaching moment | Ph |
|---|---|---|---|---|---|---|
| GB.DRV.PHONE | Never use a hand-held phone while driving, including when stopped in traffic (exceptions: 999 calls when you can't stop; contactless payment while stationary) | GEN 149 · MUST NOT · ✅ | Scripted "phone buzzes" prompt; choosing to pick it up | X | Penalty amount `UNVERIFIED` this session | M |
| GB.DRV.SEATBELT | Wear a seat belt; the driver is responsible for passengers under 14 | DRV 99–100 · MUST · ✅ | Pre-drive check; moving off unbelted | S | | M |
| GB.DRV.DRINK_DRUGS | Alcohol limits are lower in Scotland (22 µg/100 ml breath) than in England and Wales (35 µg) | DRV 95–96 · MUST NOT · ✅ | Info card | i | | M |
| GB.DRV.FATIGUE | Take at least a 15-minute break every 2 hours; never rest on the hard shoulder | DRV 91 · should · ✅ | Info card | i | | V1 |
| GB.DRV.ASSIST_SYSTEMS | Driver-assist systems don't reduce your responsibility | GEN 150 · MUST · ✅ | Info card | i | | L |

### T. Zones and charges (ZON), information only
| rule_id | Topic | Source · V | Ph |
|---|---|---|---|
| GB.ZON.ULEZ | London Ultra Low Emission Zone | TfL · ❌ `UNVERIFIED` | L |
| GB.ZON.CONGESTION | London Congestion Charge | TfL · ❌ `UNVERIFIED` | L |
| GB.ZON.CAZ | Clean Air Zones in other English cities | GOV.UK · ❌ `UNVERIFIED` | L |
| GB.ZON.TOLLS | Dartford Crossing (Dart Charge), M6 Toll | GOV.UK / operator · ❌ `UNVERIFIED` | L |

**Totals:** 129 entries. 112 are detected in the simulation; 17 are information only (`i` severity plus the 4 rows in table T). Prototype: 21 entries. MVP (prototype + M): 90 entries, of which 84 are detected in the simulation.

## 3. Rule-pack representation (preview; full detail in doc 04, section 6)
```yaml
- id: GB.RBT.GIVE_WAY_RIGHT
  kind: fail_to_give_way          # parameterised rule kind (implemented in C#)
  applies_to: { node_tags: [roundabout_entry] }
  params:
    priority_from: offside        # resolved to "right" by the GB pack (left-hand traffic)
    ttc_threshold_s: 3.0
    serious_decel_mps2: 3.0
    dangerous_decel_mps2: 6.0
  force: MUST
  severity: { base: S, escalate: [{ when: "other_decel_req > 6", to: X }] }
  source: { doc: highway-code, rule: "185", url: "https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203", verified: 2026-11-25 }
  teach: { card: rbt_priority, replay_camera: other_driver_pov }
  fixtures: [rbt_entry_gap_ok, rbt_entry_forced_brake, rbt_entry_contact]
```

## 4. Coverage analysis: reaching ~80% of daily-commute situations

**Method.** We break a typical mixed commute (residential streets → urban main road → roundabouts → dual carriageway → motorway → town) into *situation families*. Each family gets a weight: **how often it occurs per commute × how much harm a mistake can do**. The weights below are `ASSUMPTION`s for planning. We will validate them in Phase 1 in three ways:
- a 10-person commute diary study, recorded with dashcam or voice notes;
- DfT traffic statistics by road class (`UNVERIFIED` figures not yet pulled);
- telemetry from beta free-drive: rule-evaluation counts per km.

| # | Situation family | Weight (%) | Covered by (MVP) |
|---|---|---|---|
| 1 | Speed limits and limit changes, including national, Wales 20, variable | 10 | SPD.* |
| 2 | Observation and signalling (cuts across everything) | 8 | OBS.*, SIG.* |
| 3 | Lane discipline and road position (urban, dual carriageway, motorway) | 8 | POS.*, MWY.KEEP_LEFT |
| 4 | Priority junctions: emerging, turning, H2 pedestrians, H3 cyclists | 9 | JCT.* |
| 5 | Traffic-light junctions: filters, ASLs, box junctions | 8 | TL.*, JCT.BOX |
| 6 | Following distance and queues | 7 | GAP.* |
| 7 | Roundabouts: normal, multi-lane, spiral, mini, double-mini, signalised | 7 | RBT.* |
| 8 | Motorway: join, leave, lanes, red X, variable limits, hard shoulder | 6 | MWY.* |
| 9 | Pedestrian crossings and patrols | 5 | XNG.* |
| 10 | Cyclists and cycle lanes | 4 | VRU.* |
| 11 | Bus lanes and buses | 3 | BUS.LANE_HOURS |
| 12 | School zones and child hazards | 1.5 | SCH.* |
| 13 | Parking and stopping basics | 1 | PRK.* (MVP subset) |
| 14 | Overtaking on single carriageways | 1 | OVT.* |
| 15 | Emergency vehicles | 0.5 | EMG.LET_PASS |
| 16 | Roadworks and lane closures | 0.5 | WRK.* |
| 17 | Lights and dazzle | 0.5 | WX.HEADLIGHTS_NIGHT, NO_DAZZLE |
| | **Covered by MVP** | **80** | |

**The remaining ~20%, and why it is left out:**

| Gap | Weight (%) | Why it's hard | Plan |
|---|---|---|---|
| Anticipation and judgement beyond written rules: reading other drivers, eye contact, local courtesy, "creeping" for a view | 7 | Not a rule we can source; open to interpretation | Partly via hazard scenarios and the coach's "good practice" stars (doc 03); the rest is for real lessons |
| Local, unusual or temporary signs and orders that map data doesn't hold | 4 | OSM rarely stores TRO detail | Sign-reading drills; data-confidence gating |
| Parking details: bays, permits, controlled parking zones, payment | 2.5 | Many local variants | V1 sign quizzes |
| Rare and rural: level crossings, single track, horses, trams, tunnels, fords | 2.5 | Infrequent on a typical commute | V1 rural module raises coverage to ~82% |
| Severe weather and night on unlit roads | 2 | Rendering and physics effort | V1 conditions module raises coverage to ~84% |
| Incidents, police signals, non-motorway breakdowns | 1 | Rare and scripted | Later |
| Vehicle condition and checks (tyres, lights, loading) | 1 | Not a driving-in-traffic rule | Later, in Theory Prep |

**A caveat on "80%".** Covering a situation is not the same as the user mastering it. The product metric is *weighted rule mastery* (doc 01, section 7): each user's mastered rules are multiplied by these weights. That is what we report against the 80% goal.

## 5. Open verification list (to clear before MVP content lock)
1. **Stopping-distance values**: read them from SD-PDF (the table is an image there).
2. **Rule 160** (keep left) and **rule 129** (exceptions for double white lines): not seen this session.
3. **Rule 239** (door opening, "Dutch reach") and the **Traffic signs** page: not read.
4. **Smart motorways policy** (cancellation of new schemes in 2023; current names for motorway types): check GOV.UK and National Highways.
5. **Penalty amounts** (red X camera fines, phone offences): informational only, but must be correct or left out.
6. **DVSA practical-test fault guidance**: confirm the public source for the D/S/X model and the "progress" wording.
7. **Rules 222–223** (buses): the number for "give priority to buses pulling out" wasn't confirmed.
8. Every severity threshold marked `ASSUMPTION` goes to the **ADI review** (doc 06).
9. Set up an **update watcher**: a weekly CI job diffs the GOV.UK updates page and opens an issue on any change.
