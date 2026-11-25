"""Download and checksum-verify Geofabrik OSM PBF extracts into pipeline/cache/."""

from __future__ import annotations

import hashlib
import json
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from pathlib import Path

import osmium

DEFAULT_PBF_URL = (
    "https://download.geofabrik.de/europe/united-kingdom/england/west-midlands-latest.osm.pbf"
)
DEFAULT_MD5_URL = f"{DEFAULT_PBF_URL}.md5"
PIPELINE_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CACHE_DIR = PIPELINE_ROOT / "cache"


@dataclass(frozen=True)
class FetchResult:
    pbf_path: Path
    md5_hex: str
    extract_timestamp: str
    from_cache: bool


def compute_md5(path: Path, chunk_size: int = 1024 * 1024) -> str:
    """Compute the MD5 hex digest of a file."""
    digest = hashlib.md5()
    with path.open("rb") as f:
        while chunk := f.read(chunk_size):
            digest.update(chunk)
    return digest.hexdigest()


def parse_md5_text(text: str) -> str:
    """Extract the 32-char hex digest from a Geofabrik .md5 file."""
    token = text.strip().split()[0].lower()
    if len(token) != 32:
        raise ValueError(f"Unexpected MD5 format: {text!r}")
    return token


def read_pbf_timestamp(pbf_path: Path) -> str | None:
    """Read the replication/header timestamp from an OSM PBF file."""
    reader = osmium.io.Reader(str(pbf_path))
    try:
        header = reader.header()
        ts = header.get("osmosis_replication_timestamp") or header.get("timestamp")
        if ts:
            return ts
    finally:
        reader.close()
    return None


def _download_file(url: str, dest: Path) -> str | None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    req = urllib.request.Request(url, headers={"User-Agent": "ltl_pipeline/0.1"})
    last_modified: str | None = None
    with urllib.request.urlopen(req, timeout=120) as resp, dest.open("wb") as out:
        lm = resp.headers.get("Last-Modified")
        if lm:
            last_modified = parsedate_to_datetime(lm).astimezone(timezone.utc).isoformat()
        while chunk := resp.read(1024 * 1024):
            out.write(chunk)
    return last_modified


def fetch_extract(
    pbf_url: str = DEFAULT_PBF_URL,
    md5_url: str = DEFAULT_MD5_URL,
    cache_dir: Path = DEFAULT_CACHE_DIR,
    force: bool = False,
) -> FetchResult:
    """Download the PBF extract into cache_dir, verify MD5, and record timestamp."""
    cache_dir.mkdir(parents=True, exist_ok=True)
    filename = pbf_url.rsplit("/", 1)[-1]
    pbf_path = cache_dir / filename
    meta_path = cache_dir / f"{filename}.meta.json"

    with urllib.request.urlopen(
        urllib.request.Request(md5_url, headers={"User-Agent": "ltl_pipeline/0.1"}),
        timeout=30,
    ) as resp:
        expected_md5 = parse_md5_text(resp.read().decode("utf-8"))

    if not force and pbf_path.is_file() and meta_path.is_file():
        actual_md5 = compute_md5(pbf_path)
        if actual_md5 == expected_md5:
            meta = json.loads(meta_path.read_text(encoding="utf-8"))
            return FetchResult(
                pbf_path=pbf_path,
                md5_hex=actual_md5,
                extract_timestamp=meta["extractTimestamp"],
                from_cache=True,
            )

    last_modified = _download_file(pbf_url, pbf_path)
    actual_md5 = compute_md5(pbf_path)
    if actual_md5 != expected_md5:
        raise ValueError(
            f"Checksum mismatch for {pbf_path}: expected {expected_md5}, got {actual_md5}"
        )

    extract_ts = (
        read_pbf_timestamp(pbf_path)
        or last_modified
        or datetime.now(timezone.utc).isoformat()
    )
    meta = {
        "pbfUrl": pbf_url,
        "md5": actual_md5,
        "extractTimestamp": extract_ts,
        "fetchedAt": datetime.now(timezone.utc).isoformat(),
    }
    meta_path.write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
    return FetchResult(
        pbf_path=pbf_path,
        md5_hex=actual_md5,
        extract_timestamp=extract_ts,
        from_cache=False,
    )


if __name__ == "__main__":
    result = fetch_extract()
    print(
        f"PBF: {result.pbf_path} (cached={result.from_cache}, "
        f"md5={result.md5_hex}, timestamp={result.extract_timestamp})"
    )
