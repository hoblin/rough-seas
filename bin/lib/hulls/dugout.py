from shape import BILGE, Hull, Shape, box, paddle

PROFILE = [(0, 0), (3.5, 0.4), (5.4, 2.4), (6, 5.4), (5.8, 8)]
CUT = "inside"


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / 8)) for h, y in PROFILE])


def end(name, x0, x1, direction):
    outer = "east" if direction > 0 else "west"
    inner = "west" if direction > 0 else "east"
    sides = {"north": "bark", "south": "bark", "down": "bark", "up": CUT, outer: "bark", inner: CUT}
    tip = x1 if direction > 0 else x0

    def ahead(near, far):
        return (tip + near, tip + far) if direction > 0 else (tip - far, tip - near)

    nose_x, lug_x = ahead(-0.5, 1.2), ahead(0.5, 2.3)
    return [
        box(f"{name}Upper", (x0, 5, 4.6), (x1, 8.6, 11.4), sides),
        box(f"{name}Lower", (x0, 2, 5.4), (x1, 5, 10.6), sides),
        box(f"{name}Foot", (x0, 1.2, 6.3), (x1, 2, 9.7), "bark"),
        box(f"{name}Nose", (nose_x[0], 3, 5.8), (nose_x[1], 8.3, 10.2), {**sides, "up": CUT}),
        box(f"{name}Lug", (lug_x[0], 6.2, 6.8), (lug_x[1], 8, 9.2), {**sides, outer: CUT}),
    ]


def rib(name, x):
    return [
        box(f"{name}Low", (x, 1, 5.2), (x + 1.5, 2.6, 10.8), CUT, grain="z"),
        box(f"{name}High", (x, 2.6, 3.8), (x + 1.5, 4.2, 12.2), CUT, grain="z"),
    ]


def build(path):
    shape = Shape(path, {"bark": "game:block/wood/bark/oak-h", "inside": "game:block/wood/debarked/oak", "bilge": BILGE})
    stations = [
        section(-19, 0.5, 2.0),
        section(-14, 0.78, 0.8),
        section(-6, 0.96, 0.15),
        section(2, 1, 0),
        section(10, 1, 0),
        section(18, 0.96, 0.15),
        section(26, 0.78, 0.8),
        section(33, 0.5, 2.0),
    ]
    hull = Hull("Log", stations, 8, 1.6, "bark", CUT, edge=CUT)
    shape.add(hull.strakes())
    shape.add(end("Stern", -22.5, -19, -1), end("Bow", 33, 36.5, 1))
    shape.add(
        rib("RibAft", -4.5),
        rib("RibFore", 19),
        box("KnotPort", (6, 3, 1.4), (7.5, 4.5, 2.4), "bark"),
        box("KnotStarboard", (24, 5, 13.6), (25, 6, 14.6), "bark"),
        paddle((-2, 1.4, 9.8), (0, -4, 2.5), 17, 6, CUT),
    )
    shape.add(hull.hide_water(7.7), hull.bilge(7.7))
    shape.keep("Hull", "FrontSeat", "BackSeat")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
