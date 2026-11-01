import math
from pathlib import Path

from dugout import CENTRE, ENDS, HOLLOW, PROFILE, RIM, STATIONS, WALL, WOOD, deck, log, volumes
from physics import afloat, kilograms
from shape import BILGE, ROUNDED, VOXEL_METRES_CUBED_LITRES, Hull, Shape, beam, box, group, path, plank, run, stages
from wounds import wound

VOXEL_METRES = 1 / 16
ROPE_METRES = 10

LOG = 16
FLOAT_Z = 40
FLOAT_Y = 2.9
FLOAT_DIAMETER = 4.8
FLOAT_FROM, FLOAT_TO = -16, 32
TAPER = 6
TAPER_STEP = 2
HEWINGS = 6
BOOMS = (9.5, -10, 24)
BOOM = 1.4
POLE = 1.4
SPAR = 1.2
MAST = (12, 8)
MASTHEAD = 49
SPAR_METRES = 3
TACK = (-14, 11.5)
REACH = 20
SAIL_STRIPS = 16
CLOTH_SQUARE_METRES = 1

LASHING_TURNS = 13
FRAYED = 2.4
FRAYED_DEGREES = 16
FLOAT_FORMS = {
    "Loose": (0.8, 0, 0, 0, ()),
    "Torn": (1.2, 1, 2, 3, (2,)),
    "Shifted": (1.6, 3, 5, 6, (1, 2, 3)),
}
GUNWALE_METRES = WALL * VOXEL_METRES
GUNWALE = PROFILE[-1][0]

STRAKE = 2.4
STRAKE_THICKNESS = 0.4
PLANK = 16
STITCH_PITCH = 0.076 / VOXEL_METRES
STITCH_TURN_METRES = 2 * (0.06 + 0.05 + 0.02 + 0.02)


def spar_end(rise):
    length = SPAR_METRES / VOXEL_METRES
    return (TACK[0] + length * math.cos(math.radians(rise)), TACK[1] + length * math.sin(math.radians(rise)))


YARD_END = spar_end(55)
SPAR_END = spar_end(3.6)


def worked(n):
    return n / HEWINGS


def tapered(x):
    return max(0, FLOAT_FROM + TAPER - x, x - (FLOAT_TO - TAPER)) / TAPER


def hewn(x, n):
    """The float's section at x after n passes: its half-width, bottom and top, each worked n/6 of the way from the
    square logs to the round float, whose ends are hewn to a lifted point."""
    t = tapered(x)
    girth, centre = FLOAT_DIAMETER * (1 - 0.35 * t), FLOAT_Y + 1.4 * t
    w = worked(n)
    return LOG / 2 + (girth / 2 - LOG / 2) * w, (centre - girth / 2) * w, LOG + (centre + girth / 2 - LOG) * w


def float_cuts():
    ends = [FLOAT_FROM + k * TAPER_STEP for k in range(TAPER // TAPER_STEP + 1)]
    return ends + [FLOAT_TO - TAPER + k * TAPER_STEP for k in range(TAPER // TAPER_STEP + 1)]


def float_top(n):
    return hewn((FLOAT_FROM + FLOAT_TO) / 2, n)[2]


def log_faces(x0, x1):
    return {"north": "floatbark", "south": "floatbark", "up": "floatbark", "down": "floatbark",
            "west": "floatrings" if x0 <= FLOAT_FROM else "floatbark", "east": "floatrings" if x1 >= FLOAT_TO else "floatbark"}


def float_pass(n):
    elements, litres, waterline = [], 0, 0
    roundness = 1 - (1 - ROUNDED) * worked(n)
    cuts = float_cuts()
    for i, (x0, x1) in enumerate(zip(cuts, cuts[1:]), 1):
        half, low, high = hewn((x0 + x1) / 2, n)
        middle, rise = (low + high) / 2, (high - low) / 2
        if n == 0:
            elements.append(box(f"FloatLogx{i}", (x0, low, FLOAT_Z - half), (x1, high, FLOAT_Z + half), log_faces(x0, x1)))
        else:
            elements.append(box(f"Hewn{n}x{i}", (x0, middle - rise * roundness, FLOAT_Z - half), (x1, middle + rise * roundness, FLOAT_Z + half), "float"))
            elements.append(box(f"Hewn{n}x{i}Round", (x0, low, FLOAT_Z - half * roundness), (x1, high, FLOAT_Z + half * roundness), "float"))
        litres += (x1 - x0) * 2 * half * 2 * rise * roundness * (2 - roundness) * VOXEL_METRES_CUBED_LITRES
        waterline += (x1 - x0) * 2 * half * VOXEL_METRES ** 2
    return elements, round(litres, 1), round(waterline, 3)


def hewing():
    logs, litres, waterline = float_pass(0)
    passes = []
    for n in range(1, HEWINGS + 1):
        elements, wood, plane = float_pass(n)
        piece = group(f"Hewing{n}", (0, 0, 0), (0, 0, 0), elements)
        piece["woodLitres"], piece["waterlineSquareMetres"] = wood, plane
        passes.append(piece)
    stage = group("Hewing", (0, 0, 0), (0, 0, 0), passes)
    stage["woodLitres"], stage["waterlineSquareMetres"] = litres, waterline
    print(f"float: three logs {litres:.0f} L, {waterline:.2f} m² at the water, hewn in {HEWINGS} passes to "
          f"{passes[-1]['woodLitres']:.0f} L, {passes[-1]['waterlineSquareMetres']:.2f} m²")
    return group("FloatLog", (0, 0, 0), (0, 0, 0), [group("FloatLog1", (0, 0, 0), (0, 0, 0), logs)]), stage


def boom(name, x, n):
    """A crooked pole over her gunwales, resting on the float's top as the passes leave it."""
    end = float_top(n) + BOOM / 2
    points = [(x, RIM + 0.6, 0.5), (x, RIM + 1.4, 14.5), (x, max(RIM + 0.6, end), 30), (x, end, FLOAT_Z + 2.5)]
    return path(name, points, BOOM, BOOM, "frame")


def booms():
    pieces = []
    for n, x in enumerate(BOOMS, 1):
        piece = group(f"Booms{n}", (0, 0, 0), (0, 0, 0),
                      [group(f"Booms{n}At{k}", (0, 0, 0), (0, 0, 0), boom(f"Boom{n}At{k}x", x, k)) for k in range(HEWINGS + 1)])
        piece["metres"] = stages("Measure", [boom(f"Boom{n}x", x, HEWINGS)])["children"][0]["metres"]
        pieces.append(piece)
    return group("Booms", (0, 0, 0), (0, 0, 0), pieces)


def lashing(name, x, z, low, high):
    return [box(f"{name}{n}", (x - 1, low, z + a), (x + 1, high, z + b), "lashing", grain="y")
            for n, (a, b) in enumerate([(-0.9, -0.4), (0.4, 0.9)], 1)]


def lashings():
    gunwale = 2 * (BOOM * VOXEL_METRES + GUNWALE_METRES) * LASHING_TURNS
    afloat = 2 * (BOOM + FLOAT_DIAMETER) * VOXEL_METRES * LASHING_TURNS
    ties, metres = [], []
    for n, x in enumerate(BOOMS, 1):
        for side, z in (("Port", CENTRE - GUNWALE + 1), ("Starboard", CENTRE + GUNWALE - 1)):
            ties.append(lashing(f"Lash{n}{side}", x, z, RIM - 0.8, RIM + 2.4))
            metres.append(gunwale)
        ties.append(lashing(f"Lash{n}Float", x, FLOAT_Z, FLOAT_Y - FLOAT_DIAMETER / 2 - 0.3, FLOAT_Y + FLOAT_DIAMETER / 2 + BOOM + 0.3))
        metres.append(afloat)
    total = sum(metres)
    ropes = math.ceil(total / ROPE_METRES)
    per_rope = [[] for _ in range(ropes)]
    done = 0
    for tie, length in zip(ties, metres):
        per_rope[min(ropes - 1, int((done + length / 2) / (total / ropes)))] += tie
        done += length
    assert all(per_rope), "a rope with no lashing"
    print(f"lashing: {total:.0f} m in {len(ties)} lashings of {LASHING_TURNS} turns, {ropes} ropes of {ROPE_METRES} m")
    return per_rope


def slack(name, x, drop):
    """A float lashing worked loose, its turns reaching from the boom down to the float hanging lower."""
    low, high = float_lashed()
    return [box(f"{name}{k}", (x - 1, low - drop, FLOAT_Z + a), (x + 1, high, FLOAT_Z + b), "lashing", grain="y")
            for k, (a, b) in enumerate([(-0.9, -0.4), (0.4, 0.9)], 1)]


def frayed(name, x, drop):
    """A float lashing parted: its ends hang from the boom."""
    _, high = float_lashed()
    return [box(f"{name}{k}", (x - 0.4, high - FRAYED, FLOAT_Z + a), (x + 0.4, high, FLOAT_Z + b), "lashing",
                origin=(x, high, FLOAT_Z), rot=(0, 0, side * FRAYED_DEGREES), grain="y")
            for k, (a, b, side) in enumerate([(-0.9, -0.4, 1), (0.4, 0.9, -1)], 1)]


def float_lashed():
    return FLOAT_Y - FLOAT_DIAMETER / 2 - 0.3, FLOAT_Y + FLOAT_DIAMETER / 2 + BOOM + 0.3


def hung(name, drop, along, degrees, roll):
    """The hewn float as its lashings let it go: hanging lower under her booms, slid along and turned."""
    elements, _, _ = float_pass(HEWINGS)
    middle = ((FLOAT_FROM + FLOAT_TO) / 2, FLOAT_Y, FLOAT_Z)
    shift = (along, -drop, 0)

    def moved(element):
        return dict(element, name=f"{name}{element['name']}",
                    **{end: [c + s - m for c, s, m in zip(element[end], shift, middle)] for end in ("from", "to")})

    return box(name, middle, middle, {}, rot=(roll, degrees, 0), children=[moved(element) for element in elements])


def float_forms():
    """The float as its lashings give: slack, then one torn, then all parted. Each form names the elements of the sound
    rig it is drawn instead of."""
    replaced = [*(f"Lash{n}Float{k}" for n in range(1, len(BOOMS) + 1) for k in (1, 2)), f"Hewing{HEWINGS}"]
    children = []
    for form, (drop, along, degrees, roll, parted) in FLOAT_FORMS.items():
        lashings = [part for n, x in enumerate(BOOMS, 1) for part in (frayed if n in parted else slack)(f"Lash{n}Float{form}", x, drop)]
        piece = group(f"FloatLashing{form}", (0, 0, 0), (0, 0, 0), [*lashings, hung(f"Float{form}", drop, along, degrees, roll)])
        piece["replaces"] = replaced
        children.append(piece)
    return group("FloatLashing", (0, 0, 0), (0, 0, 0), children)


def rig(name, children):
    x, y = TACK
    return box(name, (0, 0, 0), (0, 0, 0), {}, origin=(x, y, 8), rot=(0, REACH, 0), children=children)


def reached(point):
    x, _ = TACK
    angle = math.radians(REACH)
    dx, dz = point[0] - x, point[2] - 8
    return (x + dx * math.cos(angle) + dz * math.sin(angle), point[1], 8 - dx * math.sin(angle) + dz * math.cos(angle))


def line(start, end, x):
    return start[1] + (end[1] - start[1]) * (x - start[0]) / (end[0] - start[0])


def head(x):
    return line(TACK, YARD_END, x) if x <= YARD_END[0] else line(YARD_END, SPAR_END, x)


def foot(x):
    return line(TACK, SPAR_END, x)


def sail(name, reefed):
    strips, width = [], (SPAR_END[0] - TACK[0]) / SAIL_STRIPS
    for n in range(SAIL_STRIPS):
        x0 = TACK[0] + n * width
        mid = x0 + width / 2
        top, bottom = head(mid) - 0.6, foot(mid) + 0.6
        if top - bottom < 1:
            continue
        strips.append(box(f"{name}{n + 1}", (x0, bottom + (top - bottom) * reefed, 7.6), (x0 + width, top, 8.4), "sail", grain="y"))
    return rig(name, strips)


def area():
    a = (YARD_END[0] - TACK[0], YARD_END[1] - TACK[1])
    b = (SPAR_END[0] - TACK[0], SPAR_END[1] - TACK[1])
    return abs(a[0] * b[1] - a[1] * b[0]) / 2 * VOXEL_METRES ** 2


def furled():
    cloths = math.ceil(area() / CLOTH_SQUARE_METRES)
    rolls = []
    for n in range(cloths):
        a, b = n / cloths, (n + 1) / cloths
        at = lambda f: (TACK[0] + f * (YARD_END[0] - TACK[0]), TACK[1] + f * (YARD_END[1] - TACK[1]) - 1.4, 8)
        rolls.append([beam(f"SailRoll{n + 1}", at(a), at(b), 2.2, 2.2, "sail", overlap=0)])
    print(f"sail: {area():.1f} m² of crab claw, {cloths} cloths")
    return rig("SailFurled", [group(f"SailFurled{n}", (0, 0, 0), (0, 0, 0), roll) for n, roll in enumerate(rolls, 1)])


def mast():
    x, z = MAST
    return [
        run("MastPole", (x - POLE / 2, 1.4, z - POLE / 2), (x + POLE / 2, MASTHEAD, z + POLE / 2), "frame", axis="y")
        + [box("MastStep", (x - 2, 1, z - 2), (x + 2, 2.4, z + 2), WOOD)],
        [rig("Yard", [beam("YardSpar", (*TACK, 8), (*YARD_END, 8), SPAR, SPAR, "frame")])],
        [rig("Boom", [beam("BoomSpar", (TACK[0], TACK[1] - 0.6, 8), (*SPAR_END, 8), SPAR, SPAR, "frame")])],
    ]


def rope(name, start, end):
    pieces = math.ceil(math.dist(start, end) / 16)
    points = [tuple(s + (e - s) * n / pieces for s, e in zip(start, end)) for n in range(pieces + 1)]
    return path(name, points, 0.3, 0.3, "lashing", overlap=0)


def stays():
    x, z = MAST
    top = (x, MASTHEAD - 1, z)
    return [
        rope("Forestay", top, (-26, RIM + 1, 8)),
        rope("Backstay", top, (42, RIM + 1, 8)),
        rope("Shroud", top, (BOOMS[0] + 1.5, FLOAT_Y + FLOAT_DIAMETER / 2, FLOAT_Z - 1)),
        rope("Sheet", reached((SPAR_END[0] - 1, SPAR_END[1] - 0.6, 8)), (34, RIM + 1, 8)),
    ]


def sheer(hull, x, side):
    _, y, z = hull.at(x, len(PROFILE) - 1, side)
    return (x, y, z)


def strakes(hull):
    planks = []
    for side, label in ((-1, "Port"), (1, "Starboard")):
        x = HOLLOW[0]
        while x < HOLLOW[1] - 1e-6:
            end = min(x + PLANK, HOLLOW[1])
            a, b = (sheer(hull, at, side) for at in (x, end))
            low = lambda point: (point[0], point[1] - 0.6, point[2])
            planks.append([plank(f"Strake{label}{len(planks) + 1}", low(a), low(b), STRAKE + 0.6, STRAKE_THICKNESS, 0, side, "plank", "plank")])
            x = end
    for label, x, inward in (("Stern", HOLLOW[0], 1), ("Bow", HOLLOW[1], -1)):
        half = end_half(hull, x)
        x0, x1 = sorted((x, x - inward * STRAKE_THICKNESS))
        planks.append([box(f"Strake{label}", (x0, RIM - 0.6, CENTRE - half), (x1, RIM + STRAKE, CENTRE + half), "plank")])
    extra = deck() * STRAKE * VOXEL_METRES * 1000
    print(f"washstrakes: {len(planks)} planks of a metre, the sides and a board across each end of her hollow, "
          f"{STRAKE * VOXEL_METRES * 100:.0f} cm raising her side, {extra:.0f} L more hold, {strake_litres(hull):.1f} L of plank")
    return planks


def end_half(hull, x):
    return abs(sheer(hull, x, 1)[2] - CENTRE)


def strake_litres(hull):
    sides = 2 * (HOLLOW[1] - HOLLOW[0])
    ends = sum(2 * end_half(hull, x) for x in HOLLOW)
    return (sides + ends) * (STRAKE + 0.6) * STRAKE_THICKNESS * VOXEL_METRES_CUBED_LITRES


def seams(hull):
    stitches = []
    for side, label in ((-1, "Port"), (1, "Starboard")):
        x, n = HOLLOW[0] + STITCH_PITCH / 2, 1
        while x < HOLLOW[1]:
            _, y, z = sheer(hull, x, side)
            z0, z1 = sorted((z - side * (WALL + 0.3), z + side * (STRAKE_THICKNESS + 0.3)))
            stitches.append(box(f"Stitch{label}{n}", (x - 0.2, y - 0.9, z0), (x + 0.2, y + 0.4, z1), "lashing", grain="y"))
            x, n = x + STITCH_PITCH, n + 1
    for label, x in (("Stern", HOLLOW[0]), ("Bow", HOLLOW[1])):
        half = end_half(hull, x)
        z, n = CENTRE - half + STITCH_PITCH / 2, 1
        while z < CENTRE + half:
            stitches.append(box(f"Stitch{label}{n}", (x - 0.3 - STRAKE_THICKNESS, RIM - 0.9, z - 0.2), (x + 0.3 + STRAKE_THICKNESS, RIM + 0.4, z + 0.2),
                                "lashing", grain="y"))
            z, n = z + STITCH_PITCH, n + 1
    metres = len(stitches) * STITCH_TURN_METRES
    ropes = math.ceil(metres / ROPE_METRES)
    print(f"seams: {len(stitches)} stitches {STITCH_PITCH * VOXEL_METRES * 100:.1f} cm apart, {metres:.0f} m of cord, {ropes} ropes")
    return [stitches[len(stitches) * r // ropes:len(stitches) * (r + 1) // ropes] for r in range(ropes)]


def build(target):
    shape = Shape(target, {
        WOOD: "game:block/wood/debarked/oak",
        "frame": "game:block/wood/debarked/oak",
        "float": "game:block/wood/debarked/oak",
        "floatbark": "game:block/wood/bark/oak-h",
        "floatrings": "game:block/wood/treetrunk/oak",
        "sail": "game:block/cloth/linen/plain",
        "lashing": "game:item/resource/rope",
        "plank": "game:block/wood/planks/oak1",
        "bilge": BILGE,
    })
    hull = Hull("Side", STATIONS, 8, WALL, WOOD, WOOD, edge=WOOD)
    logs, passes = hewing()
    shell = [element for piece in log(hull) for element in piece]
    shape.add(shell)
    shape.add(logs, booms(), passes)
    shape.add(stages("Lashing", lashings()), float_forms())
    shape.add(stages("Mast", mast()))
    shape.add(furled(), sail("SailUnfurled", 0), sail("SailHalf", 0.5))
    shape.add(stages("Stays", stays()))
    shape.add(stages("Strakes", strakes(hull)), stages("Seams", seams(hull)))
    shape.add(hull.hide_water(RIM - 0.3), hull.bilge(RIM - 0.3))
    shape.keep("Hull", "FrontSeat", "BackSeat", source=Path(target).with_name("dugout.json"))
    shape.keep("LowerMastStorage")
    wound(shape, [("", shell, WOOD)])
    shape.write()
    report(hull, passes["children"][-1])


def pole_litres():
    boom_length = sum(math.dist(a, b) for a, b in zip(boom_points(), boom_points()[1:])) * len(BOOMS)
    spars = 2 * SPAR_METRES / VOXEL_METRES
    return (boom_length * BOOM ** 2 + (MASTHEAD - 1.4) * POLE ** 2 + spars * SPAR ** 2) * VOXEL_METRES_CUBED_LITRES


def boom_points():
    end = float_top(HEWINGS) + BOOM / 2
    return [(RIM + 0.6, 0.5), (RIM + 1.4, 14.5), (RIM + 0.6, 30), (end, FLOAT_Z + 2.5)]


def report(hull, finished_float):
    envelope, hold, _ = volumes()
    parts = {"hull": kilograms("wood-oak", envelope - hold), "float": kilograms("wood-oak", finished_float["woodLitres"]),
             "poles": kilograms("wood-oak", pole_litres()), "strakes": kilograms("wood-oak", strake_litres(hull))}
    print("outrigger parts in oak: " + ", ".join(f"{name} {kg:.0f} kg" for name, kg in parts.items()) + f", poles {pole_litres():.0f} L")
    waterplane = afloat("dugout", parts["hull"], RIM * VOXEL_METRES, hold, deck())
    afloat("outrigger", sum(parts.values()), RIM * VOXEL_METRES, hold, deck(), waterplane=waterplane + finished_float["waterlineSquareMetres"])


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
