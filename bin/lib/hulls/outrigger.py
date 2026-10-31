import math
from pathlib import Path

from dugout import CENTRE, ENDS, HOLLOW, PROFILE, RIM, STATIONS, WALL, WOOD, log
from shape import BILGE, Hull, Shape, beam, box, group, path, plank, run, stages

VOXEL_METRES = 1 / 16
ROPE_METRES = 10

FLOAT_Z = 40
FLOAT_Y = 2.9
FLOAT_DIAMETER = 4.8
FLOAT_FROM, FLOAT_TO = -16, 32
TAPER = 6
BOOMS = (9.5, -10, 24)
BOOM = 1.4
MAST = (12, 8)
MASTHEAD = 49
SPAR_METRES = 3
TACK = (-14, 11.5)
REACH = 20
SAIL_STRIPS = 16
CLOTH_SQUARE_METRES = 1

LASHING_TURNS = 13
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


def float_body():
    centre = lambda x: (x, FLOAT_Y, FLOAT_Z)
    body = beam("FloatBody", centre(FLOAT_FROM + TAPER), centre(FLOAT_TO - TAPER), FLOAT_DIAMETER, FLOAT_DIAMETER, "float",
                rounded=True, overlap=0)
    blunt = [[beam(f"BluntEnd{n}", centre(a), centre(b), FLOAT_DIAMETER, FLOAT_DIAMETER, "float", rounded=True, overlap=0)]
             for n, (a, b) in enumerate([(FLOAT_FROM, FLOAT_FROM + TAPER), (FLOAT_TO - TAPER, FLOAT_TO)], 1)]
    return group("Float", (0, 0, 0), (0, 0, 0), [body]), stages("Blunt", blunt)


def hewn_end(name, start, direction):
    points = [(start + direction * step, FLOAT_Y + lift, FLOAT_Z) for step, lift in ((0, 0), (TAPER / 2, 0.5), (TAPER, 1.4))]
    girth = [FLOAT_DIAMETER, FLOAT_DIAMETER * 0.65]
    return path(name, points, lambda i: girth[i - 1], lambda i: girth[i - 1], "float", rounded=True)


def boom(name, x):
    float_top = FLOAT_Y + FLOAT_DIAMETER / 2 + BOOM / 2
    points = [(x, RIM + 0.6, 0.5), (x, RIM + 1.4, 14.5), (x, RIM + 0.6, 30), (x, float_top, FLOAT_Z + 2.5)]
    return path(name, points, BOOM, BOOM, "frame")


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
        run("MastPole", (x - 1, 1.4, z - 1), (x + 1, MASTHEAD, z + 1), "frame", axis="y")
        + [box("MastStep", (x - 2, 1, z - 2), (x + 2, 2.4, z + 2), WOOD)],
        [rig("Yard", [beam("YardSpar", (*TACK, 8), (*YARD_END, 8), 1.2, 1.2, "frame")])],
        [rig("Boom", [beam("BoomSpar", (TACK[0], TACK[1] - 0.6, 8), (*SPAR_END, 8), 1.2, 1.2, "frame")])],
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
    for label, slices in ENDS.items():
        x0, x1 = sorted((min(piece[0] for piece in slices), max(piece[1] for piece in slices)))
        half = slices[0][2]
        planks.append([box(f"Strake{label}", (x0, RIM - 0.6, CENTRE - half), (x1, RIM + STRAKE, CENTRE + half), "plank")])
    extra = 2 * (GUNWALE - WALL) * STRAKE * (HOLLOW[1] - HOLLOW[0]) * VOXEL_METRES ** 3 * 1000
    print(f"washstrakes: {len(planks)} planks of a metre, the sides and an end piece at bow and stern, {STRAKE * VOXEL_METRES * 100:.0f} cm raising her side, {extra:.0f} L more hold")
    return planks


def seams(hull):
    stitches = []
    for side, label in ((-1, "Port"), (1, "Starboard")):
        x, n = HOLLOW[0] + STITCH_PITCH / 2, 1
        while x < HOLLOW[1]:
            _, y, z = sheer(hull, x, side)
            z0, z1 = sorted((z - side * (WALL + 0.3), z + side * (STRAKE_THICKNESS + 0.3)))
            stitches.append(box(f"Stitch{label}{n}", (x - 0.2, y - 0.9, z0), (x + 0.2, y + 0.4, z1), "lashing", grain="y"))
            x, n = x + STITCH_PITCH, n + 1
    for label, slices in ENDS.items():
        x0, x1 = sorted((min(piece[0] for piece in slices), max(piece[1] for piece in slices)))
        half = slices[0][2]
        for side in (-1, 1):
            x, n = x0 + STITCH_PITCH / 2, 1
            while x < x1:
                z = CENTRE + side * half
                z0, z1 = sorted((z - side * 0.3, z + side * 0.3))
                stitches.append(box(f"Stitch{label}{'Port' if side < 0 else 'Starboard'}{n}", (x - 0.2, RIM - 0.9, z0), (x + 0.2, RIM + 0.4, z1),
                                    "lashing", grain="y"))
                x, n = x + STITCH_PITCH, n + 1
    metres = len(stitches) * STITCH_TURN_METRES
    ropes = math.ceil(metres / ROPE_METRES)
    print(f"seams: {len(stitches)} stitches {STITCH_PITCH * VOXEL_METRES * 100:.1f} cm apart, {metres:.0f} m of cord, {ropes} ropes")
    return [stitches[len(stitches) * r // ropes:len(stitches) * (r + 1) // ropes] for r in range(ropes)]


def build(target):
    shape = Shape(target, {
        WOOD: "game:block/wood/debarked/oak",
        "frame": "game:block/wood/debarked/oak",
        "float": "game:block/wood/debarked/oak",
        "sail": "game:block/cloth/linen/plain",
        "lashing": "game:item/resource/rope",
        "plank": "game:block/wood/planks/oak1",
        "bilge": BILGE,
    })
    hull = Hull("Side", STATIONS, 8, WALL, WOOD, WOOD, edge=WOOD)
    shape.add(*log(hull))
    shape.add(*float_body())
    shape.add(stages("Booms", [boom(f"Boom{n}x", x) for n, x in enumerate(BOOMS, 1)]))
    shape.add(stages("Lashing", lashings()))
    shape.add(stages("Ends", [hewn_end("HewnBow", FLOAT_FROM + TAPER, -1), hewn_end("HewnStern", FLOAT_TO - TAPER, 1)]))
    shape.add(stages("Mast", mast()))
    shape.add(furled(), sail("SailUnfurled", 0), sail("SailHalf", 0.5))
    shape.add(stages("Stays", stays()))
    shape.add(stages("Strakes", strakes(hull)), stages("Seams", seams(hull)))
    shape.add(hull.hide_water(RIM - 0.3), hull.bilge(RIM - 0.3))
    shape.keep("Hull", "FrontSeat", "BackSeat", source=Path(target).with_name("dugout.json"))
    shape.keep("LowerMastStorage")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
