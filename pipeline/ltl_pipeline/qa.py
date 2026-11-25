"""Generate a Markdown QA report for a RoadPack v0/v0.1 file."""

from __future__ import annotations

import math
from collections import defaultdict
from pathlib import Path
from typing import Any


def _polyline_length_m(geom: list[list[float]]) -> float:
    return sum(
        math.hypot(geom[i + 1][0] - geom[i][0], geom[i + 1][1] - geom[i][1])
        for i in range(len(geom) - 1)
    )


def _connected_components(pack: dict[str, Any]) -> list[int]:
    adj: dict[str, set[str]] = defaultdict(set)
    for lk in pack.get("links", []):
        adj[lk["from"]].add(lk["to"])
        adj[lk["to"]].add(lk["from"])

    seen: set[str] = set()
    sizes: list[int] = []
    for n in pack.get("nodes", []):
        nid = n["id"]
        if nid in seen:
            continue
        stack = [nid]
        seen.add(nid)
        size = 0
        while stack:
            cur = stack.pop()
            size += 1
            for nxt in adj[cur]:
                if nxt not in seen:
                    seen.add(nxt)
                    stack.append(nxt)
        sizes.append(size)
    return sorted(sizes, reverse=True)


def generate_qa_markdown(
    pack: dict[str, Any],
    buildings_data: dict[str, Any] | None = None,
) -> str:
    """Build a deterministic Markdown QA report for a RoadPack dict."""
    links = pack.get("links", [])
    nodes = pack.get("nodes", [])
    crossings = pack.get("crossings", [])
    restrictions = pack.get("restrictions", [])
    priorities = pack.get("junctionPriority", [])
    signal_plans = pack.get("signalPlans", [])

    high = sum(1 for lk in links if lk["confidence"] >= 0.8)
    med = sum(1 for lk in links if 0.5 <= lk["confidence"] < 0.8)
    low = sum(1 for lk in links if lk["confidence"] < 0.5)

    default_speed = sum(1 for lk in links if lk.get("speedLimitSource") == "default")
    unknown_crossings = [
        c for c in crossings if c.get("type") in ("signals_unknown", "uncontrolled")
    ]
    short_links = [
        (lk["id"], round(_polyline_length_m(lk["geometry"]), 2))
        for lk in links
        if _polyline_length_m(lk["geometry"]) < 2.0
    ]
    components = _connected_components(pack)

    lines = [
        f"# RoadPack QA Report: {pack.get('displayName', pack['packId'])}",
        "",
        f"- **Pack ID:** `{pack['packId']}` (format `{pack['packFormatVersion']}`)",
        f"- **Extract:** `{pack['source'].get('extract', 'n/a')}` "
        f"(`{pack['source'].get('extractTimestamp', 'n/a')}`)",
        f"- **Totals:** {len(nodes)} nodes, {len(links)} links, "
        f"{len(crossings)} crossings, {len(restrictions)} turn restrictions",
        "",
        "## Links by confidence band",
        "| Band | Count |",
        "|---|---|",
        f"| High (>= 0.80) | {high} |",
        f"| Medium (0.50–0.79) | {med} |",
        f"| Low (< 0.50) | {low} |",
        "",
        "## Speed limits & crossings",
        f"- **Links using default speed limit:** {default_speed} / {len(links)}",
        f"- **Crossings with unknown/uncontrolled type:** {len(unknown_crossings)} / "
        f"{len(crossings)}",
        "",
        "## Graph connectivity",
        f"- **Connected components:** {len(components)}",
        f"- **Largest component:** {components[0] if components else 0} / {len(nodes)} nodes",
        "",
        "## Very short links (< 2.0 m)",
        f"- **Count:** {len(short_links)}",
    ]
    for lid, dist in short_links:
        lines.append(f"  - `{lid}`: {dist} m")

    by_ctrl: dict[str, int] = defaultdict(int)
    for jp in priorities:
        by_ctrl[jp["controlledBy"]] += 1
    jp_high = sum(1 for jp in priorities if jp["confidence"] >= 0.8)
    jp_med = sum(1 for jp in priorities if 0.5 <= jp["confidence"] < 0.8)
    jp_low = sum(1 for jp in priorities if jp["confidence"] < 0.5)

    lines.extend(
        [
            "",
            "## Junction priority & signal plans",
            f"- **Junction priority entries:** {len(priorities)} "
            f"(high {jp_high}, medium {jp_med}, low {jp_low})",
            f"- **Inferred signal plans:** {len(signal_plans)}",
            "",
            "| controlledBy | Count |",
            "|---|---|",
        ]
    )
    for ctrl in sorted(by_ctrl):
        lines.append(f"| `{ctrl}` | {by_ctrl[ctrl]} |")

    w_tagged = sum(1 for lk in links if lk.get("widthSource") == "tagged")
    sw_tagged = sum(1 for lk in links if lk.get("sidewalkSource") == "tagged")
    lines.extend(
        [
            "",
            "## Width & sidewalk coverage",
            f"- **Tagged carriageway width:** {w_tagged} / {len(links)} "
            "(rest estimated at 3.25 m/lane)",
            f"- **Tagged sidewalk:** {sw_tagged} / {len(links)} (rest inferred from roadClass)",
        ]
    )

    if buildings_data is not None:
        bldgs = buildings_data.get("buildings", [])
        h_src: dict[str, int] = defaultdict(int)
        for b in bldgs:
            h_src[b["heightSource"]] += 1
        lines.extend(
            [
                "",
                "## Buildings",
                f"- **Total buildings (>= 15 m²):** {len(bldgs)}",
                f"- **Height sources:** tagged={h_src['tagged']}, "
                f"levels={h_src['levels']}, default={h_src['default']}",
            ]
        )

    main_net = pack.get("mainNetwork")
    if main_net is not None:
        dead_ends = sum(1 for lk in links if not lk.get("inMainNetwork", False))
        spawns = pack.get("spawnPoints", [])
        lines.extend(
            [
                "",
                "## Main AI network (strongly connected)",
                f"- **Main network:** {main_net['linkCount']} links, "
                f"{main_net['nodeCount']} nodes",
                f"- **Excluded dead-end / disconnected links:** {dead_ends} / {len(links)}",
                f"- **Spawn points:** {len(spawns)}",
            ]
        )
    lines.append("")
    return "\n".join(lines)


def write_qa_report(
    pack: dict[str, Any],
    out_path: Path,
    buildings_data: dict[str, Any] | None = None,
) -> Path:
    """Write QA.md to disk."""
    out_path.write_text(generate_qa_markdown(pack, buildings_data=buildings_data), encoding="utf-8")
    return out_path
