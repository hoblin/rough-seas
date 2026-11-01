import math

from physics import afloat, kilograms
from shape import BILGE, VOXELS_A_METRE, Hull, Shape, box, breadth, group, path, plank, renamed, span, stages
from stroke import crew, moved, strokes

CENTRE = 8
VOXEL_METRES = 1 / 16
PROFILE = [(0, 0), (6.1, 0), (8, 2.8), (9.2, 6.6), (9.6, 9.6)]
SHEER = 9.6

# Her gunwale is one 5 m lath a side, its ends drawn together at bow and stern: a little under its length round her sheer.
GUNWALE_METRES = 4.95
# Bow to stern, each end the other's mirror: (share of her half-length, breadth, rocker, rise of the sheer).
LINES = [(1, 0.04, 5.6, 1.3), (0.92, 0.27, 3.4, 1.1), (0.8, 0.56, 1.6, 0.8), (0.6, 0.82, 0.5, 0.4), (0.3, 0.96, 0.05, 0.1), (0, 1, 0, 0)]

RIB_SPACING = 3
RIB_WIDTH = 0.8
RIB_DEPTH = 0.4
STRINGER = 0.6
THWART_SHARE = 0.75
THWART = 1.4
THWART_SEAT = 7

OARS = {"OarStarboard": -1, "OarPort": 1}
THOLE_AHEAD = 9.6
BULL = 2.4
INBOARD = 8.8
LOOM_END = 14
BLADE_START = 23.2
OUTBOARD = 39.2
FRAMES = 30
SWEEP_DEGREES = 28
REST_PITCH = -13
DIP_DEGREES = 11

# A small hide is about 1.2 m²: a moose's 3.5 m² over the three leather vanilla tans from its large hide.
SECTION_SQUARE_METRES = 1.2
# The ends bound with some 10 m of cord each (an estimate); the hide laced to the gunwale every 5 cm, each turn round
# the gunwale and the hide's edge some 15 cm of cord (Hornell 1938: "laced at short intervals").
END_METRES = 10
LACING_STEP_METRES = 0.05
LACING_TURN_METRES = 0.15
LACES = 12
# Vanilla's rope: one item pays out up to 10 m (ClothSystem.maxLen).
ROPE_METRES = 10
HIDE_METRES = 0.005
LATH_SQUARE_METRES = 0.01 * 0.06
POLE_SQUARE_METRES = 0.09 * 0.09


def section(x, breadth, rocker, rise):
    top = SHEER + rise
    return (x, [(h * breadth, rocker + y * (top - rocker) / SHEER) for h, y in PROFILE])


def stations(half):
    ends = [section(CENTRE - share * half, *rest) for share, *rest in LINES]
    return ends + [section(2 * CENTRE - x, *rest) for (x, _), (_, *rest) in zip(reversed(ends[:-1]), reversed(LINES[:-1]))]


def sheer_metres(hull):
    top = len(PROFILE) - 1
    points = [hull.point(i, top, 1) for i in range(len(hull.stations))]
    return sum(math.dist(a, b) for a, b in zip(points, points[1:])) * VOXEL_METRES


def hull_of_gunwale():
    low, high = 20.0, 60.0
    for _ in range(40):
        half = (low + high) / 2
        hull = Hull("Skin", stations(half), CENTRE, 0.5, "hide", "hide", edge="hide")
        low, high = (half, high) if sheer_metres(hull) < GUNWALE_METRES else (low, half)
    print(f"hull: {2 * half * VOXEL_METRES:.2f} m long, {2 * PROFILE[-1][0] * VOXEL_METRES:.2f} m in beam, gunwale {sheer_metres(hull):.2f} m a side")
    return hull, half


def gunwales(hull):
    rail = hull.rail("Gunwale", "frame", height=0.9, thickness=1.0, lift=-0.5)
    return [[plank for plank in rail if plank["name"].startswith(f"Gunwale{side}")] for side in ("Port", "Starboard")]


def open_gunwales(hull):
    """Her gunwale laths with only the bow drawn together: astern they still run straight, their own length on."""
    top = len(PROFILE) - 1
    pieces = []
    for (side, label), closed in zip(((-1, "Port"), (1, "Starboard")), gunwales(hull)):
        bow = [p for p, (x0, _), (x1, _) in zip(closed, hull.stations, hull.stations[1:]) if x1 <= CENTRE + 1e-6]
        middle = hull.at(CENTRE, top, side)
        stern = [hull.point(i, top, side) for i in range(len(hull.stations)) if hull.stations[i][0] >= CENTRE - 1e-6]
        reach = sum(math.dist(a, b) for a, b in zip(stern, stern[1:]))
        start = (middle[0], middle[1] - 0.5, middle[2])
        end = (middle[0] + reach, middle[1] - 0.5, middle[2])
        pieces.append([*(renamed(p, "Open") for p in bow),
                       plank(f"Gunwale{label}Astern", start, end, 0.9, 1.0, 0, side, "frame", "frame")])
    return pieces


def gunwale_stage(hull):
    closed, opened = gunwales(hull), open_gunwales(hull)
    pieces = []
    for n, (shut, apart) in enumerate(zip(closed, opened), 1):
        piece = group(f"Gunwale{n}", (0, 0, 0), (0, 0, 0), [
            group(f"Gunwale{n}At1", (0, 0, 0), (0, 0, 0), apart),
            group(f"Gunwale{n}At2", (0, 0, 0), (0, 0, 0), shut),
        ])
        piece["metres"] = round(span(shut, "frame") / VOXELS_A_METRE, 2)
        pieces.append(piece)
    return group("Gunwale", (0, 0, 0), (0, 0, 0), pieces)


def ends(half):
    knots = []
    for name, x in (("Bow", CENTRE - half), ("Stern", CENTRE + half)):
        _, edges = section(x, *LINES[0][1:])
        y = edges[-1][1]
        inward = 1 if name == "Bow" else -1
        knots.append([box(f"{name}Lash", (min(x, x + inward * 2.5), y - 1.6, CENTRE - 1), (max(x, x + inward * 2.5), y + 0.4, CENTRE + 1), "lashing")])
    return knots


def ribs(hull, half):
    xs = [x for x in range(int(CENTRE - half + 6), int(CENTRE + half - 5), RIB_SPACING)]
    middle = sum(xs) / len(xs)
    order = sorted(xs, key=lambda x: (abs(x - middle), x))
    return [hull.rib(f"Rib{n}x", x, "frame", width=RIB_WIDTH, depth=RIB_DEPTH) for n, x in enumerate(order, 1)]


def stringers(hull):
    pieces = []
    inside = hull.t + STRINGER / 2
    for k in range(1, len(PROFILE) - 1):
        for side, label in ((-1, "Port"), (1, "Starboard")):
            points = [hull.point(i, k, side) for i in range(1, len(hull.stations) - 1)]
            points = [(x, y + inside, z - side * inside) for x, y, z in points]
            pieces.append(path(f"Stringer{label}{k}x", points, STRINGER, STRINGER, "frame", overlap=0))
    keel = [(x, edges[0][1] + inside, CENTRE) for x, edges in hull.stations[1:-1]]
    pieces.insert(0, path("Keelson", keel, STRINGER, STRINGER, "frame", overlap=0))
    return pieces


def thwart(hull, half):
    x = CENTRE + THWART_SHARE * 2 * half - half
    reach = breadth(hull.section(x), THWART_SEAT) - 0.4
    y = THWART_SEAT - THWART
    return [[box("ThwartPole", (x - THWART / 2, y, CENTRE - reach), (x + THWART / 2, THWART_SEAT, CENTRE + reach), "frame", grain="z")]]


def cover(hull):
    planks = hull.strakes()
    areas = []
    for plank in planks:
        board = plank["children"][0]
        areas.append((plank["from"][0], (board["to"][0] + board["from"][0]) * (board["to"][1] - board["from"][1]) * VOXEL_METRES ** 2, plank))
    area = sum(square for _, square, _ in areas)
    sections = math.ceil(area / SECTION_SQUARE_METRES)
    print(f"cover: {area:.2f} m² of hide, {sections} sections of {SECTION_SQUARE_METRES} m²")
    parts, done = [[] for _ in range(sections)], 0
    for _, square, plank in sorted(areas, key=lambda entry: entry[0]):
        parts[min(sections - 1, int((done + square / 2) / (area / sections)))].append(plank)
        done += square
    return parts


def lacing(hull):
    perimeter = 2 * sheer_metres(hull)
    metres = perimeter / LACING_STEP_METRES * LACING_TURN_METRES + perimeter
    ropes = math.ceil(metres / ROPE_METRES)
    print(f"lacing: {metres:.0f} m round a {perimeter:.2f} m gunwale, {ropes} ropes")
    top = len(PROFILE) - 1
    wraps = []
    for side, label in ((-1, "Port"), (1, "Starboard")):
        for n in range(LACES):
            i = 1 + n * (len(hull.stations) - 3) / (LACES - 1)
            a, b = hull.point(int(i), top, side), hull.point(min(int(i) + 1, len(hull.stations) - 1), top, side)
            f = i - int(i)
            x, y, z = (p + f * (q - p) for p, q in zip(a, b))
            wraps.append(box(f"Lace{label}{n + 1}", (x - 0.4, y - 1.6, z - 0.7), (x + 0.4, y + 0.4, z + 0.7), "lashing"))
    return [wraps[len(wraps) * r // ropes:len(wraps) * (r + 1) // ropes] for r in range(ropes)]


def thole_station(half):
    return CENTRE + THWART_SHARE * 2 * half - half - THOLE_AHEAD


def tholes(hull, half):
    x = thole_station(half)
    top = len(PROFILE) - 1
    pins, rests = [], {}
    for name, side in OARS.items():
        _, y, z = hull.at(x, top, side)
        pins.append(box(f"Thole{name[3:]}", (x - 0.3, y - 0.6, z - 0.3), (x + 0.3, y + BULL + 1.2, z + 0.3), "frame", grain="y"))
        rests[name] = (x, y + 0.4 + BULL, z)
    return pins, rests


def oar(name, side, rest):
    return group(name, rest, (side * 90, 0, 0), [
        box(f"{name}Grip", (-INBOARD, -0.35, -0.35), (-INBOARD + 3, 0.35, 0.35), "frame"),
        box(f"{name}Loom", (-INBOARD + 3, -0.4, -0.4), (LOOM_END, 0.4, 0.4), "frame"),
        box(f"{name}Bull", (-1.6, -BULL, -0.7), (1.6, -0.4, 0.7), "frame"),
        box(f"{name}Shaft", (LOOM_END, -0.3, -0.3), (BLADE_START, 0.3, 0.3), "frame"),
        box(f"{name}Blade", (BLADE_START, -0.15, -0.8), (OUTBOARD, 0.15, 0.8), "frame"),
    ])


def sweep(u):
    return -math.cos(2 * math.pi * u)


def depth(u):
    return max(0.0, min(1.0, 0.5 + 1.6 * math.sin(2 * math.pi * u)))


def rowing(rests):
    def pose(sweep_at, depth_at):
        def at(frame):
            u = frame / FRAMES
            yaw = 90 - SWEEP_DEGREES * sweep_at(u)
            pitch = REST_PITCH - DIP_DEGREES * depth_at(u)
            return {name: moved(rests[name], (side * 90, 0, 0), rests[name], {"rotationY": -side * yaw, "rotationZ": pitch})
                    for name, side in OARS.items()}
        return at

    return strokes("currach", FRAMES, pose(lambda u: 0, lambda u: 0), pose(sweep, depth),
                   pose(lambda u: sweep(1 - u), lambda u: depth(1 - u)), stroke="rowing")


def blade_span(rests):
    reach = (BLADE_START + OUTBOARD) / 2 * math.cos(math.radians(-REST_PITCH))
    across = [abs(z - CENTRE) + reach for _, _, z in rests.values()]
    return sum(across) / VOXELS_A_METRE


def build(path_):
    shape = Shape(path_, {
        "hide": "game:block/leather/poor",
        "frame": "game:block/wood/debarked/oak",
        "lashing": "game:item/resource/rope",
        "bilge": BILGE,
    })
    hull, half = hull_of_gunwale()
    laths = [gunwale_stage(hull), stages("Ribs", ribs(hull, half)), stages("Stringers", stringers(hull))]
    thwart_stage = stages("Thwart", thwart(hull, half))
    shape.add(laths[0])
    shape.add(stages("EndLashing", ends(half)))
    shape.add(laths[1], laths[2])
    shape.add(stages("Cover", cover(hull), wood="none"))
    shape.add(stages("Lacing", lacing(hull)))
    shape.add(thwart_stage)
    pins, rests = tholes(hull, half)
    shape.add(*pins)
    shape.add(stages("Oars", [[oar(name, side, rests[name])] for name, side in OARS.items()], wood="none"))
    shape.add(hull.hide_water(SHEER - 0.4), hull.bilge(SHEER - 0.4))
    shape.keep("Hull", "BackSeat", "ForeCargo", "MidCargo", "AftCargo")
    crew(shape, "currach", "BackSeat", "roughseas-sitthwart", rowing(rests), {"R": "OarStarboardGrip", "L": "OarPortGrip"},
         {"rotationZ": 12})
    print(f"oars: {blade_span(rests):.2f} m from blade to blade across her")
    report(hull, laths, thwart_stage, [oar(name, side, rests[name]) for name, side in OARS.items()])


def litres(element):
    size = [t - f for f, t in zip(element["from"], element["to"])]
    return size[0] * size[1] * size[2] * VOXEL_METRES ** 3 * 1000


def report(hull, laths, thwart_stage, oars):
    area = hull.skin_square_metres()
    metres = sum(piece["metres"] for stage in laths for piece in stage["children"])
    pole = sum(piece["metres"] for piece in thwart_stage["children"])
    hide_kg = kilograms("hide", area * HIDE_METRES * 1000)
    frame_kg = kilograms("wood-ash", (metres * LATH_SQUARE_METRES + pole * POLE_SQUARE_METRES) * 1000)
    oars_kg = kilograms("wood-ash", sum(litres(part) for oar_ in oars for part in oar_["children"]))
    print(f"currach: {area:.2f} m² of hide, {hide_kg:.1f} kg; {metres:.1f} m of lath and {pole:.2f} m of pole, {frame_kg:.1f} kg of ash;"
          f" two oars, {oars_kg:.1f} kg")
    afloat("currach", hide_kg + frame_kg + oars_kg, SHEER / VOXELS_A_METRE, hull.litres(SHEER, inset=hull.t),
           hull.square_metres(SHEER - 0.01, inset=hull.t))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
