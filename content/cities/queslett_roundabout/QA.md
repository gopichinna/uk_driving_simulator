# Queslett / Aldridge Road Roundabout Pack — QA Report

- **Pack ID:** `queslett_roundabout`
- **Status:** **Not yet playable in-game** — `TwoLaneRoundabout` in `core/Sim.Core/TwoLaneRoundabout.cs` currently hardcodes symmetric 90° 4-arm bearings (`0, 90, 180, 270`) and 2-in/2-out arms; this pack will become playable once Claude extends `TwoLaneRoundabout` for asymmetric 4-exit geometry and 1-lane exit arms (`Bakers Lane`, `Aldridge Road`).
- **Source:** OpenStreetMap (`west-midlands-latest.osm.pbf`, ODbL © OpenStreetMap contributors), centred at `52.565552, -1.885967`.
- **Size:** All files combined are well under 5 MB.
- **Contents:** `roadpack.json` (97 nodes, 116 links, 70 `laneConnections`), `buildings.json`, `roundabout_furniture.json`, `routes.json` (3 legal connected routes covering 3 different exits).
