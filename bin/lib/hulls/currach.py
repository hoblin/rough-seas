from shape import Hull, Shape, box, group

PROFILE = [(0, 0), (5.6, 0), (7.3, 2.8), (8.4, 6.6), (8.8, 9.6)]
SHEER = 9.6


def section(x, breadth, rocker, rise):
    top = SHEER + rise
    return (x, [(h * breadth, rocker + y * (top - rocker) / SHEER) for h, y in PROFILE])


STATIONS = [
    section(-23, 0.8, 0.8, 0.2),
    section(-16, 0.93, 0.25, 0),
    section(-8, 1, 0, 0),
    section(4, 1, 0, 0),
    section(16, 0.96, 0.05, 0.1),
    section(25, 0.82, 0.5, 0.4),
    section(32, 0.56, 1.6, 0.8),
    section(37, 0.27, 3.4, 1.1),
    section(40, 0.04, 5.6, 1.3),
]


def oar(name, at, yaw, pitch):
    return group(name, at, (0, yaw, pitch), [
        box(f"{name}Loom", (0, 0, 0), (16, 0.8, 0.8), "frame"),
        box(f"{name}Shaft", (16, 0.1, 0.1), (28, 0.7, 0.7), "frame"),
        box(f"{name}Blade", (28, 0.15, 0.05), (34, 0.65, 0.75), "frame"),
        box(f"{name}Bull", (5, -0.4, -0.4), (8, 1.2, 1.2), "frame"),
    ])


def build(path):
    shape = Shape(path, {
        "hide": "game:block/leather/poor",
        "frame": "game:block/wood/debarked/oak",
    })
    hull = Hull("Skin", STATIONS, 8, 0.5, "hide", "hide", edge="hide")
    shape.add(hull.strakes())
    shape.add(hull.transom("Transom", -22.5, 0.8, "hide"))
    shape.add(hull.rail("Outwale", "frame", height=0.9, thickness=0.7, lift=-0.5))
    shape.add(hull.rail("Inwale", "frame", height=0.7, thickness=1.6, lift=-1.4))
    for n, x in enumerate(range(-20, 38, 3), 1):
        shape.add(hull.rib(f"Lath{n}x", x, "frame", width=0.8, depth=0.4))
    for n, x in enumerate(range(-20, 24, 2), 1):
        (_, y), (half, _) = hull.section(x + 0.7)[:2]
        shape.add(box(f"Floor{n}", (x, y + 0.4, 8 - half + 0.6), (x + 1.4, y + 0.8, 8 + half - 0.6), "frame", grain="z"))
    for n, (x, y) in enumerate([(-15, 6.2), (-2, 6.2), (12, 6.2), (26, 6.6)], 1):
        half = hull.section(x + 0.8)[-1][0] - 1
        shape.add(box(f"Thwart{n}", (x, y, 8 - half), (x + 1.6, y + 0.8, 8 + half), "frame", grain="z"))
    top = len(PROFILE) - 1
    for side, label in ((-1, "Port"), (1, "Starboard")):
        for n, x in enumerate((-10, 6), 1):
            _, y, z = hull.at(x, top, side)
            shape.add(box(f"Thole{label}{n}", (x, y - 0.2, z - 0.35), (x + 0.6, y + 2.2, z + 0.35), "frame", grain="y"))
    shape.add(box("StemPost", (38.4, 5, 7.3), (40.6, 11.2, 8.7), "frame", grain="y"))
    shape.add(oar("OarPort", (-14, 7.6, 3.6), 1.5, -1), oar("OarStarboard", (-12, 7.4, 12.4), -1.5, -1))
    shape.add(hull.hide_water(9.2))
    shape.turn_about(8.3)
    shape.keep("Hull", "FrontSeat", "BackSeat")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
