# ADI Review: 15 Scenarios to Watch

These 15 scenarios are the highest-risk teaching moments where a wrong simulator judgement (false pass or false fault) would teach a learner an unsafe or incorrect UK driving habit.

1. **`s0_01_town_keep_left` (`GB.POS.KEEP_LEFT`)**: Verify that briefly moving right to pass a parked car when clear does **not** trigger a wrong-side fault, while cruising across the centre line does.
2. **`s0_03_school_street` & `s4_28_school_pass` (`GB.SCH.SLOW`)**: Verify whether 20 mph past a school warning sign without a 20 mph repeater sign feels realistic or overly strict when no children are visible.
3. **`s4_28_school_minor_30` & `s4_28_school_serious_ped` (`GB.SCH.SLOW`)**: Confirm that `30 mph` with no pedestrians is a minor `Driving` fault while `30 mph` with pedestrians nearby escalates to `Serious`.
4. **`s1_04_tjunction_north` (`GB.JCT.GIVE_WAY`)**: Check whether the `other_decel_req` threshold accurately distinguishes safe merging from forcing a main-road driver to brake.
5. **`s1_05_h2_north` (`GB.JCT.H2_PEDESTRIANS`)**: Verify Rule H2 behaviour — turning into a side road when a pedestrian is waiting on the kerb vs already crossing, and ensure waiting pedestrians trigger a fault if cut across.
6. **`s1_29_left_early_signal` & `s1_29_left_no_signal` (`GB.JCT.TURN_NEAR`)**: Check that a 2.0 s left signal before turning passes cleanly, while turning without signalling (or < 2.0 s) triggers a fault.
7. **`s1_29_right_clear_gap` & `s1_29_right_oncoming_cut` (`GB.JCT.TURN_FAR_GAP`)**: Verify that a 3.5 s oncoming gap feels safe in 30 mph town traffic and does not teach learners to cut across fast oncoming cars.
8. **`s2_07_signals_stop` (`GB.TL.RED`, `GB.TL.AMBER`, `GB.TL.RED_AMBER`)**: Check the amber "too close to stop safely" zone so learners are neither forced into unsafe emergency stops nor allowed to run late ambers.
9. **`s2_08_asl_stop` & `s2_08_asl_cyclists` (`GB.TL.ASL`)**: Verify that stopping inside the cycle box on red is `Serious`, while stopping at the second white line when amber catches you past the first line is handled fairly.
10. **`s2_09_zebra_yield` & `s2_09_zebra_zigzags` (`GB.XNG.ZEBRA`, `GB.XNG.ZIGZAG_OVERTAKE`)**: Check that overtaking on zig-zags is always flagged, and that stopping for waiting pedestrians does not penalise smooth braking.
11. **`s2_10_box_right_turn` (`GB.JCT.BOX`)**: Verify that waiting inside the yellow box **only when turning right and blocked by oncoming traffic** passes (Rule 174 exception), while entering with a blocked exit fails.
12. **`s3_11_rbt_give_way` & `s3_12_rbt_lanes` (`GB.RBT.*`)**: Check clockwise circulation, right-priority give-way, approach lane selection, and exit-signal timing (`0.25 rad` before exit).
13. **`s3_suburb_stop_pass` & `s3_suburb_stop_roll` (`GB.JCT.STOP_SIGN`)**: On the `suburb_practice` STOP junction (`Woodthorne Road` / `Wergs Road`), confirm a rolling stop at 3–5 mph fails (`Serious`) while a full standstill behind the line passes.
14. **`s4_15_town_cyclist` (`GB.VRU.PASS_CYCLIST`)**: Verify that `1.5 m` lateral clearance up to 30 mph and `2.0 m` above 30 mph (Rule 163) are judged from vehicle-body edge to cyclist, not centre-to-centre.
15. **`s7_22_rain_stopping` & `s7_23_fog_visibility` (`GB.GAP.TWO_SECOND`, weather)**: Verify that the 2-second rule vs rain/fog guidance does not teach learners that 2 seconds is enough in heavy rain (until `GB.GAP.WET_ICY` 4-second rule is wired).
