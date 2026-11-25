"""Tests for Wolverhampton free-drive routes and turn-restriction compliance."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT, validate_roadpack_file, validate_routes_file

ROADPACK_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "roadpack.json"
ROUTES_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "routes.json"


def test_routes_valid_connected_and_legal() -> None:
    pack = validate_roadpack_file(ROADPACK_PATH)
    routes_doc = validate_routes_file(ROUTES_PATH)

    assert routes_doc["packId"] == pack["packId"]
    routes = routes_doc["routes"]
    assert len(routes) == 3

    links_by_id = {lk["id"]: lk for lk in pack["links"]}
    restrictions = pack.get("restrictions", [])

    # Group restrictions by (fromLink, viaNode)
    forbidden_turns: dict[tuple[str, str], set[str]] = {}
    only_turns: dict[tuple[str, str], set[str]] = {}
    for r in restrictions:
        key = (r["fromLink"], r["viaNode"])
        rtype = r["type"]
        if rtype.startswith("no_"):
            forbidden_turns.setdefault(key, set()).add(r["toLink"])
        elif rtype.startswith("only_"):
            only_turns.setdefault(key, set()).add(r["toLink"])

    all_features: set[str] = set()

    for route in routes:
        steps = route["links"]
        assert route["start"]["linkId"] == steps[0]["linkId"]
        all_features.update(route["features"])

        prev_end_node: str | None = None
        prev_link_id: str | None = None

        for step in steps:
            lid = step["linkId"]
            direction = step["direction"]
            assert lid in links_by_id, f"{route['id']}: unknown link {lid}"
            lk = links_by_id[lid]

            if lk["oneway"]:
                assert direction == "forward", (
                    f"{route['id']}: wrong way on oneway link {lid}"
                )

            start_node = lk["from"] if direction == "forward" else lk["to"]
            end_node = lk["to"] if direction == "forward" else lk["from"]

            if prev_end_node is not None:
                assert start_node == prev_end_node, (
                    f"{route['id']}: disconnected at {prev_link_id} -> {lid}"
                )
                key = (prev_link_id, start_node)
                if key in forbidden_turns:
                    assert lid not in forbidden_turns[key], (
                        f"{route['id']}: breaks no_* restriction at {key} -> {lid}"
                    )
                if key in only_turns:
                    assert lid in only_turns[key], (
                        f"{route['id']}: breaks only_* restriction at {key} -> {lid}"
                    )

            prev_end_node = end_node
            prev_link_id = lid

    assert "signals" in all_features
    assert "roundabout" in all_features
    assert any(x in all_features for x in ("zebra", "pelican", "puffin", "toucan"))
