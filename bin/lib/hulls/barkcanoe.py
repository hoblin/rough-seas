from shape import Hull, Shape, box, group

PROFILE = [(0, 0), (2.6, 0.3), (4.6, 1.5), (5.7, 3.4), (6, 6)]
SHEER = 6


def section(x, breadth, rocker, rise):
    top = SHEER + rise
    return (x, [(h * breadth, rocker + y * (top - rocker) / SHEER) for h, y in PROFILE])


STATIONS = [
    section(-26, 0.06, 4.2, 3.6),
    section(-23, 0.4, 2.2, 1.9),
    section(-16, 0.75, 0.7, 0.7),
    section(-8, 0.93, 0.1, 0.2),
    section(0, 1, 0, 0),
    section(8, 1, 0, 0),
    section(16, 1, 0, 0),
    section(24, 0.93, 0.1, 0.2),
    section(32, 0.75, 0.7, 0.7),
    section(39, 0.4, 2.2, 1.9),
    section(42, 0.06, 4.2, 3.6),
]


def stem(name, x, direction):
    pieces = []
    rises = [(0, 3.6, 6.5), (0.6, 6.5, 8.4), (1.1, 8.4, 9.7), (1.2, 9.7, 10.5)]
    for n, (lean, low, high) in enumerate(rises, 1):
        x0 = x + direction * lean
        a, b = sorted((x0, x0 + direction * 1.6))
        pieces.append(box(f"{name}{n}", (a, low, 7.4), (b, high, 8.6), "bark", grain="y"))
    a, b = sorted((x + direction * 0.4, x + direction * 2.8))
    pieces.append(box(f"{name}Head", (a, 9.6, 7.2), (b, 10.4, 8.8), "bark"))
    return pieces


def build(path):
    shape = Shape(path, {
        "bark": "game:block/wood/bark/birch-h",
        "frame": "game:block/wood/debarked/birch",
        "lashing": "game:item/resource/rope",
    })
    hull = Hull("Skin", STATIONS, 8, 0.6, "bark", "frame", edge="bark")
    shape.add(hull.strakes())
    shape.add(hull.rail("Gunwale", "frame", height=0.8, thickness=1.4, lift=-0.2))
    for n, x in enumerate(range(-18, 36, 4), 1):
        shape.add(hull.rib(f"Rib{n}x", x, "frame", width=1.2, depth=0.5))
    shape.add(stem("SternStem", -25.2, -1), stem("BowStem", 41.2, 1))
    for n, (x, half, y) in enumerate([(-8, 5.4, 5.6), (8, 5.8, 5.4), (24, 5.4, 5.6)], 1):
        shape.add(box(f"Thwart{n}", (x, y, 8 - half), (x + 1.4, y + 0.8, 8 + half), "frame", grain="z"))
    top = len(PROFILE) - 1
    for side, label in ((-1, "Port"), (1, "Starboard")):
        for n, x in enumerate(range(-18, 36, 5), 1):
            _, y, z = hull.at(x, top, side)
            shape.add(box(f"Lashing{label}{n}", (x, y - 1.1, z - 0.8), (x + 0.5, y + 0.45, z + 0.8), "lashing", grain="y"))
    shape.add(group("Paddle", (-2, 1, 6.5), (0, 5, 3), [
        box("PaddleShaft", (0, 0, 0), (15, 0.7, 0.7), "frame"),
        box("PaddleBlade", (15, -0.1, -1.2), (22, 0.5, 1.9), "frame"),
        box("PaddleGrip", (-1.3, -0.2, -0.5), (0, 0.9, 1.2), "frame"),
    ]))
    shape.add(hull.hide_water(5.7))
    shape.keep("Hull", "FrontSeat", "BackSeat")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
