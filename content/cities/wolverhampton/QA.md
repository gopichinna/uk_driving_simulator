# RoadPack QA Report: Wolverhampton City Centre

- **Pack ID:** `gb-wolverhampton-centre` (format `0.6`)
- **Extract:** `geofabrik west-midlands-latest.osm.pbf` (`2026-11-24T21:21:23Z`)
- **Totals:** 875 nodes, 1001 links, 119 crossings, 13 turn restrictions

## Links by confidence band
| Band | Count |
|---|---|
| High (>= 0.80) | 433 |
| Medium (0.50–0.79) | 138 |
| Low (< 0.50) | 430 |

## Speed limits & crossings
- **Links using default speed limit:** 450 / 1001
- **Crossings with unknown/uncontrolled type:** 46 / 119

## Graph connectivity
- **Connected components:** 15
- **Largest component:** 833 / 875 nodes

## Very short links (< 2.0 m)
- **Count:** 3
  - `l_15132985_1`: 1.19 m
  - `l_22757677_2`: 1.37 m
  - `l_85580973_1`: 0.83 m

## Junction priority & signal plans
- **Junction priority entries:** 537 (high 8, medium 315, low 214)
- **Inferred signal plans:** 73

| controlledBy | Count |
|---|---|
| `roundabout` | 8 |
| `signals` | 73 |
| `uncontrolled` | 456 |

## Width & sidewalk coverage
- **Tagged carriageway width:** 0 / 1001 (rest estimated at 3.25 m/lane)
- **Tagged sidewalk:** 423 / 1001 (rest inferred from roadClass)

## Buildings
- **Total buildings (>= 15 m²):** 568
- **Height sources:** tagged=0, levels=10, default=558

## Main AI network (strongly connected)
- **Main network:** 550 links, 437 nodes
- **Excluded dead-end / disconnected links:** 451 / 1001
- **Spawn points:** 12

## Lane connections
- **Total lane connections:** 691 (high 10, medium 681, low 0)

## Links under 10 m (after degree-2 merging)
- **Count:** 163 / 1001 total links
  - `l_101292015_0` (9.47 m): between `end` and `junction`
  - `l_133822045_1` (9.02 m): between `end` and `junction`
  - `l_1385720236_0` (7.26 m): between `junction` and `junction`
  - `l_140385476_0` (6.66 m): between `junction` and `crossing`
  - `l_140385476_1` (5.27 m): between `crossing` and `crossing`
  - `l_140385476_3` (8.4 m): between `crossing` and `junction`
  - `l_146803783_3` (2.83 m): between `crossing` and `junction`
  - `l_148868372_1` (7.09 m): between `crossing` and `crossing`
  - `l_148868372_2` (6.98 m): between `crossing` and `end`
  - `l_148868372_3` (7.77 m): between `end` and `junction`
  - `l_149125995_1` (3.55 m): between `end` and `crossing`
  - `l_149160142_3` (7.11 m): between `junction` and `junction`
  - `l_149160142_4` (9.8 m): between `junction` and `junction`
  - `l_149166968_1` (2.97 m): between `junction` and `end`
  - `l_149402174_0` (4.14 m): between `end` and `end`
  - `l_149402177_1` (6.83 m): between `junction` and `junction`
  - `l_15132985_1` (1.19 m): between `roundabout` and `roundabout`
  - `l_15132985_3` (7.76 m): between `roundabout` and `roundabout`
  - `l_15132985_4` (9.22 m): between `roundabout` and `roundabout`
  - `l_15534450_1` (6.03 m): between `crossing` and `crossing`
