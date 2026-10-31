import json
import math
from pathlib import Path

from shape import BILGE, Shape, beam, bilge, box, group, path, plank, stages, water_plane, write

CENTRE = 8
VOXEL_METRES = 1 / 16

# A Teifi coracle, Museum Wales: 1.5 m long, 1.07 m across; a flat bottom whose laths rise about 0.35 m to the gunwale.
GUNWALE = (12, 8.56)
BOTTOM = (9.6, 6.4)
DEPTH = 5.6
STAVES = 24

# Hornell 1938: seven laths along and seven across; their spacing fills the bottom.
LATHS = 7
ALONG_SPACING = 1.8
ACROSS_SPACING = 2.7
LATH_WIDTH = 0.96
LATH_THICKNESS = 0.3
PLAITS = 3
PLAIT_WIDTH = 0.6
PLAIT_HEIGHT = 0.4

HIDE = 0.2
# A small hide is about 1.2 m²: a moose's 3.5 m² over the three leather vanilla tans from its large hide.
SECTION_SQUARE_METRES = 1.2

# A crossing tied with a few turns of cord, about 0.8 m of it (an estimate); the hide laced to the gunwale every 5 cm,
# each turn round the gunwale and the hide's edge, some 15 cm of cord (Hornell 1938: "laced at short intervals").
TIE_METRES = 0.8
LACING_STEP_METRES = 0.05
LACING_TURN_METRES = 0.15
LACING_WRAPS = 16
# Vanilla's rope: one item pays out up to 10 m (ClothSystem.maxLen).
ROPE_METRES = 10

TEXTURES = {
    "hide": "game:block/cloth/armor-padding/hide",
    "frame": "game:block/wood/debarked/oak",
    "lashing": "game:item/resource/rope",
}


def oval(axes, y, k, n=STAVES, out=0.0):
    angle = 2 * math.pi * (k + 0.5) / n
    return (CENTRE + (axes[0] + out) * math.cos(angle), y, CENTRE + (axes[1] + out) * math.sin(angle))


def offsets(spacing):
    return [spacing * (n - LATHS // 2) for n in range(LATHS)]


def half(axes, across):
    return axes[0] * math.sqrt(max(0.0, 1 - (across / axes[1]) ** 2))


def bent(along, offset, y):
    """A lath of the bottom bent up at both ends to the gunwale, in its own plane: along x at z offset, or along z."""
    (a, b), (ga, gb) = (BOTTOM, GUNWALE) if along == "x" else (BOTTOM[::-1], GUNWALE[::-1])
    flat, rim = half((a, b), offset), half((ga, gb), offset)
    run = [(-rim, DEPTH), (-flat - (rim - flat) * 0.55, DEPTH * 0.45), (-flat, y), (flat, y),
           (flat + (rim - flat) * 0.55, DEPTH * 0.45), (rim, DEPTH)]
    return [(CENTRE + u, h, CENTRE + offset) if along == "x" else (CENTRE + offset, h, CENTRE + u) for u, h in run]


def length(points):
    return sum(math.dist(p, q) for p, q in zip(points, points[1:]))


def laths():
    pieces = []
    for along, spacing, y in (("x", ALONG_SPACING, HIDE + LATH_THICKNESS / 2), ("z", ACROSS_SPACING, HIDE + 1.5 * LATH_THICKNESS)):
        for offset in offsets(spacing):
            points = bent(along, offset, y)
            reach = length(points) / 2
            ends = [(CENTRE - reach, y, CENTRE + offset), (CENTRE + reach, y, CENTRE + offset)] if along == "x" \
                else [(CENTRE + offset, y, CENTRE - reach), (CENTRE + offset, y, CENTRE + reach)]
            pieces.append((ends, points))
    return pieces


def lattice(prefix, shape_of):
    return [path(f"{prefix}{n}x", shape_of(piece), LATH_WIDTH, LATH_THICKNESS, "frame", overlap=0) for n, piece in enumerate(laths(), 1)]


def on_bottom(x, z):
    return ((x - CENTRE) / BOTTOM[0]) ** 2 + ((z - CENTRE) / BOTTOM[1]) ** 2 < 1


def ties():
    crossings = [(CENTRE + dx, CENTRE + dz) for dz in offsets(ALONG_SPACING) for dx in offsets(ACROSS_SPACING)
                 if on_bottom(CENTRE + dx, CENTRE + dz)]
    ropes = math.ceil(len(crossings) * TIE_METRES / ROPE_METRES)
    print(f"binding: {len(crossings)} crossings tied, {len(crossings) * TIE_METRES:.0f} m, {ropes} ropes of {ROPE_METRES} m")
    top = HIDE + 2 * LATH_THICKNESS + 0.15
    knots = [box(f"Tie{n}", (x - 0.6, HIDE - 0.1, z - 0.6), (x + 0.6, top, z + 0.6), "lashing") for n, (x, z) in enumerate(crossings, 1)]
    return [knots[len(knots) * r // ropes:len(knots) * (r + 1) // ropes] for r in range(ropes)]


def plaits():
    rings = []
    for n in range(PLAITS):
        y = DEPTH - n * PLAIT_HEIGHT
        points = [oval(GUNWALE, y, k, out=LATH_THICKNESS) for k in range(STAVES + 1)]
        rings.append(path(f"Plait{n + 1}x", points, PLAIT_WIDTH, PLAIT_HEIGHT, "frame", overlap=0.1))
    return rings


def panels():
    """The hide over the frame, outside it: strips across the bottom and boards up the sides, each with the angle round
    the bowl its middle lies at and its area in square metres."""
    pieces = []
    for n, dz in enumerate(range(-5, 6, 2), 1):
        reach = half(BOTTOM, abs(dz) - 1)
        for side, label in ((-1, "Bow"), (1, "Stern")):
            x0, x1 = sorted((CENTRE, CENTRE + side * reach))
            element = box(f"Bottom{label}{n}", (x0, 0, CENTRE + dz - 1), (x1, HIDE, CENTRE + dz + 1), "hide")
            pieces.append((math.atan2(dz, side * reach / 2), (x1 - x0) * 2 * VOXEL_METRES ** 2, element))
    for k in range(STAVES):
        for tier, (lower, upper, y0, y1) in enumerate([(BOTTOM, mid(), 0, DEPTH * 0.45), (mid(), GUNWALE, DEPTH * 0.45, DEPTH)], 1):
            a, b = oval(lower, y0, k), oval(lower, y0, k + 1)
            top = oval(upper, y1, k)
            rise = math.dist(a, top)
            flare = math.degrees(math.atan2(math.hypot(top[0] - a[0], top[2] - a[2]), y1 - y0))
            element = plank(f"Side{tier}x{k + 1}", a, b, rise, HIDE, flare, -1, "hide", "hide", overlap=0.5)
            pieces.append((2 * math.pi * (k + 1) / STAVES, math.dist(a, b) * rise * VOXEL_METRES ** 2, element))
    return pieces


def mid():
    return tuple(b + (g - b) * 0.55 for b, g in zip(BOTTOM, GUNWALE))


def cover():
    pieces = panels()
    area = sum(square for _, square, _ in pieces)
    sections = math.ceil(area / SECTION_SQUARE_METRES)
    print(f"cover: {area:.2f} m² of hide, {sections} sections of {SECTION_SQUARE_METRES} m²")
    sectors = [[] for _ in range(sections)]
    for angle, _, element in pieces:
        sectors[int((angle % (2 * math.pi)) / (2 * math.pi) * sections) % sections].append(element)
    return sectors


def lacing():
    perimeter = length([oval(GUNWALE, DEPTH, k) for k in range(STAVES + 1)]) * VOXEL_METRES
    metres = perimeter / LACING_STEP_METRES * LACING_TURN_METRES + perimeter
    ropes = math.ceil(metres / ROPE_METRES)
    print(f"lacing: {metres:.0f} m round a {perimeter:.2f} m gunwale, {ropes} ropes")
    wraps = []
    for n in range(LACING_WRAPS):
        x, y, z = oval(GUNWALE, DEPTH, n * STAVES / LACING_WRAPS, out=LATH_THICKNESS)
        angle = math.degrees(math.atan2(z - CENTRE, x - CENTRE))
        wraps.append(group(f"Lace{n + 1}Turn", (x, y, z), (0, -angle, 0), [
            box(f"Lace{n + 1}", (-1, -1.4, -0.3), (1.2, 0.5, 0.3), "lashing"),
        ]))
    return [wraps[len(wraps) * r // ropes:len(wraps) * (r + 1) // ropes] for r in range(ropes)]


def surface(prefix, height, texture="null"):
    share = height / DEPTH
    axes = tuple(b + (g - b) * share - 0.8 for b, g in zip(BOTTOM, GUNWALE))
    planes = []
    for n, offset in enumerate(range(-9, 9, 2), 1):
        middle = max(0, offset, -offset - 2)
        reach = half(axes, middle) if middle < axes[1] else 0
        if reach > 0:
            planes.append(water_plane(f"{prefix}{n}", CENTRE - reach, CENTRE + reach, CENTRE + offset, CENTRE + offset + 2,
                                      height, texture))
    return planes


def finished():
    return [*(element for piece in lattice("BentLath", lambda piece: piece[1]) for element in piece),
            *(element for rope in ties() for element in rope),
            *(element for plait in plaits() for element in plait),
            *(element for section in cover() for element in section),
            *(element for rope in lacing() for element in rope)]


def build(path_):
    shape = Shape(path_, {**TEXTURES, "bilge": BILGE})
    shape.add(stages("Lattice", lattice("Lath", lambda piece: piece[0])))
    shape.add(stages("LatticeBent", lattice("BentLath", lambda piece: piece[1])))
    shape.add(stages("Binding", ties()))
    shape.add(stages("Gunwale", plaits()))
    shape.add(stages("Cover", cover(), wood="none"))
    shape.add(stages("Lacing", lacing()))
    shape.add(group("hideWater", (0, 0, 0), (0, 0, 0), surface("hideWater", DEPTH - 0.5)),
              bilge(HIDE + 2 * LATH_THICKNESS, DEPTH - 0.5, lambda prefix, height: surface(prefix, height, "bilge")))
    shape.keep("Hull", "FrontSeat")
    shape.write()

    back = Path(path_).with_name("coracle-back.json")
    worn = json.loads(back.read_text())
    root = worn["elements"][0]
    root["children"][0]["children"] = finished()
    worn["textures"] = TEXTURES
    write(back, worn)
    print(f"{back}: worn bowl")


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
