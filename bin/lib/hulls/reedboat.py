from shape import Shape, box, group, path
from stroke import crew, paddling

XS = [-15.2, -12.6, -8, -1, 8, 17, 24, 28.6, 31.2]
RISE = [8.4, 6.6, 4.6, 3.6, 3.4, 3.6, 4.6, 6.6, 8.4]
SPREAD = [0.3, 2.2, 4, 4.6, 4.6, 4.6, 4, 2.2, 0.3]
GIRTH = [2.4, 3.6, 4.8, 5.2, 5.2, 4.8, 3.6, 2.4]
TIES = {1: (0.3, 0.7), 2: (0.5,), 7: (0.5,), 8: (0.3, 0.7)}
STOWED = ((-6, 5.6, 2.2), (0, -3, 1.5))


def side(name, direction):
    points = [(x, y, 8 + direction * s) for x, y, s in zip(XS, RISE, SPREAD)]
    return [
        segment
        for i, (a, b) in enumerate(zip(points, points[1:]), 1)
        for segment in path(f"{name}{i}x", [a, b], GIRTH[i - 1], GIRTH[i - 1], "reed",
                            ties=TIES.get(i, (0.5,)), tie_tex="rope", rounded=True)
    ]


def paddle(at, rot):
    return group("Paddle", at, rot, [
        box("PaddleHandle", (-10.5, -0.5, -1), (-9, 0.9, 1), "paddle", grain="z"),
        box("PaddleTop", (-9, -0.2, -0.2), (-6, 0.6, 0.6), "paddle"),
        box("PaddleShaft", (-6, -0.15, -0.15), (10, 0.55, 0.55), "paddle"),
        box("PaddleThroat", (-1, -0.2, -0.2), (1, 0.6, 0.6), "paddle"),
        box("PaddleBlade", (10, -0.05, -1.2), (18.5, 0.45, 1.9), "paddle"),
    ])


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
    crew(shape, "reedboat", "FrontSeat", "roughseas-sitlegsforward",
         paddling("reedboat", STOWED, (0, 0.2, 0.2), -1, 11, 11.5, 0.5), {"R": "PaddleThroat", "L": "PaddleHandle"},
         {"rotationZ": 6})


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
