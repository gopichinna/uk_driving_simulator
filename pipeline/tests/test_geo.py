"""Tests for local metre projection."""

import math

from pyproj import Geod

from ltl_pipeline.geo import LocalProjection

WOLVERHAMPTON_ORIGIN = (52.5862, -2.1288)


def test_origin_maps_to_zero() -> None:
    proj = LocalProjection(*WOLVERHAMPTON_ORIGIN)
    x, z = proj.to_local(*WOLVERHAMPTON_ORIGIN)
    assert x == 0.0
    assert z == 0.0


def test_round_trip() -> None:
    proj = LocalProjection(*WOLVERHAMPTON_ORIGIN)
    test_points = [
        (52.5862, -2.1288),
        (52.5905, -2.1210),
        (52.5818, -2.1365),
    ]
    for lat, lon in test_points:
        x, z = proj.to_local(lat, lon)
        lat_back, lon_back = proj.to_wgs84(x, z)
        assert abs(lat_back - lat) < 1e-6
        assert abs(lon_back - lon) < 1e-6


def test_known_distance_within_half_metre_over_one_km() -> None:
    proj = LocalProjection(*WOLVERHAMPTON_ORIGIN)
    geod = Geod(ellps="WGS84")

    # ~1 km north-east of origin
    lat2, lon2 = 52.5925, -2.1180
    _, _, true_dist_m = geod.inv(WOLVERHAMPTON_ORIGIN[1], WOLVERHAMPTON_ORIGIN[0], lon2, lat2)
    x1, z1 = proj.to_local(*WOLVERHAMPTON_ORIGIN)
    x2, z2 = proj.to_local(lat2, lon2)
    local_dist_m = math.hypot(x2 - x1, z2 - z1)

    assert 900.0 < true_dist_m < 1200.0
    assert abs(local_dist_m - true_dist_m) < 0.5
