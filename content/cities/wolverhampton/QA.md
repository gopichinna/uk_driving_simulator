# RoadPack QA Report: Wolverhampton City Centre

- **Pack ID:** `gb-wolverhampton-centre` (format `0.5`)
- **Extract:** `geofabrik west-midlands-latest.osm.pbf` (`2026-11-24T21:21:23Z`)
- **Totals:** 953 nodes, 1079 links, 119 crossings, 13 turn restrictions

## Links by confidence band
| Band | Count |
|---|---|
| High (>= 0.80) | 455 |
| Medium (0.50–0.79) | 161 |
| Low (< 0.50) | 463 |

## Speed limits & crossings
- **Links using default speed limit:** 485 / 1079
- **Crossings with unknown/uncontrolled type:** 46 / 119

## Graph connectivity
- **Connected components:** 15
- **Largest component:** 906 / 953 nodes

## Very short links (< 2.0 m)
- **Count:** 4
  - `l_15132985_1`: 1.19 m
  - `l_22757677_2`: 1.37 m
  - `l_627988061_0`: 0.63 m
  - `l_85580973_1`: 0.83 m

## Junction priority & signal plans
- **Junction priority entries:** 614 (high 8, medium 315, low 291)
- **Inferred signal plans:** 73

| controlledBy | Count |
|---|---|
| `roundabout` | 8 |
| `signals` | 73 |
| `uncontrolled` | 533 |

## Width & sidewalk coverage
- **Tagged carriageway width:** 0 / 1079 (rest estimated at 3.25 m/lane)
- **Tagged sidewalk:** 448 / 1079 (rest inferred from roadClass)

## Buildings
- **Total buildings (>= 15 m²):** 568
- **Height sources:** tagged=0, levels=10, default=558

## Main AI network (strongly connected)
- **Main network:** 596 links, 483 nodes
- **Excluded dead-end / disconnected links:** 483 / 1079
- **Spawn points:** 12

## Lane connections
- **Total lane connections:** 721 (high 10, medium 711, low 0)
