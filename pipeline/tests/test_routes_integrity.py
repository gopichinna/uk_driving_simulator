"""Tests that every route in content/cities/*/routes.json is legal, connected, and timed."""

from __future__ import annotations

import json
import math

from ltl_pipeline.schema import REPO_ROOT, validate_routes_file

CITIES_DIR = REPO_ROOT / "content" / "cities"


def _link_length_m(lk: dict, nodes_by_id: dict[str, dict]) -> float:
    pts = lk.get("geometry")
    if pts and len(pts) >= 2:
        return sum(
            math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1])
            for i in range(len(pts) - 1)
        )
    a, b = nodes_by_id[lk["from"]], nodes_by_id[lk["to"]]
    return math.hypot(b["x"] - a["x"], b["z"] - a["z"])


def test_all_city_routes_are_legal_connected_and_timed() -> None:
    route_files = sorted(CITIES_DIR.glob("*/routes.json"))
    assert len(route_files) >= 5

    for rpath in route_files:
        routes_data = validate_routes_file(rpath)
        pack = json.loads((rpath.parent / "roadpack.json").read_text(encoding="utf-8"))
        links_by_id = {lk["id"]: lk for lk in pack["links"]}
        nodes_by_id = {n["id"]: n for n in pack["nodes"]}
        banned = {
            (r["fromLink"], r["viaNode"], r["toLink"]) for r in pack.get("restrictions", [])
        }

        for route in routes_data["routes"]:
            items = route["links"]
            assert len(items) >= 2
            prev_end: str | None = None
            total_s = 0.0

            for idx, item in enumerate(items):
                lid = item["linkId"]
                direction = item["direction"]
                assert lid in links_by_id, f"{rpath.parent.name}/{route['id']}: missing link {lid}"
                lk = links_by_id[lid]
                if lk["oneway"]:
                    assert direction == "forward", (
                        f"{rpath.parent.name}/{route['id']}: oneway breach on {lid}"
                    )

                start_n = lk["from"] if direction == "forward" else lk["to"]
                end_n = lk["to"] if direction == "forward" else lk["from"]
                if prev_end is not None:
                    assert start_n == prev_end, (
                        f"{rpath.parent.name}/{route['id']}: gap at index {idx} "
                        f"({prev_end} != {start_n})"
                    )
                if idx > 0:
                    prev_lid = items[idx - 1]["linkId"]
                    assert (prev_lid, start_n, lid) not in banned, (
                        f"{rpath.parent.name}/{route['id']}: banned turn {prev_lid}->{lid}"
                    )
                prev_end = end_n

                length_m = _link_length_m(lk, nodes_by_id)
                mph = lk.get("speedLimitMph") or 30
                total_s += length_m / (mph * 0.44704 * 0.72)

            est = route.get("estimatedMinutes")
            if est is not None and est >= 2.0:
                min_min = (total_s * 0.72 / 0.85) / 60.0
                max_min = (total_s * 0.72 / 0.20) / 60.0
                assert min_min * 0.8 <= est <= max_min * 1.2, (
                    f"{rpath.parent.name}/{route['id']}: est={est} min outside "
                    f"[{min_min * 0.8:.1f}, {max_min * 1.2:.1f}]"
                )
