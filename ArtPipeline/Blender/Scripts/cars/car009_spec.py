"""CAR-009 "Brenner S1" - design specification (pure Python, no bpy).

Original design: a short-wheelbase 1980s Group-B-style AWD coupe with squared
box flares, an upright glasshouse, a four-lamp pod and a trunk wing. It is a
fictional car - no real manufacturer's body lines, badges or liveries.

Shared by build_car009.py (Blender) and livery_car009.py (Pillow) so that
geometry and the livery texture atlas always agree.

Conventions: metres, +Y forward, +Z up, +X = driver's right. Values here are
"design space"; the build script recentres the finished car so the root sits
at the ground-plane centre of its bounding box.
"""

CAR_ID = "CAR-009"
DISPLAY_NAME = "Brenner S1"
CLASS = "Group B"
REAL_WORLD_REFERENCE = "1980s short-wheelbase AWD Group B coupe (general era cues only)"

# --- Wheel package -----------------------------------------------------------
AXLE_F = 1.18          # front axle Y
AXLE_R = -1.18         # rear axle Y  -> wheelbase 2.36 m
WHEEL_X = 0.78         # half track (wheel centre plane) -> track 1.56 m
WHEEL_R = 0.33         # tyre outer radius (0.66 m diameter)
WHEEL_Z = WHEEL_R      # axle height at rest
TIRE_W = 0.235         # tyre width
RIM_R = 0.205          # rim (bead) radius
ARCH_R = 0.385         # wheel-arch cut radius (tyre + clearance)

WHEELS = {             # slot: (x, y)
    "FL": (-WHEEL_X, AXLE_F),
    "FR": (WHEEL_X, AXLE_F),
    "RL": (-WHEEL_X, AXLE_R),
    "RR": (WHEEL_X, AXLE_R),
}

# --- Lower body cross-sections ----------------------------------------------
# y, zb (floor), ws (sill half width), wsh (shoulder half width), zsh,
# wbelt, zbelt, wdeck, zdeck (deck edge), zc (deck centre)
BODY_KEYS = [
    (1.98, 0.30, 0.70, 0.745, 0.52, 0.70, 0.640, 0.62, 0.655, 0.662),
    (1.95, 0.26, 0.76, 0.800, 0.56, 0.77, 0.680, 0.68, 0.695, 0.703),
    (1.82, 0.22, 0.79, 0.830, 0.60, 0.80, 0.718, 0.71, 0.733, 0.743),
    (1.60, 0.22, 0.80, 0.840, 0.62, 0.80, 0.752, 0.72, 0.767, 0.778),
    (1.18, 0.22, 0.80, 0.840, 0.62, 0.80, 0.800, 0.72, 0.815, 0.827),
    (0.74, 0.22, 0.80, 0.840, 0.62, 0.80, 0.860, 0.73, 0.875, 0.885),
    (0.60, 0.22, 0.80, 0.840, 0.62, 0.80, 0.860, 0.73, 0.875, 0.885),
    (-0.42, 0.22, 0.80, 0.840, 0.62, 0.80, 0.860, 0.73, 0.875, 0.885),
    (-0.84, 0.22, 0.80, 0.840, 0.62, 0.80, 0.860, 0.73, 0.875, 0.885),
    (-1.18, 0.22, 0.80, 0.840, 0.62, 0.80, 0.875, 0.72, 0.890, 0.897),
    (-1.60, 0.22, 0.80, 0.840, 0.62, 0.80, 0.880, 0.72, 0.895, 0.900),
    (-1.82, 0.24, 0.79, 0.830, 0.62, 0.79, 0.880, 0.71, 0.895, 0.900),
    (-1.89, 0.27, 0.77, 0.810, 0.62, 0.77, 0.866, 0.69, 0.880, 0.885),
    (-1.92, 0.31, 0.73, 0.770, 0.62, 0.73, 0.842, 0.65, 0.856, 0.860),
]
BODY_Y_MAX = BODY_KEYS[0][0]
BODY_Y_MIN = BODY_KEYS[-1][0]
CABIN_Y = (-0.84, 0.74)  # deck opening under the glasshouse

# --- Glasshouse ----------------------------------------------------------------
GH = {
    "z0": 0.86, "z1": 1.34,   # belt / roof
    "w0": 0.76, "w1": 0.60,   # half widths at belt / roof
    "yF0": 0.74, "yB0": -0.34, "yR0": -0.84,   # A, B, C pillar feet
    "yF1": 0.20, "yB1": -0.30, "yR1": -0.56,   # A, B, C pillar tops
    "pillar": 0.034,          # painted pillar band
    "rubber": 0.016,          # black window rubber
}

# --- Box flares -------------------------------------------------------------
FLARE = {
    "lip_x": 0.952,           # outermost lip
    "shelf_x": 0.944,         # flat shelf just inside the lip
    "inner_r": 0.392,         # opening radius (just outside the arch cut)
    "bottom_z": 0.22,
    "outer": [(-0.56, 0.22), (-0.56, 0.56), (-0.44, 0.775), (0.44, 0.775), (0.56, 0.56), (0.56, 0.22)],
    "shelf_t": 0.55,
    "samples": 17,
}

# --- Palette (sRGB hex) -------------------------------------------------------
PAINT = "#ECE3D1"      # warm off-white
TEAL = "#1E5A66"
ORANGE = "#D9612A"
MUSTARD = "#E2A63A"
INK = "#1C1B1F"
UNDERBODY = "#2A2725"
SEAM = "#B9AE9B"
RIM = "#C9A04A"        # gold rims
SEAT = "#C2552A"
CAGE = "#D9D3C7"
FLAP = "#D9612A"

RACE_NUMBER = "27"
SPONSOR = "HALVIK"
CREW = "K. VARGA  /  L. MOEN"
PLATE_TEXT = "BRN 27S"

# --- Livery texture atlas -----------------------------------------------------
TEX_SIZE = 2048
U_Y0 = -2.15
S_SIDE = 1.0 / 4.3
S_TOP = 0.151
S_END = 0.10
V_RIGHT = 0.302
V_LEFT = 0.651
UNDER_UV = (0.95, 0.25)
UNDER_RECT = (0.90, 0.20, 1.00, 0.30)          # u0, v0, u1, v1
PLATE_FRONT_RECT = (0.875, 0.010, 0.995, 0.040)
PLATE_REAR_RECT = (0.875, 0.060, 0.995, 0.090)


def uv_right(x, y, z):
    return ((y - U_Y0) * S_SIDE, V_RIGHT + z * S_SIDE)


def uv_left(x, y, z):
    # mirrored so text reads correctly from the car's left side
    return ((-U_Y0 - y) * S_SIDE, V_LEFT + z * S_SIDE)


def uv_top(x, y, z):
    # forward -> +u, car-left (-X) -> +v ; reads correctly from the right side
    return ((y - U_Y0) * S_TOP, (1.0 - x) * S_TOP)


def uv_front(x, y, z):
    return (0.655 + (1.0 - x) * S_END, z * S_END)


def uv_rear(x, y, z):
    return (0.655 + (x + 1.0) * S_END, 0.15 + z * S_END)


def region_for_normal(nx, ny, nz):
    ax, ay, az = abs(nx), abs(ny), abs(nz)
    if ax >= ay and ax >= az:
        return "right" if nx > 0 else "left"
    if az >= ay:
        return "top" if nz > 0 else "under"
    return "front" if ny > 0 else "rear"


UV_FUNCS = {"right": uv_right, "left": uv_left, "top": uv_top, "front": uv_front, "rear": uv_rear}


def lerp_keys(y):
    """Interpolated body section parameters at y (clamped)."""
    keys = BODY_KEYS
    if y >= keys[0][0]:
        return list(keys[0][1:])
    if y <= keys[-1][0]:
        return list(keys[-1][1:])
    for a, b in zip(keys, keys[1:]):
        if a[0] >= y >= b[0]:
            t = (a[0] - y) / (a[0] - b[0])
            return [pa + (pb - pa) * t for pa, pb in zip(a[1:], b[1:])]
    raise ValueError(y)


def body_half_width_at(y, z):
    """Half width of the body side surface at (y, z) - used to seat flares, handles, skirts."""
    zb, ws, wsh, zsh, wbelt, zbelt, _wd, _zd, _zc = lerp_keys(y)
    prof = [(ws - 0.05, zb), (ws, zb + 0.06), (wsh, zsh), (wbelt, zbelt)]
    if z <= prof[0][1]:
        return prof[0][0]
    for (x0, z0), (x1, z1) in zip(prof, prof[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0) if z1 > z0 else 0
            return x0 + (x1 - x0) * t
    return prof[-1][0]

# Door shut lines (front, rear) and handle position, shared by geometry and livery
DOOR_Y = (0.60, -0.36)
HANDLE_Y = -0.25
