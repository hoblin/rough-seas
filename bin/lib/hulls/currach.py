import math

from shape import BILGE, Hull, Shape, box, group
from stroke import crew, moved, strokes

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


TURN = 8.3
STATION = 3.5
BULL = 2.4
SHIPPED = (7.45, 5)
FRAMES = 30
OARS = {"OarStarboard": -1, "OarPort": 1}


def oar(name, side):
    return group(name, shipped(side), (side * 90, 0, 0), [
        box(f"{name}Grip", (-8, -0.35, -0.35), (-5, 0.35, 0.35), "frame"),
        box(f"{name}Loom", (-5, -0.45, -0.45), (11, 0.45, 0.45), "frame"),
        box(f"{name}Bull", (-1.6, -BULL, -0.7), (1.6, -0.45, 0.7), "frame"),
        box(f"{name}Shaft", (11, -0.3, -0.3), (21, 0.3, 0.3), "frame"),
        box(f"{name}Blade", (21, -0.6, -0.2), (27, 0.6, 0.2), "frame"),
    ])


def rowlock(hull, side):
    x, y, z = hull.at(2 * TURN - STATION, len(PROFILE) - 1, side)
    pin = box(f"Thole{'Port' if side > 0 else 'Starboard'}", (STATION - 0.3, y - 0.6, z - 0.3),
              (STATION + 0.3, y + BULL + 1.2, z + 0.3), "frame", grain="y")
    return pin, (STATION, y + 0.4 + BULL, z)


def shipped(side):
    y, inboard = SHIPPED
    return (0, y, 8 + side * inboard)


def sweep(u):
    return -math.cos(2 * math.pi * u)


def depth(u):
    return max(0.0, min(1.0, 0.5 + 1.6 * math.sin(2 * math.pi * u)))


def rowing(tholes):
    def pose(sweep_at, depth_at):
        def at(frame):
            u = frame / FRAMES
            yaw = 90 - 28 * sweep_at(u)
            pitch = -13 - 11 * depth_at(u)
            return {name: moved(shipped(side), (side * 90, 0, 0), tholes[name],
                                {"rotationY": -side * yaw, "rotationZ": pitch})
                    for name, side in OARS.items()}
        return at

    return strokes("currach", FRAMES, pose(lambda u: 0, lambda u: 0), pose(sweep, depth),
                   pose(lambda u: sweep(1 - u), lambda u: depth(1 - u)))


def build(path):
    shape = Shape(path, {
        "hide": "game:block/leather/poor",
        "frame": "game:block/wood/debarked/oak",
        "bilge": BILGE,
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
    shape.add(box("StemPost", (38.4, 5, 7.3), (40.6, 11.2, 8.7), "frame", grain="y"))
    shape.add(hull.hide_water(9.2), hull.bilge(9.2))
    shape.turn_about(TURN)
    tholes = {}
    for name, side in OARS.items():
        pin, tholes[name] = rowlock(hull, side)
        shape.add(pin, oar(name, side))
    shape.keep("Hull", "FrontSeat", "BackSeat", "ForeCargo", "AftCargo")
    crew(shape, "currach", "BackSeat", "sitboatidle", rowing(tholes), {"R": "OarStarboardGrip", "L": "OarPortGrip"},
         {"rotationZ": 12})


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
