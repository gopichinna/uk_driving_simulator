"""Tests for Wolverhampton direction_signs.json and street_names.json."""

from __future__ import annotations

import json

from ltl_pipeline.direction_signs import (
    RoadGraph,
    _arrival_tangent,
    _classify_turn,
    _departure_tangent,
    _signed_turn_deg,
)
from ltl_pipeline.readability import lint_all_strings
from ltl_pipeline.schema import REPO_ROOT, validate_signs_placed_file

CITY_DIR = REPO_ROOT / "content" / "cities" / "wolverhampton"
SIGNS_PATH = CITY_DIR / "direction_signs.json"
STREETS_PATH = CITY_DIR / "street_names.json"
ROADPACK_PATH = CITY_DIR / "roadpack.json"
POIS_PATH = CITY_DIR / "pois.json"


def _load_all() -> tuple[dict, dict, dict, dict]:
    signs = json.loads(SIGNS_PATH.read_text(encoding="utf-8"))
    streets = json.loads(STREETS_PATH.read_text(encoding="utf-8"))
    pack = json.loads(ROADPACK_PATH.read_text(encoding="utf-8"))
    pois = json.loads(POIS_PATH.read_text(encoding="utf-8"))
    return signs, streets, pack, pois


def test_direction_signs_and_street_names_validate_against_schema() -> None:
    signs_data = validate_signs_placed_file(SIGNS_PATH)
    streets_data = validate_signs_placed_file(STREETS_PATH)
    assert len(signs_data["signs"]) >= 5
    assert len(streets_data["plates"]) >= 20
    assert any("gov.uk" in u for u in signs_data["source"]["styleSources"])


def test_every_row_direction_matches_shortest_route_first_turn() -> None:
    signs, _, pack, pois = _load_all()
    graph = RoadGraph(pack)
    poi_by_id = {p["id"]: p for p in pois["pois"]}

    for sign in signs["signs"]:
        app_link = sign["approachLinkId"]
        nid = sign["nodeId"]
        lk = graph.links[app_link]
        app_fwd = lk["to"] == nid
        assert lk["from"] == nid or lk["to"] == nid
        in_dx, in_dz = _arrival_tangent(lk["geometry"], app_fwd)
        link_dists = graph.shortest_first_turns(app_link, app_fwd, nid)

        expected_style = "primary" if lk.get("roadClass") in ("primary", "trunk") else "local"
        assert sign["style"] == expected_style

        for row in sign["rows"]:
            poi_id = row.get("poiId")
            if not poi_id:
                continue
            acc_link = poi_by_id[poi_id]["accessLinkId"]
            assert acc_link in link_dists, f"{sign['id']} POI {poi_id} unreachable"
            _, first_out = link_dists[acc_link]
            out_lk = graph.links[first_out]
            out_fwd = out_lk["from"] == nid
            out_dx, out_dz = _departure_tangent(out_lk["geometry"], out_fwd)
            expected_dir = _classify_turn(_signed_turn_deg(in_dx, in_dz, out_dx, out_dz))
            assert row["direction"] == expected_dir, (
                f"{sign['id']} row {row['text']}: got {row['direction']}, expected {expected_dir}"
            )


def test_no_sign_or_plate_on_carriageway() -> None:
    signs, streets, pack, _ = _load_all()
    graph = RoadGraph(pack)
    for sign in signs["signs"]:
        assert not graph.on_carriageway(sign["x"], sign["z"], min_clearance_m=0.5), (
            f"Direction sign {sign['id']} sits within 0.5 m of carriageway"
        )
    for plate in streets["plates"]:
        assert not graph.on_carriageway(plate["x"], plate["z"], min_clearance_m=0.5), (
            f"Street plate {plate['id']} sits within 0.5 m of carriageway"
        )


def test_direction_sign_text_passes_readability() -> None:
    assert lint_all_strings() == []
