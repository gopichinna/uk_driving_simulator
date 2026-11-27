# 4-Exit Roundabout Build Specification: Queslett / Aldridge Road (Great Barr, Birmingham)

**Candidate selected:** Queslett / Aldridge Road Roundabout (from `docs/specs/complex-roundabouts.md` §1.1) — simplest 4-exit upgrade from the current symmetric 2-lane roundabout.  
**OSM verification:** `west-midlands-latest.osm.pbf` (2026-11-26 Geofabrik extract, ODbL © OpenStreetMap contributors), circulatory way `792` centred at `52.565552, -1.885967`.

---

## 1. Geometry, Speed Limits, Signals, and Lane Markings

- **Signals:** None (`0` traffic-signal nodes; Give Way to traffic from the right, Highway Code Rule 185).
- **Circulatory carriageway:** `2` lanes (`lanes=2`, `oneway=yes`, `lit=yes`), speed limit **30 mph** (`maxspeed=30 mph`).
- **Four arms (clockwise from North, verified from OSM bearings):**

| Arm | Road / Ref | Bearing from centre | Entry lanes | Exit lanes | Speed limit | Entry lane markings (left → right) |
|---|---|---|---|---|---|---|
| **A1** | Queslett Road East (`A4041` NW) | `321.4°` | 2 (`way=791` / flare) | 2 | 40 mph | Lane 0 (left): Exit 1 (`Aldridge Rd` W) & Exit 2 (`Queslett Rd E` SE). Lane 1 (right): Exit 2 (`Queslett Rd E` SE), Exit 3 (`Bakers Ln`), Exit 4 (U-turn). |
| **A2** | Queslett Road East (`A4041` SE) | `126.2°` | 2 (`way=790` / flare) | 2 | 40 mph | Lane 0 (left): Exit 1 (`Bakers Ln`) & Exit 2 (`Aldridge Rd` W). Lane 1 (right): Exit 2 (`Aldridge Rd` W), Exit 3 (`Queslett Rd E` NW), Exit 4 (U-turn). |
| **A3** | Bakers Lane (SSE) | `152.1°` | 2 (`way=633069094` flare) | 1 (`way=793`) | 30 mph | Lane 0 (left): Exit 1 (`Aldridge Rd` W) & Exit 2 (`Queslett Rd E` NW). Lane 1 (right): Exit 3 (`Queslett Rd E` SE) & Exit 4 (U-turn). |
| **A4** | Aldridge Road (W) | `284.6°` | 2 (`way=71204710` flare) | 1 (`way=71204701`) | 30 mph | Lane 0 (left): Exit 1 (`Queslett Rd E` NW) & Exit 2 (`Queslett Rd E` SE). Lane 1 (right): Exit 3 (`Bakers Ln`) & Exit 4 (U-turn). |

---

## 2. Three Practice Routes (`route.v0` format, not yet built into pack)

```json
{
  "routeFormatVersion": "0.1",
  "packId": "rbt_4exit_queslett",
  "routes": [
    {
      "id": "route_queslett_exit1_left",
      "title": "Exit 1: Bakers Lane left turn",
      "linkIds": ["l_790_in", "l_792_se_sse", "l_793_out"],
      "instructions": [
        { "atLinkId": "l_790_in", "action": "keep_left", "text": "Stay in the left lane for Bakers Lane (1st exit)." },
        { "atLinkId": "l_792_se_sse", "action": "take_exit", "text": "Signal left and take the 1st exit onto Bakers Lane." }
      ]
    },
    {
      "id": "route_queslett_exit2_ahead",
      "title": "Exit 2: A4041 straight ahead",
      "linkIds": ["l_790_in", "l_792_se_w", "l_792_w_nw", "l_791_out"],
      "instructions": [
        { "atLinkId": "l_790_in", "action": "straight", "text": "Follow A4041 ahead (3rd exit, Queslett Road East)." },
        { "atLinkId": "l_792_w_nw", "action": "take_exit", "text": "Signal left after Aldridge Road and leave onto A4041." }
      ]
    },
    {
      "id": "route_queslett_exit3_right",
      "title": "Exit 3: Aldridge Road right turn",
      "linkIds": ["l_791_in", "l_792_nw_se", "l_792_se_sse", "l_792_sse_w", "l_71204701_out"],
      "instructions": [
        { "atLinkId": "l_791_in", "action": "keep_right", "text": "Use the right lane and signal right for Aldridge Road." },
        { "atLinkId": "l_792_se_sse", "action": "take_exit", "text": "Signal left after Bakers Lane and exit onto Aldridge Road." }
      ]
    }
  ]
}
```

---

## 3. Gaps in `Sim.Core/Roundabout.cs` and `Sim.Core/TwoLaneRoundabout.cs`

1. **Hardcoded orthogonal 90° bearings (`TwoLaneRoundabout.cs` line 16):** `ArmBearingsDeg = { 0, 90, 180, 270 }` assumes symmetric 90° arms; Queslett Roundabout has non-orthogonal arm bearings (`126.2°`, `152.1°`, `284.6°`, `321.4°`) with a tight `25.9°` gap between A2 (`126.2°`) and A3 (`152.1°`).
2. **Symmetric 2-in/2-out arm geometry (`Entry[n, 2]`, `Exit[n, 2]`):** `TwoLaneRoundaboutGeometry` assumes every arm has 2 entry lanes and 2 exit lanes; A3 (`Bakers Lane`) and A4 (`Aldridge Road`) flare to 2 lanes on entry (`way=633069094`, `way=71204710`) but narrow to **1 exit lane** (`way=793`, `way=71204701`).
3. **No per-entry exit-to-lane mapping:** `RoundaboutRule.cs` / `TwoLaneRoundabout.cs` assume fixed clock-face rules (lane 0 = left/straight, lane 1 = right/U-turn). On asymmetric 4-arm layouts (e.g., entering on A1 `321.4°`, A2 `126.2°` is at ~165° so both lanes can be signed for ahead/A4041), allowed exits per entry lane must be driven by `RoadPack.laneConnections[]` / per-arm sign metadata.
4. **Speed-limit transitions at the give-way line:** A1/A2 (`A4041`) are `40 mph` approaches feeding a `30 mph` circulatory carriageway, requiring per-link `speedLimitMph` updates across entry/exit transitions.
