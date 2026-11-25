"""Coordinate projection between WGS84 lat/lon and local metres (x = east, z = north)."""

from __future__ import annotations

from dataclasses import dataclass

from pyproj import CRS, Transformer


@dataclass(frozen=True)
class LocalProjection:
    """Transverse Mercator projection centred on a RoadPack origin.

    Local coordinates are in metres: x = east, z = north.
    """

    origin_lat: float
    origin_lon: float

    def __post_init__(self) -> None:
        wgs84 = CRS.from_epsg(4326)
        local_crs = CRS.from_proj4(
            f"+proj=tmerc +lat_0={self.origin_lat} +lon_0={self.origin_lon} "
            "+k=1 +x_0=0 +y_0=0 +datum=WGS84 +units=m +no_defs"
        )
        object.__setattr__(
            self,
            "_to_local",
            Transformer.from_crs(wgs84, local_crs, always_xy=True),
        )
        object.__setattr__(
            self,
            "_to_wgs84",
            Transformer.from_crs(local_crs, wgs84, always_xy=True),
        )

    def to_local(self, lat: float, lon: float) -> tuple[float, float]:
        """Convert WGS84 (lat, lon) to local (x, z) metres."""
        x, z = self._to_local.transform(lon, lat)
        return (round(float(x), 3), round(float(z), 3))

    def to_wgs84(self, x: float, z: float) -> tuple[float, float]:
        """Convert local (x, z) metres back to WGS84 (lat, lon)."""
        lon, lat = self._to_wgs84.transform(x, z)
        return (float(lat), float(lon))
