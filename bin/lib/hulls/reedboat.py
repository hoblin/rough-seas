import math

from shape import Shape, box, group, path
from stroke import Rower, fit, moved, player_patch, rowed, seat

XS = [-15.2, -12.6, -8, -1, 8, 17, 24, 28.6, 31.2]
RISE = [8.4, 6.6, 4.6, 3.6, 3.4, 3.6, 4.6, 6.6, 8.4]
SPREAD = [0.3, 2.2, 4, 4.6, 4.6, 4.6, 4, 2.2, 0.3]
GIRTH = [2.4, 3.6, 4.8, 5.2, 5.2, 4.8, 3.6, 2.4]
TIES = {1: (0.3, 0.7), 2: (0.5,), 7: (0.5,), 8: (0.3, 0.7)}
STOWED = ((-6, 5.6, 2.2), (0, -3, 1.5))
FRAMES = 30


def paddle(at, rot):
    return group("Paddle", at, rot, [
        box("PaddleHandle", (-10.5, -0.5, -1), (-9, 0.9, 1), "paddle", grain="z"),
        box("PaddleTop", (-9, -0.2, -0.2), (-6, 0.6, 0.6), "paddle"),
        box("PaddleShaft", (-6, -0.15, -0.15), (10, 0.55, 0.55), "paddle"),
        box("PaddleThroat", (-1, -0.2, -0.2), (1, 0.6, 0.6), "paddle"),
        box("PaddleBlade", (10, -1.2, -0.05), (18.5, 1.9, 0.45), "paddle"),
    ])


def strokes():
    origin, rest = STOWED

    def at(frame):
        u = frame / FRAMES
        drive = u < 0.55
        f = u / 0.55 if drive else (u - 0.55) / 0.45
        ease = 0.5 - 0.5 * math.cos(math.pi * f)
        reach = ease if drive else 1 - ease
        lift = 0 if drive else math.sin(math.pi * f)
        hand = (-1 + 12 * reach, 11.5 + 1.5 * lift, 0.5 - 1.5 * lift)
        yaw = 110 - 45 * reach
        pitch = -72 + 34 * lift
        return {"Paddle": moved(origin, rest, hand, {"rotationY": yaw, "rotationZ": pitch})}

    def held(frame):
        return {"Paddle": moved(origin, rest, (4, 14, 1.5), {"rotationY": 92, "rotationZ": -40})}

    return [
        {"code": "roughseas-paddle-ready", "frames": FRAMES, "keys": [0, FRAMES // 2], "pose": held},
        {"code": "roughseas-paddle-forward", "frames": FRAMES, "keys": list(range(0, FRAMES, 2)), "pose": at},
        {"code": "roughseas-paddle-backward", "frames": FRAMES, "keys": list(range(0, FRAMES, 2)),
         "pose": lambda frame: at(FRAMES - frame)},
    ]


def side(name, direction):
    points = [(x, y, 8 + direction * s) for x, y, s in zip(XS, RISE, SPREAD)]
    return [
        segment
        for i, (a, b) in enumerate(zip(points, points[1:]), 1)
        for segment in path(f"{name}{i}x", [a, b], GIRTH[i - 1], GIRTH[i - 1], "reed",
                            ties=TIES.get(i, (0.5,)), tie_tex="rope", rounded=True)
    ]


def build(target):
    shape = Shape(target, {
        "reed": "roughseas:entity/vessel/reed",
        "rope": "game:block/cloth/reedrope",
        "paddle": "game:block/wood/debarked/birch",
    })
    shape.add(side("Port", -1), side("Starboard", 1))
    core = [(-9, 4.2, 8), (-3, 2.6, 8), (8, 2.4, 8), (19, 2.6, 8), (25, 4.2, 8)]
    shape.add(path("Core", core, lambda i: (4.2, 6, 6, 4.2)[i - 1], lambda i: (3.6, 4.8, 4.8, 3.6)[i - 1], "reed",
                   rounded=True))
    for n, x in enumerate((-3.5, 4.5, 12.5, 19.5), 1):
        shape.add(box(f"Binding{n}", (x, 0.2, 2.9), (x + 0.8, 6.3, 13.1),
                      {"north": "rope", "south": "rope", "up": "rope", "down": "rope"}, grain="z"))
    shape.add(paddle(*STOWED))
    shape.keep("Hull", "FrontSeat", "Cargo")
    rower = Rower(seat(shape, "FrontSeat"), "roughseas-sitlegsforward")
    vessel, player = fit(rower, shape.elements, strokes(), {"R": "PaddleThroat", "L": "PaddleHandle"},
                         {"rotationZ": 6})
    shape.animations = vessel
    shape.write()
    player_patch("player-paddle", player)
    rowed(shape, rower, vessel, player)


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
