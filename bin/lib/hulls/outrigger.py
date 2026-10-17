import math

from shape import BILGE, Hull, Shape, beam, box, group, paddle, path, run

PROFILE = [(0, 0), (2.6, 0.4), (3.8, 2.2), (4.1, 5), (4.2, 7.6)]


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / 7.6)) for h, y in PROFILE])


STATIONS = [
    section(-29, 0.2, 3.4),
    section(-25, 0.55, 1.6),
    section(-18, 0.85, 0.5),
    section(-8, 1, 0),
    section(4, 1, 0),
    section(16, 1, 0),
    section(28, 1, 0),
    section(36, 0.85, 0.5),
    section(42, 0.55, 1.6),
    section(45.5, 0.2, 3.4),
]
FLOAT = 32.5
MAST = (12, 8)
MASTHEAD = 55
REACH = 40


def prow(name, x, direction):
    pieces = []
    for n, (lean, low, high, half) in enumerate([(0, 3, 9, 1.2), (1.4, 6, 10.6, 1), (2.6, 8.6, 11.6, 0.8)], 1):
        a, b = sorted((x + direction * lean, x + direction * (lean + 1.8)))
        pieces.append(box(f"{name}{n}", (a, low, 8 - half), (b, high, 8 + half), "inside", grain="y"))
    return pieces


def outrigger_float():
    points = [(-21, 4.6, FLOAT), (-16, 2.6, FLOAT), (-6, 2, FLOAT), (10, 2, FLOAT), (26, 2, FLOAT), (34, 2.6, FLOAT),
              (38.5, 4.6, FLOAT)]
    girth = [2.2, 4, 4.6, 4.6, 4, 2.2]
    return path("Float", points, lambda i: girth[i - 1], lambda i: girth[i - 1] * 0.9, "bark", rounded=True)


def boom(name, x):
    points = [(x, 8.4, 5.6), (x, 9.4, 14), (x, 9.6, 23), (x, 8.8, FLOAT)]
    pieces = path(name, points, 1.4, 1.2, "inside", ties=(0.1,), tie=(0.7, 0.3), tie_tex="lashing")
    for n, lean in enumerate((-1.6, 1.6), 1):
        pieces.append(beam(f"{name}Stanchion{n}", (x + lean, 2.8, FLOAT), (x, 8.6, FLOAT), 0.7, 0.7, "inside",
                           ties=(0.85,), tie=(0.6, 0.3), tie_tex="lashing", grain="x"))
    return pieces


def sail(name, low, high):
    strips = []
    for n, (x0, x1) in enumerate([(-6, 6), (6, 18), (18, 30)], 1):
        strips.append(box(f"{name}{n}", (x0, low, 7.6), (x1, high, 8.4), "sail", grain="y"))
    return strips


def rig(name, children):
    x, z = MAST
    return box(name, (0, 0, 0), (0, 0, 0), {}, origin=(x, 0, z), rot=(0, REACH, 0), children=children)


def reached(point):
    x, z = MAST
    angle = math.radians(REACH)
    dx, dz = point[0] - x, point[2] - z
    return (x + dx * math.cos(angle) + dz * math.sin(angle), point[1], z - dx * math.sin(angle) + dz * math.cos(angle))


def rope(name, start, end):
    pieces = math.ceil(math.dist(start, end) / 16)
    points = [tuple(s + (e - s) * n / pieces for s, e in zip(start, end)) for n in range(pieces + 1)]
    return path(name, points, 0.3, 0.3, "lashing", overlap=0)


def build(target):
    shape = Shape(target, {
        "bark": "game:block/wood/bark/oak-h",
        "inside": "game:block/wood/debarked/oak",
        "sail": "game:block/cloth/linen/plain",
        "lashing": "game:item/resource/rope",
        "bilge": BILGE,
    })
    hull = Hull("Log", STATIONS, 8, 1.2, "bark", "inside", edge="inside")
    shape.add(hull.strakes())
    shape.add(hull.rail("Washstrake", "inside", height=1.4, thickness=0.8, lift=-0.2))
    shape.add(prow("SternPost", -28.4, -1), prow("BowPost", 44.6, 1))
    shape.add(outrigger_float())
    shape.add(boom("BoomFore", 3), boom("BoomAft", 21))
    shape.add(run("Platform", (4.4, 9.6, 12), (19.6, 10, 20), "inside", axis="x"))
    for n, z in enumerate((12.6, 16, 19.4), 1):
        shape.add(box(f"PlatformBearer{n}", (3, 9, z - 0.5), (22, 9.6, z + 0.5), "inside"))
    x, z = MAST
    shape.add(run("Mast", (x - 1, 1.4, z - 1), (x + 1, MASTHEAD, z + 1), "inside", axis="y"))
    shape.add(box("MastStep", (x - 2, 1, z - 2), (x + 2, 2.4, z + 2), "inside"))
    shape.add(box("MastPartner", (x - 2.4, 7.4, z - 3.6), (x + 2.4, 8.4, z + 3.6), "inside", grain="z"))
    shape.add(rig("Yard", run("Yard", (-8, 52, 7.5), (32, 53, 8.5), "inside")))
    shape.add(
        rope("Forestay", (x, MASTHEAD - 1, z), (44.5, 9, 8)),
        rope("Backstay", (x, MASTHEAD - 1, z), (-29.5, 9, 8)),
        rope("Shroud", (x, MASTHEAD - 1, z), (x, 10, FLOAT - 1)),
        rope("Sheet", reached((-7, 15.5, 8)), (-22, 9.4, 8)),
    )
    shape.add(
        rig("SailUnfurled", sail("SailUnfurled", 16, 52) + [
            *run("SailUnfurledBoom", (-8, 15, 7.4), (32, 16, 8.6), "inside")]),
        rig("SailHalf", sail("SailHalf", 34, 52) + [
            *run("SailHalfBoom", (-8, 33, 7.4), (32, 34, 8.6), "inside")]),
        rig("SailFurled", [
            box("SailRoll1", (-6, 49.5, 7), (10, 52, 9), "sail"),
            box("SailRoll2", (10, 49.5, 7), (26, 52, 9), "sail"),
            box("SailRoll3", (26, 49.5, 7), (30, 52, 9), "sail"),
        ] + [box(f"SailGasket{n}", (gx, 49.2, 6.7), (gx + 0.6, 52.3, 9.3), "lashing") for n, gx in
             enumerate((-3, 5, 13, 21, 28), 1)]),
    )
    shape.add(paddle((-22, 2.6, 9.4), (0, -2, 3), 14, 7, "inside"))
    shape.add(hull.hide_water(7.3), hull.bilge(7.3))
    shape.keep("LowerMastStorage", "Hull", "FrontSeat", "BackSeat")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
