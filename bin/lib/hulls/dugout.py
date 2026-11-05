import math
from held import ARM_REACH_METRES, worked_from_middle, PADDLE, paddling, poling, tools, waterline_metres
from stroke import find, player_patch, seat
from physics import afloat, kilograms, stands
from fit import APART
import units
from entity import merged, record
from shape import BILGE, FACES, ORIGIN, Hull, bilge_levels, hull_box, windage, Shape, box, breadth, group, num, seed, water_plane
from wounds import wound

LOG = 16
LOGS = 5
ROW = (8 - LOGS * LOG / 2, 8 + LOGS * LOG / 2)
CENTRE = 8
PROFILE = [(0, 0), (3.84, 0.4), (6.24, 2.0), (7.08, 4.7), (7.2, 9)]
WOOD = "inside"
BARK = "bark"
RINGS = "rings"
RIM = 9
WALL = 0.8
# Wet clay goes along the rim before each fire (PHMC dugout project, 2005; SUNY Potsdam, 2015), and the fire is tended
# until the clay is spent, about a day and a half: 8.5 cm at a time, the hollow takes six fires and about nine days, as
# Wampanoag builders burn a mishoon for 6 to 10 days (Plimoth Patuxet, The Making of a Mishoon).
HOLLOWINGS = 6
BARK_THICKNESS = units.voxels(0.02)
BARE = LOG / 2 - BARK_THICKNESS
BARE_TOP = LOG - BARK_THICKNESS
CHAR_LIFT = 0.02
CLAY = 0.5
CLAY_PIECE = 12
CLAY_BALL_VOXELS = 25
GRASS = 0.3
TUFTS = 14
FIREWOOD = 4
SPLIT = 1.5
BED = 0.55
BURNING = 3
EMBERS = (0.3, 0.5, 0.4)
GLOW = (160, 160, 110)
LOGS_GLOW = 90
TRUNK_METRES = units.metres(LOG)
SLICES = {
    "Stern": [(-24.5, -22, 5.0, 1.6), (-26.5, -24.5, 4.0, 2.8), (-28, -26.5, 2.9, 4.2)],
    "Bow": [(38, 40.5, 5.0, 1.6), (40.5, 42.5, 4.0, 2.8), (42.5, 44, 2.9, 4.2)],
}
HOLLOW = (-22, 38)
POLER_AT = HOLLOW[0] + 5
CUTS = (ROW[0] + 2 * LOG, ROW[0] + 3 * LOG)
SPANS = list(zip((HOLLOW[0], *CUTS), (*CUTS, HOLLOW[1])))
LEVEL = (0, 0, 0)


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / RIM)) for h, y in PROFILE])


STATIONS = [
    section(-22, 0.72, 1.4),
    section(-15, 0.92, 0.45),
    section(-5, 1, 0),
    section(8, 1, 0),
    section(21, 1, 0),
    section(31, 0.92, 0.45),
    section(38, 0.72, 1.4),
]


LOWER = 0.7


def waist_of(bottom, top=RIM):
    return bottom + (top - bottom) / 3


def inscribed(slices, edges, top=RIM, lower_share=LOWER):
    """Her stepped ends, each slice an upper block over a narrower lower one, as wide as its rough size gives but no
    wider than the hull's end section, or the slice before it, at every height its blocks reach."""
    outline = lambda y: breadth(edges, y)
    drawn = []
    for x0, x1, half, bottom in slices:
        waist = waist_of(bottom, top)
        upper, lower = min(half, outline(waist), outline(top)), min(half * lower_share, outline(bottom), outline(waist))
        drawn.append((x0, x1, upper, lower, bottom))
        outline = lambda y, upper=upper, lower=lower, bottom=bottom, waist=waist: upper if y >= waist else lower if y >= bottom else 0
    return drawn


ENDS = {"Stern": inscribed(SLICES["Stern"], STATIONS[0][1]), "Bow": inscribed(SLICES["Bow"], STATIONS[-1][1])}


def log(hull):
    return [hull.strakes(), *(end(name, slices, hull.cz) for name, slices in ENDS.items())]


def end(name, slices, centre, top=RIM):
    grain = {**{face: WOOD for face in FACES}, ("west" if "Stern" in name else "east"): RINGS}
    blocks = []
    for n, (x0, x1, upper, lower, bottom) in enumerate(slices, 1):
        waist = waist_of(bottom, top)
        blocks.append(box(f"{name}{n}Upper", (x0, waist, centre - upper), (x1, top, centre + upper), grain))
        blocks.append(box(f"{name}{n}Lower", (x0, bottom, centre - lower), (x1, waist, centre + lower), grain))
    return blocks


def end_slice(x):
    for slices in ENDS.values():
        for x0, x1, upper, lower, bottom in slices:
            if x0 <= x <= x1:
                return upper, lower, bottom
    return None


def finished_half(x, y):
    x = min(max(x, ENDS["Stern"][-1][0]), ENDS["Bow"][-1][1])
    if (piece := end_slice(x)) is not None:
        upper, lower, bottom = piece
        return 0 if y < bottom else lower if y < waist_of(bottom) else upper
    for (x0, edges0), (x1, edges1) in zip(STATIONS, STATIONS[1:]):
        if x0 <= x <= x1:
            f = (x - x0) / (x1 - x0)
            low = edges0[0][1] + f * (edges1[0][1] - edges0[0][1])
            return 0 if y < low else breadth(edges0, y) + f * (breadth(edges1, y) - breadth(edges0, y))
    return 0


def finished_bottom(x):
    x = min(max(x, ENDS["Stern"][-1][0]), ENDS["Bow"][-1][1])
    if (piece := end_slice(x)) is not None:
        return piece[2]
    for (x0, edges0), (x1, edges1) in zip(STATIONS, STATIONS[1:]):
        if x0 <= x <= x1:
            return edges0[0][1] + (x - x0) / (x1 - x0) * (edges1[0][1] - edges0[0][1])
    return 0


def worked(n):
    return n / HOLLOWINGS


def top(n):
    return BARE_TOP + (RIM - BARE_TOP) * worked(n)


def wall(n):
    return WALL + (BARE - PROFILE[-1][0]) * (1 - worked(n))


def floor(n, x):
    return max(finished_bottom(x) * worked(n) + wall(n), top(n) - (RIM - WALL) * worked(n))


def extent(n):
    stern, bow = ENDS["Stern"][-1][0], ENDS["Bow"][-1][1]
    return ROW[0] + (stern - ROW[0]) * worked(n), ROW[1] + (bow - ROW[1]) * worked(n)


def hollow_half(x):
    return max(0, finished_half(x, RIM) - WALL)


def hollowed(x):
    return HOLLOW[0] < x < HOLLOW[1]


def bare_section():
    return [(0, 0), (BARE, 0), *((BARE, y * BARE_TOP / RIM) for _, y in PROFILE[2:])]


def stations_at(n):
    f = worked(n)
    return [(x, [(h0 + (h - h0) * f, y0 + (y - y0) * f) for (h0, y0), (h, y) in zip(bare_section(), edges)]) for x, edges in STATIONS]


def hewing(n):
    return Hull(f"Blank{n}Side", stations_at(n), CENTRE, wall(n), WOOD, WOOD, edge=WOOD)


def ends_at(n):
    f = worked(n)
    stations = stations_at(n)
    drawn = {}
    for name, station, far in (("Stern", stations[0], extent(0)[0]), ("Bow", stations[-1], extent(0)[1])):
        join, finished = station[0], (ENDS[name][-1][0] if name == "Stern" else ENDS[name][-1][1])
        stretch = (far - join) / (finished - join)
        moved = lambda x: join + (x - join) * (stretch + (1 - stretch) * f)
        rough = [(min(moved(x0), moved(x1)), max(moved(x0), moved(x1)), BARE + (half - BARE) * f, bottom * f) for x0, x1, half, bottom in SLICES[name]]
        drawn[name] = inscribed(rough, station[1], top(n), 1 + (LOWER - 1) * f)
    return drawn


def faced(element, faces):
    for face, rect in faces.items():
        element["faces"][face]["uv"] = [num(c) for c in rect]
        element["faces"][face].pop("rotation", None)
    return element


def side_window(y0, y1):
    return (0, LOG - y1, LOG, LOG - y0)


def end_window(z0, z1, y0, y1):
    edge = CENTRE - LOG / 2
    return (z0 - edge, LOG - y1, z1 - edge, LOG - y0)


def top_window(x0, x1, start, z0, z1):
    edge = CENTRE - LOG / 2
    return (x0 - start, z0 - edge, x1 - start, z1 - edge)


def joints_of(i):
    return (() if i == 0 else ("west",)) + (() if i == LOGS - 1 else ("east",))


def ends_of_log(i, z0, z1, y0, y1):
    return {face: end_window(z0, z1, y0, y1) for face in ("west", "east") if face not in joints_of(i)}


LOG_TEXTURES = {"north": WOOD, "south": WOOD, "up": WOOD, "down": BARK, "west": RINGS, "east": RINGS}


def trunk():
    logs = []
    for i in range(LOGS):
        x0 = ROW[0] + i * LOG
        element = box(f"Blank0Log{i + 1}", (x0, 0, CENTRE - BARE), (x0 + LOG, BARE_TOP, CENTRE + BARE), LOG_TEXTURES, skip=joints_of(i))
        logs.append(faced(element, {"down": (0, 0, LOG, 2 * BARE), "north": side_window(0, BARE_TOP), "south": side_window(0, BARE_TOP),
                                    "up": top_window(x0, x0 + LOG, x0, CENTRE - BARE, CENTRE + BARE), **ends_of_log(i, CENTRE - BARE, CENTRE + BARE, 0, BARE_TOP)}))
    return logs


def trench():
    """The first hewing's inside cut before its outside: each log with the hollow that hewing leaves cut into it as
    a trench, its outer faces mapped as the trunk's, so the outside stays as it was."""
    hull = Hull("", stations_at(1), CENTRE, wall(1), WOOD, WOOD)
    sections = {j: [] for j in range(1, len(SPANS) + 1)}
    for i in range(LOGS):
        x0, x1 = ROW[0] + i * LOG, ROW[0] + (i + 1) * LOG
        a, b = max(x0, HOLLOW[0]), min(x1, HOLLOW[1])
        along = [a, *(x for x, _ in STATIONS if a < x < b), b]
        bottom = max(floor(1, x) for x in along)
        half = min(hull.half(x, y, inset=wall(1)) for x in along for y in (bottom, top(1)))
        name, joints = f"Hewn0Log{i + 1}", joints_of(i)
        pieces = [faced(box(f"{name}Bed", (x0, 0, CENTRE - BARE), (x1, bottom, CENTRE + BARE), LOG_TEXTURES, skip=joints),
                        {"down": (0, 0, LOG, 2 * BARE), "north": side_window(0, bottom), "south": side_window(0, bottom),
                         **ends_of_log(i, CENTRE - BARE, CENTRE + BARE, 0, bottom)})]
        for label, outer, z0, z1 in (("Port", "north", CENTRE - BARE, CENTRE - half), ("Starboard", "south", CENTRE + half, CENTRE + BARE)):
            pieces.append(faced(box(f"{name}{label}", (x0, bottom, z0), (x1, BARE_TOP, z1), LOG_TEXTURES, skip=("down", *joints)),
                                {outer: side_window(bottom, BARE_TOP), "up": top_window(x0, x1, x0, z0, z1), **ends_of_log(i, z0, z1, bottom, BARE_TOP)}))
        for f0, f1, inner, outer in ((x0, a, "east", "west"), (b, x1, "west", "east")):
            if f1 - f0 < 1e-6:
                continue
            solid = box(f"{name}Solid{outer.capitalize()}", (f0, bottom, CENTRE - half), (f1, BARE_TOP, CENTRE + half), LOG_TEXTURES,
                        skip=("north", "south", "down", *((outer,) if outer in joints else ())))
            solid["faces"][inner]["texture"] = "#" + WOOD
            pieces.append(faced(solid, {"up": top_window(f0, f1, x0, CENTRE - half, CENTRE + half),
                                        **{face: window for face, window in ends_of_log(i, CENTRE - half, CENTRE + half, bottom, BARE_TOP).items() if face == outer}}))
        sections[section_of(x0)].extend(pieces)
    return [group(f"Hewn0x{j}", LEVEL, LEVEL, pieces) for j, pieces in sections.items()]


def section_of(x):
    return 1 + sum(x >= cut for cut in CUTS)


def sliced(stations, a, b):
    hull = Hull("", stations, CENTRE, 0, WOOD, WOOD)
    return [(a, hull.section(a)), *(station for station in stations if a < station[0] < b), (b, hull.section(b))]


def cut(stations):
    hull = Hull("", stations, CENTRE, 0, WOOD, WOOD)
    return sorted([*stations, *((x, hull.section(x)) for x in CUTS if all(abs(x - at) > 1e-6 for at, _ in stations))])


def skin(name, stations, thickness, cuts):
    return Hull(f"{name}Side", stations, CENTRE, thickness, WOOD, WOOD, edge=WOOD).strakes(cuts)


def hewn_to(name, n, floors=True):
    """The hull as hewing n leaves it, one group a section, and its two ends: what a hewing's stages show apart. What
    the next hewing's inside work replaces, a section's floor or the bare trunk's logs, is its Inside."""
    if n == 0:
        logs = trunk()
        return [group(f"{name}x{j}", LEVEL, LEVEL, [group(f"{name}x{j}Inside", LEVEL, LEVEL, [log for log in logs if section_of(log["from"][0]) == j])])
                for j in range(1, len(SPANS) + 1)]
    sections, cuts = [], []
    for j, (a, b) in enumerate(SPANS, 1):
        stations = sliced(stations_at(n), a, b)
        inside = [group(f"{name}x{j}Inside", LEVEL, LEVEL, floor_planes(f"{name}x{j}Floor", stations, n, n))] if floors else []
        sections.append(group(f"{name}x{j}", LEVEL, LEVEL, [*skin(f"{name}x{j}", stations, wall(n), cuts), *inside]))
    return [*sections, *(group(f"{name}{side}", LEVEL, LEVEL, end(f"{name}{side}", slices, CENTRE, top(n))) for side, slices in ends_at(n).items())]


def floor_planes(name, stations, walls, deep):
    """The floor hewing deep leaves inside the walls hewing walls left."""
    return [water_plane(f"{name}{i}", x0, x1, CENTRE - half, CENTRE + half, height, WOOD)
            for i, (x0, x1, height, half) in enumerate(bed_of(stations, lambda x: floor(deep, x), lambda edges, height: wall(walls)), 1)]


def inside_hewn(n):
    """Hewing n's sections with their inside cut and their outside not yet. Its walls stay as they stood, since the
    next hewing's inner faces lie no further out than these, and its floor goes down to the next hewing's; the bare
    trunk gets a trench as deep and as wide as the first hewing's hollow."""
    if n == 0:
        return trench()
    return [group(f"Hewn{n}x{j}", LEVEL, LEVEL, floor_planes(f"Hewn{n}x{j}Floor", sliced(stations_at(n), a, b), n, n + 1)) for j, (a, b) in enumerate(SPANS, 1)]


def bark():
    """One piece a stroke, in the order EntityBehaviorBurnout strips them. Each face is mapped as the log block maps
    its own, so the row becomes the blank without a seam moving."""
    tops, sides = [], {-1: [], 1: []}
    for i in range(LOGS):
        x0, x1 = ROW[0] + i * LOG, ROW[0] + (i + 1) * LOG
        joints = (() if i == 0 else ("west",)) + (() if i == LOGS - 1 else ("east",))
        rings = {face: (0, 0, LOG, BARK_THICKNESS) for face in ("west", "east") if face not in joints}
        piece = box(f"Bark{i + 1}", (x0, BARE_TOP, CENTRE - LOG / 2), (x1, LOG, CENTRE + LOG / 2),
                    {**{face: BARK for face in FACES}, "west": RINGS, "east": RINGS}, skip=("down", *joints))
        tops.append(faced(piece, {"up": (0, 0, LOG, LOG), "north": (0, 0, LOG, BARK_THICKNESS), "south": (0, 0, LOG, BARK_THICKNESS), **rings}))
        for side in (-1, 1):
            outer, inner = ("north", "south") if side < 0 else ("south", "north")
            z0, z1 = sorted((CENTRE + side * LOG / 2, CENTRE + side * BARE))
            rings = {face: (0, BARK_THICKNESS, BARK_THICKNESS, LOG) for face in ("west", "east") if face not in joints}
            piece = box("", (x0, 0, z0), (x1, BARE_TOP, z1), {**{face: BARK for face in FACES}, "west": RINGS, "east": RINGS},
                        skip=(inner, *joints))
            sides[side].append(faced(piece, {outer: (0, BARK_THICKNESS, LOG, LOG), "up": (0, 0, LOG, BARK_THICKNESS),
                                             "down": (0, 0, LOG, BARK_THICKNESS), **rings}))
    pieces = [*tops, *sides[-1], *sides[1]]
    for k, piece in enumerate(pieces, 1):
        piece["name"] = f"Bark{k}"
    return pieces


def bed_of(stations, floor_at_x, inset):
    """The hollow's floor between each two of the stations: the height and half-width of each."""
    spans = []
    for (x0, e0), (x1, e1) in zip(stations, stations[1:]):
        height = max(floor_at_x(x0), floor_at_x(x1))
        spans.append((x0, x1, height, min(breadth(edges, height) - inset(edges, height) for edges in (e0, e1))))
    return spans


def beds(n):
    """The hollow's floor as hewing n leaves it, section by section."""
    if n == 0:
        return [[(x0, x1, BARE_TOP, BARE - wall(0)) for x0, x1 in pieces(span, CLAY_PIECE)] for span in SPANS]
    return [bed_of(sliced(stations_at(n), a, b), lambda x: floor(n, x), lambda edges, height: wall(n)) for a, b in SPANS]


def bed(n):
    return [span for section in beds(n) for span in section]


def pieces(span, length):
    a, b = span
    count = max(1, round((b - a) / length))
    step = (b - a) / count
    return [(a + step * k, a + step * (k + 1)) for k in range(count)]


def balls(span, width):
    a, b = span
    count = math.ceil((b - a) * width / CLAY_BALL_VOXELS)
    return [(a + (b - a) * k / count, a + (b - a) * (k + 1) / count) for k in range(count)]


def floor_at(n, x):
    return next(height for x0, x1, height, _ in bed(n) if x0 <= x <= x1)


def char(n):
    return [group(f"Char{n}x{j}", LEVEL, LEVEL, [water_plane(f"Char{n}x{j}Bed{i}", x0, x1, CENTRE - half, CENTRE + half, height + CHAR_LIFT, "char")
                                                  for i, (x0, x1, height, half) in enumerate(spans, 1)])
            for j, spans in enumerate(beds(n), 1)]


def grass(n):
    tufts = []
    for k, (x0, x1) in enumerate(pieces((HOLLOW[0] + 2, HOLLOW[1] - 2), (HOLLOW[1] - HOLLOW[0] - 4) / TUFTS), 1):
        middle, height = (x0 + x1) / 2, floor_at(n, (x0 + x1) / 2) + CHAR_LIFT * 2 + APART * (k % 2)
        yaw = (-1) ** k * (10 + 30 * seed(f"Grass{n}x{k}", "yaw"))
        across = (seed(f"Grass{n}x{k}", "across") - 0.5) * 3
        tufts.append(box(f"Grass{n}x{k}", (middle - 2.5, height, CENTRE + across - 1.2), (middle + 2.5, height + GRASS, CENTRE + across + 1.2),
                         "hay", origin=(middle, height, CENTRE + across), rot=(0, yaw, 0)))
    return tufts


def split_logs(name, n, k, thickness, share, tex):
    x0, x1 = pieces((HOLLOW[0] + 1, HOLLOW[1] - 1), (HOLLOW[1] - HOLLOW[0] - 2) / FIREWOOD)[k - 1]
    middle = (x0 + x1) / 2
    length = (x1 - x0 - 1) * share
    low = floor_at(n, middle) + CHAR_LIFT * 2 + GRASS + 2 * APART
    yaw = (-1) ** k * 4
    lay = [(-thickness / 2 - 0.15, low), (thickness / 2 + 0.15, low), (0, low + thickness + APART)]
    return [box(f"{name}{n}x{k}Split{j}", (middle - length / 2, y, CENTRE + z - thickness / 2), (middle + length / 2, y + thickness, CENTRE + z + thickness / 2),
                tex, origin=(middle, low, CENTRE), rot=(0, yaw + (j - 2) * 2, 0))
            for j, (z, y) in enumerate(lay, 1)]


def firewood(n):
    tex = {"north": "firewood", "south": "firewood", "up": "firewood", "down": "firewood", "west": "firewoodend", "east": "firewoodend"}
    return [group(f"Wood{n}x{k}", (0, 0, 0), (0, 0, 0), split_logs("Wood", n, k, SPLIT, 1, tex)) for k in range(1, FIREWOOD + 1)]


def fire(n):
    """One group per burning phase, At1 to At3, as EntityBehaviorBurnout counts them after the fire has caught."""
    phases = []
    for p in range(1, BURNING + 1):
        embers = [glowing(box(f"Fire{n}At{p}Embers{i}", (x0 + 0.5, height + CHAR_LIFT * 2, CENTRE - half * BED), (x1 - 0.5, height + CHAR_LIFT * 2 + EMBERS[p - 1], CENTRE + half * BED),
                              "ember"), GLOW[p - 1])
                  for i, (x0, x1, height, half) in enumerate(bed(n), 1) if half > 1 and x1 - x0 > 1]
        logs = [] if p == BURNING else [glowing(element, LOGS_GLOW) for k in range(1, FIREWOOD + 1)
                                        for element in split_logs(f"Fire{n}At{p}Wood", n, k, SPLIT * (1 if p == 1 else 0.6), 1 if p == 1 else 0.75, "burntlog")]
        phases.append(group(f"Fire{n}At{p}", (0, 0, 0), (0, 0, 0), [*embers, *logs]))
    return phases


def clay(n):
    """In the order the balls cover it, which EntityBehaviorBurnout reads: the stern, up the port side, the bow, back
    down starboard."""
    if n == 0:
        port = [box(f"Clay0Port{i}", (x0, BARE_TOP, CENTRE - BARE), (x1, BARE_TOP + CLAY, CENTRE - BARE + wall(0)), "clay",
                    skip=("west",) if i == 1 else ("east",) if x1 >= HOLLOW[1] else ())
                for i, (x0, x1) in enumerate((piece for span in SPANS for piece in pieces(span, CLAY_PIECE)), 1)]
        starboard = [mirrored(element, "Port", "Starboard") for element in port]
        caps = {name: [box(f"Clay0{name}{k}", (a, BARE_TOP, CENTRE - BARE), (b, BARE_TOP + CLAY, CENTRE + BARE), "clay")
                       for k, (a, b) in enumerate(balls((x0, x1), 2 * BARE), 1)]
                for name, (x0, x1) in (("Stern", (ROW[0], HOLLOW[0])), ("Bow", (HOLLOW[1], ROW[1])))}
    else:
        rail = Hull(f"Blank{n}Side", cut(stations_at(n)), CENTRE, wall(n), WOOD, WOOD).rail(f"Clay{n}", "clay", height=CLAY, thickness=wall(n))
        port, starboard = (joined(balled([element for element in rail if side in element["name"]])) for side in ("Port", "Starboard"))
        caps = {name: [box(f"Clay{n}{name}{k}x{i}", (a, top(n), CENTRE - upper), (b, top(n) + CLAY, CENTRE + upper), "clay")
                       for k, (x0, x1, upper, _, _) in enumerate(slices, 1)
                       for i, (a, b) in enumerate(balls((x0, x1), 2 * upper), 1)] for name, slices in ends_at(n).items()}
    return [*reversed(caps["Stern"]), *port, *caps["Bow"], *reversed(starboard)]


def balled(run):
    pieces_of = []
    for plank in run:
        board = plank["children"][0]
        (x0, y0, z0), (x1, y1, z1) = board["from"], board["to"]
        for i, (a, b) in enumerate(balls((x0, x1), abs(z1 - z0))):
            piece = box(f"{board['name']}x{i + 1}", (a, y0, z0), (b, y1, z1), "clay", origin=board.get("rotationOrigin", (0, 0, 0)))
            pieces_of.append(dict(plank, name=f"{board['name']}x{i + 1}Frame", children=[piece]))
    return pieces_of


def joined(run):
    for k, plank in enumerate(run):
        board = plank["children"][0]
        if k < len(run) - 1:
            board["faces"].pop("east", None)
        if k % 2:
            board["from"][1], board["to"][1] = num(board["from"][1] + APART), num(board["to"][1] - APART)
    return run


def mirrored(element, side, other):
    flipped = mirror_z(element)
    flipped["name"] = element["name"].replace(side, other)
    return flipped


def mirror_z(element):
    flipped = dict(element)
    flipped["from"] = [element["from"][0], element["from"][1], num(2 * CENTRE - element["to"][2])]
    flipped["to"] = [element["to"][0], element["to"][1], num(2 * CENTRE - element["from"][2])]
    flipped["faces"] = {{"north": "south", "south": "north"}.get(face, face): value for face, value in element["faces"].items()}
    return flipped


def glowing(element, glow):
    element["glow"] = glow
    return element


def build_stages():
    stages = {"Blank": [], "Hewn": [], "Char": [], "Grass": [], "Wood": [], "Fire": [], "Clay": []}
    for n in range(HOLLOWINGS):
        blank = hewn_to(f"Blank{n}", n)
        for name, children in (("Blank", blank), ("Hewn", inside_hewn(n)), ("Char", char(n)), ("Grass", grass(n)), ("Wood", firewood(n)),
                               ("Fire", fire(n)), ("Clay", clay(n))):
            stages[name].append(group(f"{name}{n}", LEVEL, LEVEL, children))
    return [group("Bark", LEVEL, LEVEL, bark()), *(group(name, LEVEL, LEVEL, children) for name, children in stages.items())]


def hewing_stages(stages, shell, n):
    """What each stage of hewing n shows, as EntityBehaviorBurnout shows it: the inside cut section by section from
    the stern, then the outside from the bow, each section's ends going with it."""
    sections = len(SPANS)
    part = lambda level, name: next(child for child in level["children"] if child["name"] == name)
    before, inside = stages["Blank"]["children"][n], stages["Hewn"]["children"][n]
    after = shell if n + 1 == HOLLOWINGS else stages["Blank"]["children"][n + 1]
    char = stages["Char"]["children"][n]
    shown = []
    for stage in range(2 * sections + 1):
        cut, trimmed = min(stage, sections), max(0, stage - sections)
        parts = []
        for j in range(1, sections + 1):
            if j > sections - trimmed:
                parts.append(part(after, f"{after['name']}x{j}"))
            elif j <= cut:
                section = part(before, f"Blank{n}x{j}")
                parts += [child for child in section["children"] if child["name"] != f"Blank{n}x{j}Inside"] + [part(inside, f"Hewn{n}x{j}")]
            else:
                parts += [part(before, f"Blank{n}x{j}"), part(char, f"Char{n}x{j}")]
        for side, j in (("Stern", 1), ("Bow", sections)):
            level = after if j > sections - trimmed else before
            parts += [child for child in level["children"] if child["name"] == f"{level['name']}{side}"]
        shown.append(parts)
    return shown


def hewn(shape, hull, shell, stages):
    """What the dugout's shapes follow: her finished hull, and at each hewing the trunk as that pass leaves it, with
    what each step of its fire and each stage of its hewing shows on it together."""
    shape.within(hull.outline("her hull, its end section carried on", ends=False), shell)
    shape.together(shell)
    level = lambda name, n: stages[name]["children"][n]
    shape.together(stages["Bark"], level("Blank", 0))
    for n in range(HOLLOWINGS):
        if n:
            loft = hewing(n).outline(f"the trunk hewn {n} times", ends=False)
            shape.within(loft, level("Blank", n), level("Hewn", n))
        laid, burning = [level("Blank", n), level("Clay", n), level("Grass", n), level("Wood", n)], [level("Blank", n), level("Clay", n), level("Char", n)]
        shape.together(*laid)
        for phase in level("Fire", n)["children"]:
            shape.together(*burning, phase)
        for stage, parts in enumerate(hewing_stages(stages, shell, n)):
            shape.together(*parts, *([level("Clay", n)] if stage <= len(SPANS) else []))


def volumes():
    step = 0.25
    envelope = hold = 0
    x = ENDS["Stern"][-1][0] + step / 2
    while x < ENDS["Bow"][-1][1]:
        y = finished_bottom(x) + step / 2
        while y < RIM:
            half = finished_half(x, y)
            envelope += 2 * half * step * step
            if hollowed(x) and y > floor(HOLLOWINGS, x):
                hold += 2 * max(0, half - WALL) * step * step
            y += step
        x += step
    trunk_litres = units.litres(TRUNK_METRES ** 2 * LOGS * TRUNK_METRES)
    return units.litres(units.metres(envelope, 3)), units.litres(units.metres(hold, 3)), trunk_litres


def deck():
    step, area, x = 0.25, 0, HOLLOW[0] + 0.125
    while x < HOLLOW[1]:
        area += 2 * hollow_half(x) * step
        x += step
    return units.metres(area, 2)


def walls():
    """Her walls as WALL draws them; a breaker bends a side as a cantilever, from her bottom's edge to her gunwale."""
    return {"behaviorConfigs": {"roughseas:vesselintegrity": {"shell": {"metres": units.metres(WALL), "voxels": WALL,
                                                                        **units.both("span", units.metres(RIM - PROFILE[1][1]))}}}}


def build(path):
    shape = Shape(path, {WOOD: "game:block/wood/debarked/oak", BARK: "game:block/wood/bark/oak-h", RINGS: "game:block/wood/treetrunk/oak",
                         "bilge": BILGE, "char": "game:block/wood/firepit/burntlog", "clay": "game:block/clay/blueclay",
                         "clay-red": "game:block/clay/redclay", "clay-fire": "game:block/clay/fireclay",
                         "ember": "game:block/coal/ember", "burntlog": "game:block/wood/firepit/burntlog", "hay": "game:block/hay/normal-side",
                         "firewood": "game:block/wood/firewood/side", "firewoodend": "game:block/wood/firewood/north"})
    hull = Hull("Side", STATIONS, CENTRE, WALL, WOOD, WOOD, edge=WOOD)
    shell = group("Shell", LEVEL, LEVEL, hewn_to("Shell", HOLLOWINGS, floors=False))
    shape.add(shell)
    stages = {stage["name"]: stage for stage in build_stages()}
    shape.add(*stages.values())
    hewn(shape, hull, shell, stages)
    shape.add(hull.hide_water(RIM - 0.3), hull.bilge(RIM - 0.3))
    box, measured = hull_box(shell)
    shape.add(box)
    record("dugout", merged(measured, walls()))
    shape.keep("FrontSeat", "BackSeat", "OarStorage")
    wound(shape, [("", [shell], WOOD), *((built["name"], [built], WOOD) for name in ("Blank", "Hewn") for built in stages[name]["children"])])
    envelope, hold, trunk_litres = volumes()
    print(f"dugout: trunk {trunk_litres:.0f} L, finished envelope {envelope:.0f} L, hold {hold:.0f} L, wood {envelope - hold:.0f} L, "
          f"hewn off outside {trunk_litres - envelope:.0f} L")
    burnout = {"hollowings": HOLLOWINGS, **units.both("hollowDepth", units.metres(RIM - WALL)), **units.both("rim", units.metres(RIM)),
               **units.both("blankDepth", units.metres(LOG)), **units.both("blankWaterplane", units.metres(LOGS * LOG * LOG, 2), 2),
               **units.both("unhewn", trunk_litres - envelope, 3), **units.both("hollowBeam", units.metres(2 * (PROFILE[-1][0] - WALL))),
               **units.both("hollowLength", units.metres(HOLLOW[1] - HOLLOW[0])),
               **units.both("wall", units.metres(WALL)), "barkPieces": 3 * LOGS,
               "hewnSectionsMetres": [[num(units.metres(x - ORIGIN[0])) for x in span] for span in SPANS],
               "hewnSectionsVoxels": [[num(x - ORIGIN[0]) for x in span] for span in SPANS]}
    record("dugout", merged(afloat("dugout", kilograms("wood-oak", envelope - hold), units.metres(RIM), hold, deck(), at_draft=windage([shell], box["from"][1])), measured, bilge_levels(shape.elements),
                            {"behaviorConfigs": {"roughseas:burnout": burnout}}))
    shape.write()
    crew_tools(shape, "dugout", ("Shell",))


def crew_tools(shape, vessel, walls, catch=None, fore=3, outboard=(0, 0), **given):
    at = seat(shape, "FrontSeat")
    record(vessel, {"behaviorConfigs": {"roughseas:propulsion": worked_from_middle(find(shape.elements, "Hull"), at, ARM_REACH_METRES)}})
    water_metres = waterline_metres(vessel)
    hull = Hull("Side", STATIONS, CENTRE, WALL, WOOD, WOOD)
    floor = hull.section(POLER_AT)[0][1] + WALL
    chine = hull.half(POLER_AT, floor + 1, inset=WALL)
    pole = poling(stands(vessel, units.metres(floor), outboard=outboard, **given), at, units.voxels(water_metres),
                  CENTRE - PROFILE[-1][0] - 1, (POLER_AT, floor, CENTRE), chine,
                  floor_at=lambda x, across: hull.floor(x, across, inset=WALL))
    player_patch(f"player-{vessel}", tools(shape, vessel, "FrontSeat", "roughseas-sitlegsforward", {"rotationZ": 6}, water_metres, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, 3.5, 1, catch=catch, fore=fore)),
        "pole": pole,
    }, walls))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
