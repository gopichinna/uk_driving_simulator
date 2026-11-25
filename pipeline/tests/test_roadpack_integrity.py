"""Integrity tests for Wolverhampton RoadPack v0.6 (no dangling refs, merged links)."""

from __future__ import annotations

from ltl_pipeline.schema import REPO_ROOT, validate_roadpack_file

ROADPACK_PATH = REPO_ROOT / "content" / "cities" / "wolverhampton" / "roadpack.json"


def test_wolverhampton_has_no_dangling_references_and_merged_links() -> None:
    pack = validate_roadpack_file(ROADPACK_PATH)
    assert pack["packFormatVersion"] == "0.6"
    assert len(pack["links"]) < 1079

    node_ids = {n["id"] for n in pack["nodes"]}
    link_ids = {lk["id"] for lk in pack["links"]}
    crossing_ids = {c["id"] for c in pack["crossings"]}

    for lk in pack["links"]:
        assert lk["from"] in node_ids
        assert lk["to"] in node_ids

    for c in pack["crossings"]:
        assert c["linkId"] in link_ids

    for r in pack.get("restrictions", []):
        assert r["fromLink"] in link_ids
        assert r["viaNode"] in node_ids
        assert r["toLink"] in link_ids

    for jp in pack.get("junctionPriority", []):
        assert jp["nodeId"] in node_ids
        assert all(lid in link_ids for lid in jp["majorLinks"])

    ped_plans = 0
    for sp in pack.get("signalPlans", []):
        assert sp["nodeId"] in node_ids
        for st in sp["stages"]:
            assert all(lid in link_ids for lid in st)
        if "pedestrianStageS" in sp:
            ped_plans += 1
            assert sp["confidence"] <= 0.5
            assert all(cid in crossing_ids for cid in sp.get("crossingIds", []))
    assert ped_plans > 0

    for lc in pack.get("laneConnections", []):
        assert lc["nodeId"] in node_ids
        assert lc["fromLink"] in link_ids
        assert lc["toLink"] in link_ids

    for spn in pack.get("spawnPoints", []):
        assert spn["linkId"] in link_ids
