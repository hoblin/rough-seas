import copy
import json
import math
from pathlib import Path

from fit import APART, Oval
from physics import HIDE_METRES, LATH_SQUARE_METRES, ROPE_METRES, afloat, shares, hide_sections, kilograms, lacing_metres, per_rope, ropes, stands
from held import SCULL_REACH_METRES, worked_from_middle, PADDLE, poling, sculling, tools, waterline_metres
from stroke import player_patch, seat
import units
from entity import merged, record
from shape import BILGE, Shape, bilge_levels, hull_box, windage, across, heading, seated, bilge, box, group, path, plank, stages, water_plane, write
from wounds import backed, wound

CENTRE = 8

# Hornell 1938: seven laths along and seven across; their spacing fills the bottom, on the 1/16 m the lath bed lays them by.
LATHS = 7
ALONG_SPACING = 2
ACROSS_SPACING = 3
LATH_WIDTH = 0.96
LATH_THICKNESS = 0.3
PLAITS = 3
PLAIT_WIDTH = 0.6
PLAIT_HEIGHT = 0.4

HIDE = 0.2

# A Teifi coracle, Museum Wales: 1.5 m long, 1.07 m across; a flat bottom whose laths rise about 0.35 m to the gunwale.
# The hide is stretched over the frame, so the bottom reaches round the outermost laths, the across ones over the along.
GUNWALE = (12, 8.56)
BOTTOM = (ACROSS_SPACING * (LATHS // 2) + LATH_WIDTH / 2 + HIDE + LATH_THICKNESS, ALONG_SPACING * (LATHS // 2) + LATH_WIDTH / 2 + HIDE)
DEPTH = 5.6
STAVES = 24
STRIP = 2
KNEE = DEPTH * 0.45
SIDE_HEIGHTS = (0, KNEE, DEPTH)

# A crossing tied with a few turns of cord, about 0.8 m of it (an estimate).
TIE_METRES = 0.8
TIE = 1.2
LACING_WRAPS = 16

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


def bent(along, offset, layer):
    """A lath of the bottom bent up at both ends to the gunwale, in its own plane: along x at z offset, or along z."""
    run = rising(along, offset, layer)
    run = [(-u, h) for u, h in reversed(run)] + run
    return [(CENTRE + u, h, CENTRE + offset) if along == "x" else (CENTRE + offset, h, CENTRE + u) for u, h in run]


def rising(along, offset, layer):
    """Where a lath's centre turns up from the bottom, then at the knee and at the gunwale, out from the middle: its
    outer face against the hide's inside (or the laths under it) where its edge lies nearest the bowl."""
    floor = HIDE + layer * LATH_THICKNESS
    edge = abs(offset) + LATH_WIDTH / 2

    def wall(h):
        axes = tuple(axis - floor for axis in axes_at(h))
        return half(axes if along == "x" else axes[::-1], edge), h

    lines = [inward((0, floor), (1, floor)), inward(wall(floor), wall(KNEE)), inward(wall(KNEE), wall(DEPTH)), ((0, DEPTH), (1, DEPTH))]
    return [meeting(*pair) for pair in zip(lines, lines[1:])]


def inward(p, q):
    du, dh = q[0] - p[0], q[1] - p[1]
    shift = LATH_THICKNESS / 2 / math.hypot(du, dh)
    return (p[0] - dh * shift, p[1] + du * shift), (q[0] - dh * shift, q[1] + du * shift)


def meeting(first, second):
    (p, q), (r, s) = first, second
    d1, d2 = (q[0] - p[0], q[1] - p[1]), (s[0] - r[0], s[1] - r[1])
    t = ((r[0] - p[0]) * d2[1] - (r[1] - p[1]) * d2[0]) / (d1[0] * d2[1] - d1[1] * d2[0])
    return p[0] + t * d1[0], p[1] + t * d1[1]


def length(points):
    return sum(math.dist(p, q) for p, q in zip(points, points[1:]))


def laths():
    pieces = []
    for along, spacing, layer in (("x", ALONG_SPACING, 0), ("z", ACROSS_SPACING, 1)):
        for offset in offsets(spacing):
            points = bent(along, offset, layer)
            reach = length(points) / 2
            flat = (layer + 0.5) * LATH_THICKNESS
            ends = [(CENTRE - reach, flat, CENTRE + offset), (CENTRE + reach, flat, CENTRE + offset)] if along == "x" \
                else [(CENTRE + offset, flat, CENTRE - reach), (CENTRE + offset, flat, CENTRE + reach)]
            pieces.append((ends, points))
    return pieces


def lattice(prefix, shape_of):
    return [path(f"{prefix}{n}x", shape_of(piece), LATH_WIDTH, LATH_THICKNESS, "frame", overlap=0) for n, piece in enumerate(laths(), 1)]


def on_bottom(dx, dz):
    return abs(dx) + TIE / 2 <= rising("x", dz, 0)[0][0] and abs(dz) + TIE / 2 <= rising("z", dx, 1)[0][0]


def ties():
    crossings = [(CENTRE + dx, CENTRE + dz) for dz in offsets(ALONG_SPACING) for dx in offsets(ACROSS_SPACING) if on_bottom(dx, dz)]
    count = ropes(len(crossings) * TIE_METRES)
    print(f"binding: {len(crossings)} crossings tied, {len(crossings) * TIE_METRES:.0f} m, {count} ropes of {ROPE_METRES} m")
    top = HIDE + 2 * LATH_THICKNESS + 0.15
    return per_rope([box(f"Tie{n}", (x - TIE / 2, HIDE + APART, z - TIE / 2), (x + TIE / 2, top, z + TIE / 2), "lashing")
                     for n, (x, z) in enumerate(crossings, 1)], count)


def plaits():
    rings = []
    for n in range(PLAITS):
        y = DEPTH - n * PLAIT_HEIGHT
        points = [oval(GUNWALE, y, k, out=LATH_THICKNESS) for k in range(STAVES + 2)]
        turn = max(abs(math.remainder(heading(b, c)[0] - heading(a, b)[0], 360)) for a, b, c in zip(points, points[1:], points[2:]))
        overlap = PLAIT_WIDTH / 2 * math.tan(math.radians(turn) / 2)
        rings.append(path(f"Plait{n + 1}x", points[:-1], PLAIT_WIDTH, PLAIT_HEIGHT, "frame", overlap=overlap))
    return rings


def panels():
    """The hide over the frame, outside it: strips across the bottom, a facet under each side board's foot joining
    them to the sides, and boards up the sides, each with the angle round the bowl its middle lies at and the square
    metres of hide it stands for (the facets carry the bottom's, the strips none)."""
    pieces = []
    for n, dz in enumerate(range(-5, 6, STRIP), 1):
        reach = half(BOTTOM, abs(dz) + STRIP / 2)
        for side, label in ((-1, "Bow"), (1, "Stern")):
            x0, x1 = sorted((CENTRE, CENTRE + side * reach))
            element = box(f"Bottom{label}{n}", (x0, 2 * APART, CENTRE + dz - STRIP / 2), (x1, HIDE - 2 * APART, CENTRE + dz + STRIP / 2), "hide")
            pieces.append((math.atan2(dz, side * reach / 2), 0, element))
    for k in range(STAVES):
        a, b = oval(BOTTOM, 0, k), oval(BOTTOM, 0, k + 1)
        y0 = APART * (k % 2)
        yaw = math.degrees(math.atan2(a[2] - b[2], b[0] - a[0]))
        facet = box(f"Floor{k + 1}", (0, y0, 0), (math.dist(a, b), HIDE - y0, STRIP), "hide")
        sector = abs((a[0] - CENTRE) * (b[2] - CENTRE) - (b[0] - CENTRE) * (a[2] - CENTRE)) / 2
        pieces.append((2 * math.pi * (k + 0.5) / STAVES, units.metres(sector, 2), group(f"Floor{k + 1}Frame", a, (0, yaw, 0), [facet])))
        for row, (y0, y1) in enumerate(zip(SIDE_HEIGHTS, SIDE_HEIGHTS[1:])):
            a, b = oval(axes_at(y0), y0, k), oval(axes_at(y0), y0, k + 1)
            d, c = oval(axes_at(y1), y1, k), oval(axes_at(y1), y1, k + 1)
            rise, flare = across(a, b, [((p - q) + (r - s)) / 2 for p, q, r, s in zip(d, a, c, b)])
            wide = math.dist(c, d)
            middleward = [a[2] - b[2], 0, b[0] - a[0]]
            middleward = [v / math.hypot(*middleward) for v in middleward]
            board = lambda depth, a=a, b=b, rise=rise, flare=flare, wide=wide, row=row, middleward=middleward: plank(
                f"Side{row + 1}x{k + 1}", [p + v * depth for p, v in zip(a, middleward)], [p + v * depth for p, v in zip(b, middleward)], rise, HIDE, -flare, -1,
                "hide", "hide", overlap=(wide - math.dist(a, b)) / 2)
            element = board(seated(board, bowl()))
            pieces.append((2 * math.pi * (k + 1) / STAVES, units.metres((math.dist(a, b) + wide) / 2 * rise, 2), element))
    return pieces


def mid():
    return tuple(b + (g - b) * 0.55 for b, g in zip(BOTTOM, GUNWALE))


def hide_area(pieces):
    return sum(square for _, square, _ in pieces)


def cover(pieces):
    area = hide_area(pieces)
    sections = hide_sections(area)
    sectors = [[] for _ in range(sections)]
    for angle, _, element in pieces:
        sectors[int((angle % (2 * math.pi)) / (2 * math.pi) * sections) % sections].append(element)
    return sectors


def lacing():
    perimeter = units.metres(length([oval(GUNWALE, DEPTH, k) for k in range(STAVES + 1)]))
    metres = lacing_metres(perimeter)
    count = ropes(metres)
    print(f"lacing: {metres:.0f} m round a {perimeter:.2f} m gunwale, {count} ropes")
    wraps = []
    for n in range(LACING_WRAPS):
        x, y, z = oval(GUNWALE, DEPTH, n * STAVES / LACING_WRAPS, out=LATH_THICKNESS)
        angle = math.degrees(math.atan2(z - CENTRE, x - CENTRE))
        wraps.append(group(f"Lace{n + 1}Turn", (x, y, z), (0, -angle, 0), [
            box(f"Lace{n + 1}", (-1, -1.4, -0.3), (1.2, 0.5, 0.3), "lashing"),
        ]))
    return per_rope(wraps, count)


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


def bowl_litres(inset, floor):
    step, total = 0.05, 0
    y = floor + step / 2
    while y < DEPTH:
        a, b = (axis - inset for axis in axes_at(y))
        total += math.pi * a * b * step
        y += step
    return units.litres(units.metres(total, 3))


def bowl():
    return Oval("the bowl", (CENTRE, CENTRE), axes_at, floor=0)


def axes_at(y):
    (lower, upper, y0, y1) = (BOTTOM, mid(), 0, KNEE) if y < KNEE else (mid(), GUNWALE, KNEE, DEPTH)
    share = (y - y0) / (y1 - y0)
    return tuple(l + (u - l) * share for l, u in zip(lower, upper))


def shell(hide, bent, box):
    """What backs her hide: her bent laths, each as wide as LATH_WIDTH, set as far apart as that over the share of her
    sides they back; a breaker bends them over her side, from her bottom to her gunwale."""
    sides, bottom = backed([hide], ["hide"], [bent], box)
    rise = [(axes_at(y)[0], y) for y in (DEPTH * k / STAVES for k in range(STAVES + 1))]
    spacing = units.metres(LATH_WIDTH) / sides
    return {"backedShare": sides, "bottomBackedShare": bottom, **units.both("span", units.metres(length(rise))),
            "frame": {**units.both("spacing", spacing), **units.both("span", spacing)}}


def report(lattice_stage, gunwale_stage, area, side):
    metres = sum(piece["metres"] for stage in (lattice_stage, gunwale_stage) for piece in stage["children"])
    hide_kg, frame_kg = kilograms("hide", area * HIDE_METRES * 1000), kilograms("wood-willow", metres * LATH_SQUARE_METRES * 1000)
    inset = HIDE + 2 * LATH_THICKNESS
    hold = bowl_litres(inset, inset)
    deck = units.metres(math.pi * (GUNWALE[0] - inset) * (GUNWALE[1] - inset), 2)
    print(f"coracle: {area:.2f} m² of hide, {hide_kg:.1f} kg; {metres:.1f} m of lath, {frame_kg:.1f} kg of willow; "
          f"bowl {bowl_litres(0, 0):.0f} L outside")
    return merged(afloat("coracle", hide_kg + frame_kg, units.metres(DEPTH), hold, deck, at_draft=side), shares([hide_kg, frame_kg]))


def build(path_):
    shape = Shape(path_, {**TEXTURES, "bilge": BILGE})
    plaited = plaits()
    flat, gunwale = stages("Lattice", lattice("Lath", lambda piece: piece[0])), stages("Gunwale", plaited)
    shape.add(flat)
    bent_laths = lattice("BentLath", lambda piece: piece[1])
    bent = stages("LatticeBent", bent_laths)
    shape.add(bent)
    tied = ties()
    binding = stages("Binding", tied)
    shape.add(binding)
    shape.add(gunwale)
    pieces = panels()
    covered = cover(pieces)
    hide = stages("Cover", covered, wood="none")
    shape.add(hide)
    shape.within(bowl(), hide)
    shape.within(Oval("the hide's inside", (CENTRE, CENTRE), axes_at, floor=0, inset=HIDE), bent, binding)
    shape.within(Oval("the gunwale", (CENTRE, CENTRE), lambda y: tuple(axis + LATH_THICKNESS + PLAIT_WIDTH / 2 for axis in GUNWALE)), gunwale)
    laces = lacing()
    worn_bowl = copy.deepcopy([element for stage in (bent_laths, tied, plaited, covered, laces) for piece in stage for element in piece])
    laced = stages("Lacing", laces)
    shape.add(laced)
    shape.together(flat, binding)
    shape.together(bent, binding, gunwale, hide, laced)
    shape.add(group("hideWater", (0, 0, 0), (0, 0, 0), surface("hideWater", DEPTH - 0.5)),
              bilge(HIDE + 2 * LATH_THICKNESS, DEPTH - 0.5, lambda prefix, height: surface(prefix, height, "bilge")))
    box, measured = hull_box(hide)
    shape.add(box)
    record("coracle", merged(measured, {"behaviorConfigs": {"roughseas:vesselintegrity": {"shell": shell(hide, bent, box)}}}))
    shape.keep("FrontSeat", "OarStorage")
    wound(shape, [("", [hide], "hide")], framing=([bent], "frame"))
    shape.write()
    layout = {"along": LATHS, "across": LATHS, **units.both("alongSpacing", units.metres(ALONG_SPACING)),
              **units.both("acrossSpacing", units.metres(ACROSS_SPACING))}
    record("coracle", merged(report(flat, gunwale, hide_area(pieces), windage([hide], box["from"][1])), measured, bilge_levels(shape.elements), {"behaviorConfigs": {"roughseas:covering": {"layout": layout}}}))
    at = seat(shape, "FrontSeat")
    record("coracle", {"behaviorConfigs": {"roughseas:propulsion": worked_from_middle(box, at, SCULL_REACH_METRES, ahead=True)}})
    water_metres = waterline_metres("coracle")
    floor = HIDE + 2 * LATH_THICKNESS
    pole = poling(stands("coracle", units.metres(floor)), at, units.voxels(water_metres), CENTRE - GUNWALE[1] - 1,
                  (CENTRE, floor, CENTRE), BOTTOM[1] - HIDE - 2 * LATH_THICKNESS)
    player_patch("player-coracle", tools(shape, "coracle", "FrontSeat", "sitflooridle", {"rotationZ": 14}, water_metres, {
        "paddle": ("sculling", PADDLE, sculling(PADDLE, at, 6.5, 5, 2.5)),
        "pole": pole,
    }, ("Cover",)))

    back = Path(path_).with_name("coracle-back.json")
    worn = json.loads(back.read_text())
    root = worn["elements"][0]
    root["children"][0]["children"] = worn_bowl
    worn["textures"] = TEXTURES
    write(back, worn)
    print(f"{back}: worn bowl")


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
