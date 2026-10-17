from shape import Shape, box, group, path

XS = [-15.2, -12.6, -8, -1, 8, 17, 24, 28.6, 31.2]
RISE = [8.4, 6.6, 4.6, 3.6, 3.4, 3.6, 4.6, 6.6, 8.4]
SPREAD = [0.3, 2.2, 4, 4.6, 4.6, 4.6, 4, 2.2, 0.3]
GIRTH = [2.4, 3.6, 4.8, 5.2, 5.2, 4.8, 3.6, 2.4]
TIES = {1: (0.3, 0.7), 2: (0.5,), 7: (0.5,), 8: (0.3, 0.7)}


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
        "pole": "game:block/wood/debarked/birch",
    })
    shape.add(side("Port", -1), side("Starboard", 1))
    core = [(-9, 4.2, 8), (-3, 2.6, 8), (8, 2.4, 8), (19, 2.6, 8), (25, 4.2, 8)]
    shape.add(path("Core", core, lambda i: (4.2, 6, 6, 4.2)[i - 1], lambda i: (3.6, 4.8, 4.8, 3.6)[i - 1], "reed",
                   rounded=True))
    for n, x in enumerate((-3.5, 4.5, 12.5, 19.5), 1):
        shape.add(box(f"Binding{n}", (x, 0.2, 2.9), (x + 0.8, 6.3, 13.1),
                      {"north": "rope", "south": "rope", "up": "rope", "down": "rope"}, grain="z"))
    shape.add(group("Pole", (-6, 5.6, 6.2), (0, -3, 1.5), [
        box("PoleShaft", (0, 0, 0), (30, 0.7, 0.7), "pole"),
        box("PoleFoot", (30, -0.1, -0.1), (31.2, 0.8, 0.8), "pole"),
    ]))
    shape.keep("Hull", "FrontSeat", "Cargo")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
