import math
import statistics

from physics import MASS, afloat, kilograms, stands
from shape import BILGE, VOXELS_A_METRE, Hull, Shape, box, breadth, group, path, plank, renamed, span, stages
from held import POLE, seated_poling, standing_poling, tools, waterline
from stroke import Rower, centre, crew, drive_share, facing, find, moved, player_patch, posed, seat, strokes
from transform import direct, unapply
from wounds import wound

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
THWART = 1.4
# Aran thwarts are nailed to the lower gunwale, the upper some 6 in above it (Hornell 1938).
THWART_SEAT = SHEER - 0.15 * VOXELS_A_METRE
# A seated man's centre of mass lies some 9 cm from his hips toward his feet: his thighs, 20% of his mass, at 0.19 m,
# his shanks and feet, 12%, at 0.45 m (Winter 2009, table 4.1).
SEATED_CENTRE = 0.09 * VOXELS_A_METRE
POLER_AFT = 5

OARS = {"OarStarboard": -1, "OarPort": 1}
# Dingle's thole pins stand 15 in abaft their thwart (Hornell 1938); none is given for Aran.
THOLE_ABAFT = 0.38 * VOXELS_A_METRE
# The pin passes through the bull, nailed to the loom's after side (Hornell 1938).
PIN_TO_LOOM = 1.4
LOOM_RADIUS = 0.4
OAR = 48
# The grips cross 6 to 12 in amidships (the Celtic MKE currach club): each 0.115 m past her middle.
GRIP = 0.115 * VOXELS_A_METRE
LOOM_END = 14
BLADE = 16
BLADE_BREADTH = 1.6
# One hand over the other where the grips cross, a hand's thickness apart: 3.3 cm (NASA-STD-3000, hand thickness at
# the metacarpals of a 50th-percentile man).
HAND_THICKNESS = 0.033 * VOXELS_A_METRE
FRAMES = 30
# The trunk from the vertical, forward at the catch and back at the finish: the world's best rowers, 22.5° and 25°
# (Kleshnev, row2k 2014, video of 25 small-boat rowers).
CATCH_LEAN = 22.5
FINISH_LEAN = -25
# A fixed-seat stroke sweeps some 50° forward of square at the catch and 35° aft of it at the finish (gigrower.co.uk,
# 2010; John Welsford's skiffs), unless the rower's reach or his chest stops it sooner.
CATCH_DEGREES = 50
FINISH_DEGREES = 35
DIP_SHARPNESS = 3

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


def thwart(hull, x):
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


def tholes(hull, x):
    top = len(PROFILE) - 1
    pins, rests = [], {}
    for name, side in OARS.items():
        _, y, z = hull.at(x, top, side)
        pins.append(box(f"Thole{name[3:]}", (x - 0.3, y - 0.6, z - 0.3), (x + 0.3, y + 2 * LOOM_RADIUS + 1.6, z + 0.3), "frame", grain="y"))
        rests[name] = (x, y + LOOM_RADIUS, z)
    return pins, rests


def inboard(rest):
    return abs(rest[2] - CENTRE)


def oar(name, side, rest):
    loom = inboard(rest)
    outboard = OAR - loom - GRIP
    z = side * PIN_TO_LOOM
    return group(name, rest, (side * 90, 0, 0), [
        box(f"{name}Grip", (-loom - GRIP, -0.35, z - 0.35), (-loom, 0.35, z + 0.35), "frame"),
        box(f"{name}Loom", (-loom, -LOOM_RADIUS, z - LOOM_RADIUS), (LOOM_END, LOOM_RADIUS, z + LOOM_RADIUS), "frame"),
        box(f"{name}Bull", (-1.6, -LOOM_RADIUS, min(0, z) - 0.7), (1.6, LOOM_RADIUS, max(0, z) + 0.7), "frame"),
        box(f"{name}Shaft", (LOOM_END, -0.3, z - 0.3), (outboard - BLADE, 0.3, z + 0.3), "frame"),
        box(f"{name}Blade", (outboard - BLADE, -BLADE_BREADTH / 2, z - 0.15), (outboard, BLADE_BREADTH / 2, z + 0.15), "frame"),
    ])


def pitches(rests, chest):
    rest = next(iter(rests.values()))
    outboard = OAR - inboard(rest) - GRIP
    above = rest[1] - waterline("currach") * VOXELS_A_METRE
    hand = inboard(rest) + GRIP / 2
    apart = math.degrees(math.asin(HAND_THICKNESS / 2 / hand))
    drive = -math.degrees(math.asin((chest - rest[1]) / hand))
    recovery = -math.degrees(math.asin((above - BLADE_BREADTH / 2) / outboard)) + apart
    return {name: (recovery + side * apart, drive + side * apart) for name, side in OARS.items()}


def chest_height(shape):
    rower = Rower(seat(shape, "BackSeat"), "roughseas-sitthwart", facing(shape, "currach", "BackSeat"))
    matrices = posed(rower.seraph["elements"], lambda name: rower.idle.get(name, {}))
    return 16 * unapply(rower.placing, [c / 16 for c in centre(rower.seraph["elements"], matrices, "UpperTorso")])[1]


def blade_square_metres(rests, chest):
    rest = next(iter(rests.values()))
    outboard = OAR - inboard(rest) - GRIP
    above = rest[1] - waterline("currach") * VOXELS_A_METRE
    wet = [min(BLADE, max(0.0, outboard - above / math.sin(math.radians(-drive)))) for _, drive in pitches(rests, chest).values()]
    return statistics.fmean(wet) / VOXELS_A_METRE * BLADE_BREADTH / VOXELS_A_METRE


def rowing_station(hull):
    x = CENTRE
    for _ in range(8):
        pole = sum(piece["metres"] for piece in stages("Thwart", thwart(hull, x))["children"])
        _, rests = tholes(hull, x + THOLE_ABAFT)
        oars = sum(litres(part) for name, side in OARS.items() for part in oar(name, side, rests[name])["children"])
        rower = MASS["crewKg"] + MASS["playerGearKg"]
        weights = {"rower": (rower, SEATED_CENTRE), "thwart": (kilograms("wood-ash", pole * POLE_SQUARE_METRES * 1000), 0),
                   "oars": (kilograms("wood-ash", oars), THOLE_ABAFT)}
        x = CENTRE - sum(kg * d for kg, d in weights.values()) / sum(kg for kg, _ in weights.values())
    print(f"thwart: {(x - CENTRE) / VOXELS_A_METRE:+.2f} m from her middle, where she floats level with her rower and his oars;"
          f" tholes {THOLE_ABAFT / VOXELS_A_METRE:.2f} m abaft it")
    return x


def sweep(u):
    drive = drive_share("rowing")
    return -math.cos(math.pi * u / drive) if u < drive else math.cos(math.pi * (u - drive) / (1 - drive))


def depth(u):
    drive = drive_share("rowing")
    return min(1.0, DIP_SHARPNESS * math.sin(math.pi * u / drive)) if u < drive else 0.0


def oar_pose(rests, name, side, yaw, pitch):
    return moved(rests[name], (side * 90, 0, 0), rests[name], {"rotationY": -side * yaw, "rotationZ": pitch})


def grip_at(rests, yaw, pitch):
    oars = [oar(name, side, rests[name]) for name, side in OARS.items()]
    matrices = posed(oars, lambda name: oar_pose(rests, name, OARS[name], yaw, pitch) if name in OARS else {})
    return centre(oars, matrices, "OarPortGrip")


def solve(low, high, too_far):
    for _ in range(40):
        middle = (low + high) / 2
        low, high = (low, middle) if too_far(middle) else (middle, high)
    return (low + high) / 2


def arc(shape, rests, chest):
    rower = Rower(seat(shape, "BackSeat"), "roughseas-sitthwart", facing(shape, "currach", "BackSeat"))
    arm = rower.arms["R"]
    reach = 16 * (math.dist((0, 0, 0), arm.humerus) + math.dist((0, 0, 0), arm.forearm))
    drive = pitches(rests, chest)["OarPort"][1]

    def shape_point(point):
        return tuple(16 * c for c in unapply(rower.placing, point))

    shoulder = shape_point(arm.joints({"UpperTorso": {"rotationZ": CATCH_LEAN}})[0])
    catch = solve(90, 90 + CATCH_DEGREES, lambda yaw: math.dist(shoulder, grip_at(rests, yaw, drive)) > reach)
    leaning = {"UpperTorso": {"rotationZ": FINISH_LEAN}}
    matrices = posed(rower.seraph["elements"], lambda name: leaning.get(name) or rower.idle.get(name, {}))
    torso = find(rower.seraph["elements"], "UpperTorso")
    forward = direct(matrices["UpperTorso"], (-1, 0, 0))
    middle = centre(rower.seraph["elements"], matrices, "UpperTorso")
    depth = (torso["to"][0] - torso["from"][0]) / 2
    body = shape_point([(m + depth * f) / 16 for m, f in zip(middle, forward)])[0] + HAND_THICKNESS
    finish = solve(90 - FINISH_DEGREES, 90, lambda yaw: grip_at(rests, yaw, drive)[0] > body)
    print(f"stroke: catch {catch - 90:.0f}° forward of square, leaning {CATCH_LEAN}°; finish {90 - finish:.0f}° aft of it,"
          f" leaning {-FINISH_LEAN}° back")
    return (catch + finish) / 2, (catch - finish) / 2


def rowing(rests, chest, sweep_middle, sweep_degrees):
    pitched = pitches(rests, chest)

    def pose(sweep_at, depth_at):
        def at(frame):
            u = frame / FRAMES
            yaw = sweep_middle - sweep_degrees * sweep_at(u)
            return {name: moved(rests[name], (side * 90, 0, 0), rests[name],
                                {"rotationY": -side * yaw, "rotationZ": pitched[name][0] + (pitched[name][1] - pitched[name][0]) * depth_at(u)})
                    for name, side in OARS.items()}
        return at

    def swing(sweep_at):
        return lambda frame: {"UpperTorso": {"rotationZ": (CATCH_LEAN + FINISH_LEAN) / 2
                                                         - (CATCH_LEAN - FINISH_LEAN) / 2 * sweep_at(frame / FRAMES)}}

    rowed = strokes("currach", FRAMES, pose(lambda u: 0, lambda u: 0), pose(sweep, depth),
                    pose(lambda u: sweep(1 - u), lambda u: depth(1 - u)), stroke="rowing")
    for stroke, sweep_at in zip(rowed, (lambda u: 0, sweep, lambda u: sweep(1 - u))):
        stroke["body"] = swing(sweep_at)
    return rowed


def blade_span(rests, chest, sweep_middle, sweep_degrees):
    drive = drive_share("rowing")
    abeam = statistics.fmean(math.sin(math.radians(sweep_middle - sweep_degrees * sweep(drive * k / 100))) for k in range(100))
    across = [inboard(rest) + (OAR - inboard(rest) - GRIP - BLADE / 2) * math.cos(math.radians(pitched)) * abeam
              for rest, (_, pitched) in zip(rests.values(), pitches(rests, chest).values())]
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
    station = rowing_station(hull)
    thwart_stage = stages("Thwart", thwart(hull, station))
    shape.add(laths[0])
    shape.add(stages("EndLashing", ends(half)))
    shape.add(laths[1], laths[2])
    hide = stages("Cover", cover(hull), wood="none")
    shape.add(hide)
    shape.add(stages("Lacing", lacing(hull)))
    shape.add(thwart_stage)
    pins, rests = tholes(hull, station + THOLE_ABAFT)
    shape.add(*pins)
    shape.add(stages("Oars", [[oar(name, side, rests[name])] for name, side in OARS.items()], wood="none"))
    shape.add(hull.hide_water(SHEER - 0.4), hull.bilge(SHEER - 0.4))
    shape.keep("Hull", "BackSeat", "ForeCargo", "MidCargo", "AftCargo", "OarStorage")
    wound(shape, [("", [hide], "hide")], framing=([laths[1]], "frame"))
    chest = chest_height(shape)
    middle, degrees = arc(shape, rests, chest)
    rowed = crew(shape, "currach", "BackSeat", "roughseas-sitthwart", rowing(rests, chest, middle, degrees), {"R": "OarPortGrip", "L": "OarStarboardGrip"},
                 {"rotationZ": 12})
    at = seat(shape, "BackSeat")
    line = waterline("currach")
    poler = sum(hull.span()) / 2 + POLER_AFT
    floor = hull.section(poler)[0][1] + hull.t + RIB_DEPTH
    pole = (("pole", POLE, standing_poling(POLE, (poler, floor, CENTRE), line * VOXELS_A_METRE, CENTRE - PROFILE[-1][0] - 1,
                                           hull.half(poler, floor + 1, inset=hull.t + RIB_DEPTH),
                                           floor_at=lambda x, across: hull.floor(x, across, inset=hull.t + RIB_DEPTH)))
            if stands("currach", floor * VOXEL_METRES)
            else ("seatedpole", POLE, seated_poling(POLE, at, line * VOXELS_A_METRE, CENTRE - PROFILE[-1][0] - 1)))
    player_patch("player-currach", rowed + tools(shape, "currach", "BackSeat", "roughseas-sitthwart", {"rotationZ": 12}, line, {
        "pole": pole,
    }, ("Cover",)))
    print(f"oars: {blade_span(rests, chest, middle, degrees):.2f} m from blade to blade across her,"
          f" {blade_square_metres(rests, chest):.3f} m² of blade under the water")
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
