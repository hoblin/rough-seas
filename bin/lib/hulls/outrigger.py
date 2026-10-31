import math
from pathlib import Path

from dugout import RIM, STATIONS, WALL, WOOD, log
from shape import BILGE, Hull, Shape, beam, box, group, path, run, stages

VOXEL_METRES = 1 / 16
ROPE_METRES = 10

FLOAT_Z = 40
FLOAT_Y = 2.9
FLOAT_DIAMETER = 4.8
FLOAT_FROM, FLOAT_TO = -17, 31
TAPER = 6
BOOMS = (9.5, -10, 24)
BOOM = 1.4
MAST = (12, 8)
MASTHEAD = 49
TACK = (-14, 11.5)
YARD_END = (30, 52)
SPAR_END = (38, 15)
REACH = 20
SAIL_STRIPS = 16
CLOTHS = 4

LASHING_TURNS = 13
GUNWALE_METRES = WALL * VOXEL_METRES


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
        for side, z in (("Port", 8 - 6), ("Starboard", 8 + 6)):
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
    rolls = []
    for n in range(CLOTHS):
        a, b = n / CLOTHS, (n + 1) / CLOTHS
        at = lambda f: (TACK[0] + f * (YARD_END[0] - TACK[0]), TACK[1] + f * (YARD_END[1] - TACK[1]) - 1.4, 8)
        rolls.append([beam(f"SailRoll{n + 1}", at(a), at(b), 2.2, 2.2, "sail", overlap=0)])
    print(f"sail: {area():.1f} m² of crab claw, {CLOTHS} cloths")
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
        rope("Forestay", top, (-21, RIM + 1, 8)),
        rope("Backstay", top, (35, RIM + 1, 8)),
        rope("Shroud", top, (BOOMS[0] + 1.5, RIM + 1.4, 30)),
        rope("Sheet", reached((SPAR_END[0] - 1, SPAR_END[1] - 0.6, 8)), (28, RIM + 1, 8)),
    ]


def build(target):
    shape = Shape(target, {
        WOOD: "game:block/wood/debarked/oak",
        "frame": "game:block/wood/debarked/oak",
        "float": "game:block/wood/debarked/oak",
        "sail": "game:block/cloth/linen/plain",
        "lashing": "game:item/resource/rope",
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
    shape.add(hull.hide_water(7.7), hull.bilge(7.7))
    shape.keep("Hull", "FrontSeat", "BackSeat", source=Path(target).with_name("dugout.json"))
    shape.keep("LowerMastStorage")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
