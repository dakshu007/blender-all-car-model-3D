"""bike_specs.py - the six Blossom Rally motorbikes (BIKE-001..006): compact descriptions shared by
build_bike.py (Blender) and livery_bike.py (Pillow), plus the bike livery atlas. Pure Python, no bpy.

Conventions (same as the cars): metres, +Y forward, +Z up, +X = rider's right, ground at z = 0.
Design space is final space: the rear axle sits at y = -wb/2 and the front axle at +wb/2, both at x = 0
(the builder does not recentre bikes, so the livery can be drawn in the same coordinates).
All names, liveries and plates are fictional.
"""

import math


class BikeAtlas:
    """Livery atlas for a bike (one square texture): right side, left side, top, front, rear,
    the rear plate and an underbody block."""

    Y0 = -1.40            # side views cover y = -1.40 .. +1.40
    S_SIDE = 0.262        # uv per metre on the side views (2.8 m -> 0.734)
    V_RIGHT = 0.008
    V_LEFT = 0.430        # each side band holds z = 0 .. 1.60
    S_TOP = 0.17
    TOP_U = 0.75
    S_END = 0.15
    UNDER_UV = (0.62, 0.93)
    UNDER_RECT = (0.56, 0.88, 0.70, 0.99)
    PLATE_REAR_RECT = (0.020, 0.872, 0.180, 0.976)
    PLATE_FRONT_RECT = (0.220, 0.872, 0.380, 0.976)   # unused by most bikes (kept for the car tooling)

    def __init__(self):
        self.UV_FUNCS = {"right": self.uv_right, "left": self.uv_left, "top": self.uv_top,
                         "front": self.uv_front, "rear": self.uv_rear}
        self.SCALES = {"right": self.S_SIDE, "left": self.S_SIDE, "top": self.S_TOP,
                       "front": self.S_END, "rear": self.S_END}

    def uv_right(self, x, y, z):
        return ((y - self.Y0) * self.S_SIDE, self.V_RIGHT + z * self.S_SIDE)

    def uv_left(self, x, y, z):
        return ((-self.Y0 - y) * self.S_SIDE, self.V_LEFT + z * self.S_SIDE)

    def uv_top(self, x, y, z):
        return (self.TOP_U + (0.62 - x) * self.S_TOP, (y - self.Y0) * self.S_TOP)

    def uv_front(self, x, y, z):
        return (0.765 + (0.6 - x) * self.S_END, 0.50 + z * self.S_END)

    def uv_rear(self, x, y, z):
        return (0.765 + (x + 0.6) * self.S_END, 0.752 + z * self.S_END)

    @staticmethod
    def region_for_normal(nx, ny, nz):
        ax, ay, az = abs(nx), abs(ny), abs(nz)
        if ax >= ay and ax >= az:
            return "right" if nx > 0 else "left"
        if az >= ay:
            return "top" if nz > 0 else "under"
        return "front" if ny > 0 else "rear"


# --------------------------------------------------------------------------------------------- bikes
# wheel: R outer radius, w tyre width, rim radius, rim style ("cast", n) / ("spoke", n) / ("disc", n)
# tyre: road | knobby | semi | scooter | whitewall
# fork: stanchion colour key (chrome / gold / black), rake (deg from vertical), fork_len (axle -> top clamp)
# seat: (z top, y front, y rear); livery: drawn in side view (y, z) on both sides (see livery_bike.py)

BIKES = {
    # ---------------------------------------------------------------- sport bike with a full fairing
    "BIKE-001": {
        "name": "Lingyun SR", "class": "Sport Bike", "kind": "sport", "drivetrain": "RWD",
        "wb": 1.40, "rake": 24.0, "fork_len": 0.70,
        "wheel_f": {"R": 0.300, "w": 0.120, "rim": 0.216, "rim_style": ("cast", 3)},
        "wheel_r": {"R": 0.312, "w": 0.185, "rim": 0.216, "rim_style": ("cast", 3)},
        "tyre": "road", "fork": "gold", "seat": (0.83, -0.06, -0.44),
        "colors": {"base": "#F3F1EC", "accent": "#E8577E", "accent2": "#2B2A30", "frame": "#5C5F66",
                   "engine": "#3A3A3F", "seat": "#26252A", "rim": "#E8577E"},
        "livery": {
            "base": "#F3F1EC",
            "polys": [
                # charcoal lower fairing + a pink blade sweeping up the side to the tail
                {"pts": [(-0.20, 0.20), (0.92, 0.20), (0.92, 0.52), (0.55, 0.50), (0.20, 0.45), (-0.20, 0.40)], "color": "#2B2A30"},
                {"pts": [(0.88, 0.62), (0.40, 0.58), (-0.05, 0.62), (-0.55, 0.78), (-1.05, 0.95), (-1.05, 1.04),
                         (-0.55, 0.88), (-0.05, 0.71), (0.40, 0.67), (0.88, 0.72)], "color": "#E8577E"},
                {"pts": [(0.86, 0.75), (0.40, 0.70), (-0.05, 0.74), (-0.55, 0.905), (-1.05, 1.07), (-1.05, 1.10),
                         (-0.55, 0.93), (-0.05, 0.77), (0.40, 0.73), (0.86, 0.78)], "color": "#2B2A30"},
            ],
            "top_stripes": [{"x0": -0.045, "x1": -0.015, "color": "#E8577E"}, {"x0": 0.015, "x1": 0.045, "color": "#E8577E"}],
            "roundel": {"y": 0.36, "z": 0.40, "r": 0.075, "fill": "#F3F1EC", "ink": "#2B2A30"},
            "texts": [{"y": 0.05, "z": 0.86, "text": "LINGYUN", "h": 0.045, "color": "#2B2A30"},
                      {"y": -0.62, "z": 0.70, "text": "SR", "h": 0.06, "color": "#E8577E"}],
            "number": "27", "plate": "LY 001",
        },
        "accent_parts": True,
    },
    # ---------------------------------------------------------------- street naked bike
    "BIKE-002": {
        "name": "Mantis 750", "class": "Street Naked", "kind": "naked", "drivetrain": "RWD",
        "wb": 1.42, "rake": 25.0, "fork_len": 0.72,
        "wheel_f": {"R": 0.300, "w": 0.120, "rim": 0.216, "rim_style": ("cast", 5)},
        "wheel_r": {"R": 0.312, "w": 0.180, "rim": 0.216, "rim_style": ("cast", 5)},
        "tyre": "road", "fork": "gold", "seat": (0.81, -0.06, -0.48),
        "colors": {"base": "#2E7D6B", "accent": "#F2A33A", "accent2": "#F3EEDC", "frame": "#F2A33A",
                   "engine": "#2F3034", "seat": "#2A2826", "rim": "#D9A441"},
        "livery": {
            "base": "#2E7D6B",
            "polys": [
                {"pts": [(-0.06, 0.88), (0.30, 0.95), (0.30, 0.99), (-0.06, 0.92)], "color": "#F3EEDC"},
                {"pts": [(-0.06, 0.84), (0.30, 0.90), (0.30, 0.925), (-0.06, 0.865)], "color": "#F2A33A"},
                {"pts": [(-0.95, 0.86), (-0.50, 0.78), (-0.50, 0.81), (-0.95, 0.89)], "color": "#F2A33A"},
            ],
            "top_stripes": [{"x0": -0.02, "x1": 0.02, "color": "#F3EEDC"}],
            "texts": [{"y": 0.13, "z": 0.80, "text": "MANTIS", "h": 0.04, "color": "#F3EEDC"}],
            "number": "75", "plate": "MT 750",
        },
    },
    # ---------------------------------------------------------------- retro scooter (the city icon)
    "BIKE-003": {
        "name": "Peony 125", "class": "Retro Scooter", "kind": "scooter", "drivetrain": "RWD",
        "wb": 1.28, "rake": 24.0, "fork_len": 0.62,
        "wheel_f": {"R": 0.245, "w": 0.105, "rim": 0.160, "rim_style": ("disc", 5)},
        "wheel_r": {"R": 0.245, "w": 0.110, "rim": 0.160, "rim_style": ("disc", 5)},
        "tyre": "scooter", "fork": "chrome", "seat": (0.79, -0.02, -0.60),
        "colors": {"base": "#9FD8C8", "accent": "#F6EFDF", "accent2": "#E58A9A", "frame": "#3A3A3C",
                   "engine": "#6C6C70", "seat": "#B5764A", "rim": "#F6EFDF"},
        "livery": {
            "base": "#9FD8C8",
            "polys": [
                # cream belly band along the floorboard and the side cowls
                {"pts": [(-0.80, 0.18), (0.60, 0.18), (0.60, 0.30), (-0.10, 0.31), (-0.55, 0.36), (-0.80, 0.34)], "color": "#F6EFDF"},
                {"pts": [(-0.70, 0.40), (-0.12, 0.38), (-0.12, 0.405), (-0.70, 0.425)], "color": "#E58A9A"},
            ],
            "texts": [{"y": -0.42, "z": 0.53, "text": "Peony", "h": 0.055, "color": "#F6EFDF", "bold": True}],
            "blossoms": [(-0.30, 0.60, 0.035), (-0.22, 0.56, 0.025), (-0.37, 0.55, 0.022)],
            "front_badge": {"z": 0.62, "r": 0.035},
            "number": "", "plate": "PE 125",
        },
    },
    # ---------------------------------------------------------------- enduro / dirt bike
    "BIKE-004": {
        "name": "Dune Hare 450", "class": "Enduro", "kind": "enduro", "drivetrain": "RWD",
        "wb": 1.48, "rake": 27.0, "fork_len": 0.92,
        "wheel_f": {"R": 0.370, "w": 0.090, "rim": 0.268, "rim_style": ("spoke", 32)},
        "wheel_r": {"R": 0.360, "w": 0.135, "rim": 0.230, "rim_style": ("spoke", 32)},
        "tyre": "knobby", "fork": "gold", "seat": (0.95, 0.10, -0.62),
        "colors": {"base": "#F28A2E", "accent": "#F5F4F0", "accent2": "#25386B", "frame": "#3B5DA8",
                   "engine": "#434448", "seat": "#1F2433", "rim": "#2A2B2E"},
        "livery": {
            "base": "#F28A2E",
            "polys": [
                {"pts": [(-0.30, 0.55), (0.40, 0.66), (0.40, 0.74), (-0.30, 0.63)], "color": "#25386B"},
                {"pts": [(0.05, 0.86), (0.38, 0.90), (0.38, 0.93), (0.05, 0.89)], "color": "#F5F4F0"},
            ],
            "side_plate": {"y0": -0.60, "y1": -0.30, "z0": 0.62, "z1": 0.84, "fill": "#F5F4F0", "ink": "#25386B"},
            "front_number": {"z": 1.00, "fill": "#F5F4F0", "ink": "#25386B"},
            "texts": [{"y": 0.22, "z": 0.80, "text": "HARE", "h": 0.045, "color": "#F5F4F0"}],
            "number": "45", "plate": "DH 450",
        },
    },
    # ---------------------------------------------------------------- adventure tourer
    "BIKE-005": {
        "name": "Snow Leopard 1100", "class": "Adventure Tourer", "kind": "adventure", "drivetrain": "RWD",
        "wb": 1.56, "rake": 26.0, "fork_len": 0.86,
        "wheel_f": {"R": 0.355, "w": 0.110, "rim": 0.243, "rim_style": ("spoke", 36)},
        "wheel_r": {"R": 0.345, "w": 0.160, "rim": 0.216, "rim_style": ("spoke", 36)},
        "tyre": "semi", "fork": "gold", "seat": (0.88, 0.02, -0.62),
        "colors": {"base": "#D9D2C1", "accent": "#C8402F", "accent2": "#4A4B4F", "frame": "#3F4044",
                   "engine": "#55565B", "seat": "#2B2B2E", "rim": "#C9A44E"},
        "livery": {
            "base": "#D9D2C1",
            "polys": [
                {"pts": [(0.10, 0.70), (0.62, 0.86), (0.62, 0.91), (0.10, 0.76)], "color": "#C8402F"},
                {"pts": [(0.10, 0.62), (0.62, 0.78), (0.62, 0.82), (0.10, 0.665)], "color": "#4A4B4F"},
                {"pts": [(-1.10, 0.86), (-0.60, 0.80), (-0.60, 0.84), (-1.10, 0.90)], "color": "#C8402F"},
            ],
            "spots": True,
            "texts": [{"y": 0.24, "z": 0.98, "text": "SNOW LEOPARD", "h": 0.032, "color": "#4A4B4F"}],
            "number": "11", "plate": "SL 1100",
        },
    },
    # ---------------------------------------------------------------- cruiser
    "BIKE-006": {
        "name": "Red Lantern 1800", "class": "Cruiser", "kind": "cruiser", "drivetrain": "RWD",
        "wb": 1.70, "rake": 31.0, "fork_len": 0.84,
        "wheel_f": {"R": 0.335, "w": 0.130, "rim": 0.230, "rim_style": ("spoke", 40)},
        "wheel_r": {"R": 0.340, "w": 0.210, "rim": 0.230, "rim_style": ("spoke", 40)},
        "tyre": "whitewall", "fork": "chrome", "seat": (0.68, -0.18, -0.52),
        "colors": {"base": "#9E1B2C", "accent": "#E2B54E", "accent2": "#F2E6CE", "frame": "#1E1C1E",
                   "engine": "#2A292C", "seat": "#5A3424", "rim": "#D4D2CE"},
        "livery": {
            "base": "#9E1B2C",
            "polys": [],
            "pinstripes": True,
            "lanterns": [(0.20, 0.80), (-1.02, 0.66)],
            "texts": [{"y": 0.22, "z": 0.86, "text": "RED LANTERN", "h": 0.028, "color": "#E2B54E"}],
            "number": "18", "plate": "RL 1800",
        },
    },
}

ORDER = ["BIKE-001", "BIKE-002", "BIKE-003", "BIKE-004", "BIKE-005", "BIKE-006"]


def rake_axis(rake_deg):
    """Steering axis (unit) pointing up the steering head: up and back."""
    r = math.radians(rake_deg)
    return (0.0, -math.sin(r), math.cos(r))
