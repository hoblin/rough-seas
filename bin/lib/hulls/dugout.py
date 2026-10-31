import math

from shape import BILGE, Hull, Shape, box, breadth, group, water_plane

LOG = 16
LOGS = 5
ROW = (8 - LOGS * LOG / 2, 8 + LOGS * LOG / 2)
CENTRE = 8
PROFILE = [(0, 0), (3.84, 0.4), (6.24, 2.0), (7.08, 4.7), (7.2, 9)]
WOOD = "inside"
BARK = "bark"
RINGS = "rings"
RIM = 9
WALL = 1.2
HOLLOWINGS = 6
BANDS = 3
WALL_BANDS = 2
CHAR_LIFT = 0.02
CLAY = 0.5
EMBERS = 0.6
BED = 0.55
GLOW = 160
VOXEL_METRES = 1 / 16
TRUNK_METRES = LOG * VOXEL_METRES
ENDS = {
    "Stern": [(-24.5, -22, 5.0, 1.6), (-26.5, -24.5, 4.0, 2.8), (-28, -26.5, 2.9, 4.2)],
    "Bow": [(38, 40.5, 5.0, 1.6), (40.5, 42.5, 4.0, 2.8), (42.5, 44, 2.9, 4.2)],
}
HOLLOW = (-22, 38)


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / RIM)) for h, y in PROFILE])


STATIONS = [
    section(-22, 0.72, 1.4),
    section(-15, 0.92, 0.45),
    section(-5, 1, 0),
    section(8, 1, 0),
    section(21, 1, 0),
    section(31, 0.92, 0.45),
    section(38, 0.72, 1.4),
]


def log(hull):
    return [hull.strakes(), *(end(name, slices, hull.cz) for name, slices in ENDS.items())]


def end(name, slices, centre):
    blocks = []
    for n, (x0, x1, half, bottom) in enumerate(slices, 1):
        waist = waist_of(bottom)
        blocks.append(box(f"{name}{n}Upper", (x0, waist, centre - half), (x1, RIM, centre + half), WOOD))
        blocks.append(box(f"{name}{n}Lower", (x0, bottom, centre - half * 0.7), (x1, waist, centre + half * 0.7), WOOD))
    return blocks


def waist_of(bottom):
    return bottom + (RIM - bottom) / 3


def end_slice(x):
    for slices in ENDS.values():
        for x0, x1, half, bottom in slices:
            if x0 <= x <= x1:
                return half, bottom
    return None


def finished_half(x, y):
    x = min(max(x, ENDS["Stern"][-1][0]), ENDS["Bow"][-1][1])
    if (piece := end_slice(x)) is not None:
        half, bottom = piece
        return 0 if y < bottom else half * 0.7 if y < waist_of(bottom) else half
    for (x0, edges0), (x1, edges1) in zip(STATIONS, STATIONS[1:]):
        if x0 <= x <= x1:
            f = (x - x0) / (x1 - x0)
            low = edges0[0][1] + f * (edges1[0][1] - edges0[0][1])
            return 0 if y < low else breadth(edges0, y) + f * (breadth(edges1, y) - breadth(edges0, y))
    return 0


def finished_bottom(x):
    x = min(max(x, ENDS["Stern"][-1][0]), ENDS["Bow"][-1][1])
    if (piece := end_slice(x)) is not None:
        return piece[1]
    for (x0, edges0), (x1, edges1) in zip(STATIONS, STATIONS[1:]):
        if x0 <= x <= x1:
            return edges0[0][1] + (x - x0) / (x1 - x0) * (edges1[0][1] - edges0[0][1])
    return 0


def worked(n):
    return n / HOLLOWINGS


def top(n):
    return LOG + (RIM - LOG) * worked(n)


def floor(n, x):
    return max(finished_bottom(x) * worked(n) + WALL, top(n) - (RIM - WALL) * worked(n))


def extent(n):
    stern, bow = ENDS["Stern"][-1][0], ENDS["Bow"][-1][1]
    return ROW[0] + (stern - ROW[0]) * worked(n), ROW[1] + (bow - ROW[1]) * worked(n)


def half_at(n, x, y):
    return LOG / 2 + (finished_half(x, y * RIM / top(n)) - LOG / 2) * worked(n)


def hollow_half(x):
    return max(0, finished_half(x, RIM) - WALL)


def segments(n):
    stern, bow = extent(n)
    cuts = {stern, bow, *HOLLOW, *(x for x, _ in STATIONS), *(edge for slices in ENDS.values() for piece in slices for edge in piece[:2])}
    points = sorted(x for x in cuts if stern <= x <= bow)
    return [(a, b) for a, b in zip(points, points[1:]) if b - a > 1e-6]


def hollowed(x):
    return HOLLOW[0] < x < HOLLOW[1]


def faces(n, x0, x1):
    if n > 0:
        return WOOD
    stern, bow = extent(n)
    return {"north": BARK, "south": BARK, "up": BARK, "down": BARK,
            "west": RINGS if x0 <= stern + 1e-6 else BARK, "east": RINGS if x1 >= bow - 1e-6 else BARK}


def bands(low, high, count):
    step = (high - low) / count
    return [(low + step * k, low + step * (k + 1)) for k in range(count)]


def trunk(n):
    pieces = []
    for i, (x0, x1) in enumerate(segments(n), 1):
        mid = (x0 + x1) / 2
        tex = faces(n, x0, x1)
        bottom = finished_bottom(mid) * worked(n)
        cut = n > 0 and hollowed(mid)
        solid_top = floor(n, mid) if cut else top(n)
        for k, (low, high) in enumerate(bands(bottom, solid_top, BANDS), 1):
            half = half_at(n, mid, (low + high) / 2)
            if half > 0:
                pieces.append(box(f"Blank{n}x{i}Body{k}", (x0, low, CENTRE - half), (x1, high, CENTRE + half), tex))
        if not cut:
            continue
        inner = hollow_half(mid)
        for k, (low, high) in enumerate(bands(solid_top, top(n), WALL_BANDS), 1):
            half = half_at(n, mid, (low + high) / 2)
            if half > inner:
                pieces.append(box(f"Blank{n}x{i}Port{k}", (x0, low, CENTRE - half), (x1, high, CENTRE - inner), tex))
                pieces.append(box(f"Blank{n}x{i}Starboard{k}", (x0, low, CENTRE + inner), (x1, high, CENTRE + half), tex))
    return pieces


def fire(n):
    planes, embers = [], []
    for i, (x0, x1) in enumerate(segments(n), 1):
        mid = (x0 + x1) / 2
        if not hollowed(mid):
            continue
        height, half = floor(n, mid) if n > 0 else top(n), hollow_half(mid)
        planes.append(water_plane(f"Char{n}x{i}", x0, x1, CENTRE - half, CENTRE + half, height + CHAR_LIFT, "char"))
        if half > 1:
            embers.append(glowing(box(f"Fire{n}Embers{i}", (x0 + 0.5, height, CENTRE - half * BED),
                                      (x1 - 0.5, height + EMBERS, CENTRE + half * BED), "ember")))
    height = floor(n, CENTRE) if n > 0 else top(n)
    logs = [box(f"Fire{n}Log{k}", (x, height + EMBERS, CENTRE + across - 0.7), (x + 9, height + EMBERS + 1.4, CENTRE + across + 0.7),
                "burntlog", origin=(x + 4.5, height, CENTRE), rot=(0, yaw, 0))
            for k, (x, across, yaw) in enumerate([(-12, -1.4, 8), (-3, 1.2, -6), (6, -1, 5), (15, 1.5, -9), (24, 0, 4)], 1)]
    return planes, embers, logs


def clay(n):
    strips = []
    for i, (x0, x1) in enumerate(segments(n), 1):
        mid = (x0 + x1) / 2
        half, height = half_at(n, mid, top(n)), top(n)
        if hollowed(mid):
            inner = hollow_half(mid)
            strips.append(box(f"Clay{n}x{i}Port", (x0, height, CENTRE - half), (x1, height + CLAY, CENTRE - inner), "clay"))
            strips.append(box(f"Clay{n}x{i}Starboard", (x0, height, CENTRE + inner), (x1, height + CLAY, CENTRE + half), "clay"))
        elif half > 0:
            strips.append(box(f"Clay{n}x{i}Cap", (x0, height, CENTRE - half), (x1, height + CLAY, CENTRE + half), "clay"))
    return strips


def glowing(element):
    element["glow"] = GLOW
    return element


def build_stages():
    stages = {"Blank": [], "Char": [], "Wood": [], "Fire": [], "Clay": []}
    for n in range(HOLLOWINGS):
        planes, embers, logs = fire(n)
        for name, children in (("Blank", trunk(n)), ("Char", planes), ("Wood", logs), ("Fire", embers), ("Clay", clay(n))):
            stages[name].append(group(f"{name}{n}", (0, 0, 0), (0, 0, 0), children))
    return [group(name, (0, 0, 0), (0, 0, 0), children) for name, children in stages.items()]


def volumes():
    step = 0.25
    envelope = hold = 0
    x = ENDS["Stern"][-1][0] + step / 2
    while x < ENDS["Bow"][-1][1]:
        y = finished_bottom(x) + step / 2
        while y < RIM:
            half = finished_half(x, y)
            envelope += 2 * half * step * step
            if hollowed(x) and y > floor(HOLLOWINGS, x):
                hold += 2 * max(0, half - WALL) * step * step
            y += step
        x += step
    litres = VOXEL_METRES ** 3 * 1000
    trunk_litres = math.pi * (TRUNK_METRES / 2) ** 2 * LOGS * TRUNK_METRES * 1000
    return envelope * litres, hold * litres, trunk_litres


def hull_box():
    stern, bow = ENDS["Stern"][-1][0], ENDS["Bow"][-1][1]
    half = PROFILE[-1][0]
    return {"name": "Hull", "from": [stern, 0, CENTRE - half], "to": [bow, RIM + 0.5, CENTRE + half], "faces": {},
            "attachmentpoints": [{"code": "HullAP", "posX": str(8 - stern), "posY": "0", "posZ": str(half),
                                  "rotationX": "0", "rotationY": "0", "rotationZ": "0"}]}


def build(path):
    shape = Shape(path, {WOOD: "game:block/wood/debarked/oak", BARK: "game:block/wood/bark/oak-h", RINGS: "game:block/wood/treetrunk/oak",
                         "bilge": BILGE, "char": "game:block/coal/charcoal", "clay": "game:block/clay/blueclay",
                         "ember": "game:block/coal/ember", "burntlog": "game:block/wood/firepit/burntlog"})
    hull = Hull("Side", STATIONS, CENTRE, WALL, WOOD, WOOD, edge=WOOD)
    shape.add(group("Shell", (0, 0, 0), (0, 0, 0), [element for piece in log(hull) for element in (piece if isinstance(piece, list) else [piece])]))
    shape.add(*build_stages())
    shape.add(hull.hide_water(RIM - 0.3), hull.bilge(RIM - 0.3))
    shape.elements.append(hull_box())
    shape.keep("FrontSeat", "BackSeat")
    envelope, hold, trunk_litres = volumes()
    print(f"dugout: trunk {trunk_litres:.0f} L, finished envelope {envelope:.0f} L, hold {hold:.0f} L, wood {envelope - hold:.0f} L, "
          f"hewn off outside {trunk_litres - envelope:.0f} L")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
