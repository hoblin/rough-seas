from shape import BILGE, Hull, Shape, box, breadth, group, water_plane

PROFILE = [(0, 0), (3.2, 0.35), (5.2, 1.8), (5.9, 4.2), (6, 8)]
WOOD = "inside"
RIM = 8
WALL = 1.2
HOLLOWINGS = 6
SLAB = 1
CHAR_LIFT = 0.02
CLAY = 0.5
EMBERS = 0.6
BED = 0.55
GLOW = 160
ENDS = {
    "Stern": [(-19.5, -17, 4.2, 1.4), (-21.5, -19.5, 3.4, 2.4), (-23, -21.5, 2.4, 3.6)],
    "Bow": [(31, 33.5, 4.2, 1.4), (33.5, 35.5, 3.4, 2.4), (35.5, 37, 2.4, 3.6)],
}


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / RIM)) for h, y in PROFILE])


STATIONS = [
    section(-17, 0.72, 1.2),
    section(-11, 0.92, 0.4),
    section(-3, 1, 0),
    section(7, 1, 0),
    section(17, 1, 0),
    section(25, 0.92, 0.4),
    section(31, 0.72, 1.2),
]


def log(hull):
    return [hull.strakes(), *(end(name, slices, hull.cz) for name, slices in ENDS.items())]


def end(name, slices, centre):
    blocks = []
    for n, (x0, x1, half, bottom) in enumerate(slices, 1):
        waist = bottom + (RIM - bottom) / 3
        blocks.append(box(f"{name}{n}Upper", (x0, waist, centre - half), (x1, RIM, centre + half), WOOD))
        blocks.append(box(f"{name}{n}Lower", (x0, bottom, centre - half * 0.7), (x1, waist, centre + half * 0.7), WOOD))
    return blocks


def level(floor, n):
    return RIM - (RIM - floor) * n / HOLLOWINGS


def blank(hull, floor):
    levels, chars, woods, fires = [], [], [], []
    for n in range(HOLLOWINGS):
        height = level(floor, n)
        slabs, planes, embers = [], [], []
        for i, ((x0, edges0), (x1, edges1)) in enumerate(zip(hull.stations, hull.stations[1:]), 1):
            half = min(breadth(edges0, height - SLAB), breadth(edges1, height - SLAB)) - hull.t
            if height - SLAB > max(edges0[0][1], edges1[0][1]) + hull.t and half > 0:
                slabs.append(box(f"Blank{n}x{i}", (x0, height - SLAB, hull.cz - half), (x1, height, hull.cz + half), WOOD))
                planes.append(water_plane(f"Char{n}x{i}", x0, x1, hull.cz - half, hull.cz + half, height + CHAR_LIFT, "char"))
            if half > 1:
                embers.append(glowing(box(f"Fire{n}Embers{i}", (x0 + 0.5, height, hull.cz - half * BED),
                                          (x1 - 0.5, height + EMBERS, hull.cz + half * BED), "ember")))
        logs = [box(f"Fire{n}Log{k}", (x, height + EMBERS, hull.cz + across - 0.7), (x + 9, height + EMBERS + 1.4, hull.cz + across + 0.7),
                    "burntlog", origin=(x + 4.5, height, hull.cz), rot=(0, yaw, 0))
                for k, (x, across, yaw) in enumerate([(-10, -1.2, 8), (-2, 1, -6), (6, -0.8, 5), (14, 1.3, -9), (21, 0, 4)], 1)]
        levels.append(group(f"Blank{n}", (0, 0, 0), (0, 0, 0), slabs))
        chars.append(group(f"Char{n}", (0, 0, 0), (0, 0, 0), planes))
        woods.append(group(f"Wood{n}", (0, 0, 0), (0, 0, 0), logs))
        fires.append(group(f"Fire{n}", (0, 0, 0), (0, 0, 0), embers))
    return [group(name, (0, 0, 0), (0, 0, 0), children)
            for name, children in (("Blank", levels), ("Char", chars), ("Wood", woods), ("Fire", fires))]


def glowing(element):
    element["glow"] = GLOW
    return element


def clay(hull):
    caps = [box(f"Clay{name}{n}", (x0, RIM, hull.cz - half), (x1, RIM + CLAY, hull.cz + half), "clay")
            for name, slices in ENDS.items() for n, (x0, x1, half, _) in enumerate(slices, 1)]
    return group("Clay", (0, 0, 0), (0, 0, 0), hull.rail("ClayRim", "clay", height=CLAY, thickness=WALL + 0.6) + caps)


def build(path):
    shape = Shape(path, {"inside": "game:block/wood/debarked/oak", "bilge": BILGE, "char": "game:block/coal/charcoal",
                         "clay": "game:block/clay/blueclay", "ember": "game:block/coal/ember",
                         "burntlog": "game:block/wood/firepit/burntlog"})
    hull = Hull("Side", STATIONS, 8, WALL, WOOD, WOOD, edge=WOOD)
    shape.add(*log(hull))
    shape.add(blank(hull, min(edges[0][1] for _, edges in STATIONS) + hull.t), clay(hull))
    shape.add(hull.hide_water(7.7), hull.bilge(7.7))
    shape.keep("Hull", "FrontSeat", "BackSeat")
    shape.write()


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
