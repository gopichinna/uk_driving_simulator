# Lesson Rule Implementation Report

Generated from `content/lessons/gb/*.json` and `content/countries/gb/rules.yaml`.
- ✅ = implemented in `core/Rules.Core`
- ⏳ = planned (not yet implemented in `core/Rules.Core`)

| Order | Lesson | Rule ID | Status |
|---|---|---|---|
| 1 | Keeping left and the controls | `GB.POS.KEEP_LEFT` | ⏳ |
| 1 | Keeping left and the controls | `GB.SPD.OVER_LIMIT` | ✅ |
| 1 | Keeping left and the controls | `GB.POS.DUAL_KEEP_LEFT` | ⏳ |
| 2 | Moving off and MSM | `GB.OBS.MOVE_OFF_CHECKS` | ⏳ |
| 2 | Moving off and MSM | `GB.SIG.TIMELY` | ✅ |
| 2 | Moving off and MSM | `GB.OBS.MSM` | ⏳ |
| 3 | Speed limits | `GB.SPD.OVER_LIMIT` | ✅ |
| 3 | Speed limits | `GB.SPD.APPROPRIATE` | ⏳ |
| 3 | Speed limits | `GB.SCH.SLOW` | ⏳ |
| 3 | Speed limits | `GB.GAP.TWO_SECOND` | ✅ |
| 4 | Give way at T-junctions | `GB.JCT.GIVE_WAY` | ⏳ |
| 4 | Give way at T-junctions | `GB.JCT.EMERGE_LOOK` | ⏳ |
| 4 | Give way at T-junctions | `GB.JCT.TURN_FAR_GAP` | ⏳ |
| 5 | Pedestrians at junctions (H2) | `GB.JCT.H2_PEDESTRIANS` | ⏳ |
| 5 | Pedestrians at junctions (H2) | `GB.SIG.TIMELY` | ✅ |
| 5 | Pedestrians at junctions (H2) | `GB.JCT.GIVE_WAY` | ⏳ |
| 6 | Road position after turning | `GB.POS.AFTER_TURN` | ⏳ |
| 6 | Road position after turning | `GB.POS.KEEP_LEFT` | ⏳ |
| 6 | Road position after turning | `GB.JCT.TURN_NEAR` | ⏳ |
| 6 | Road position after turning | `GB.JCT.TURN_FAR_POSITION` | ⏳ |
| 6 | Road position after turning | `GB.JCT.TURN_FAR_GAP` | ⏳ |
| 7 | Traffic lights and amber | `GB.TL.RED` | ⏳ |
| 7 | Traffic lights and amber | `GB.TL.AMBER` | ⏳ |
| 7 | Traffic lights and amber | `GB.TL.RED_AMBER` | ⏳ |
| 7 | Traffic lights and amber | `GB.TL.NO_TURN_ON_RED` | ⏳ |
| 7 | Traffic lights and amber | `GB.SIG.TIMELY` | ✅ |
| 8 | Advanced stop lines | `GB.TL.ASL` | ⏳ |
| 8 | Advanced stop lines | `GB.TL.RED` | ⏳ |
| 8 | Advanced stop lines | `GB.VRU.PASS_CYCLIST` | ✅ |
| 8 | Advanced stop lines | `GB.JCT.H3_CYCLISTS` | ⏳ |
| 9 | Zebra crossings and zig-zags | `GB.XNG.ZEBRA` | ⏳ |
| 9 | Zebra crossings and zig-zags | `GB.XNG.KEEP_CLEAR` | ⏳ |
| 9 | Zebra crossings and zig-zags | `GB.XNG.ZIGZAG_OVERTAKE` | ⏳ |
| 10 | Box junctions | `GB.JCT.BOX` | ⏳ |
| 10 | Box junctions | `GB.TL.CLEAR_JUNCTION` | ⏳ |
| 10 | Box junctions | `GB.JCT.TURN_FAR_GAP` | ⏳ |
