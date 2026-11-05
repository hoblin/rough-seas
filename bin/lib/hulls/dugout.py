from held import ARM_REACH_METRES, worked_from_middle, PADDLE, paddling, poling, tools, waterline_metres
from stroke import find, player_patch, seat
from physics import afloat, kilograms, stands
from fit import APART
import units
from entity import merged, record
from shape import BILGE, FACES, Hull, bilge_levels, hull_box, windage, Shape, box, breadth, group, num, seed, water_plane
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


def trunk():
    logs = []
    for i in range(LOGS):
        x0 = ROW[0] + i * LOG
        skip = (() if i == 0 else ("west",)) + (() if i == LOGS - 1 else ("east",))
        tex = {"north": WOOD, "south": WOOD, "up": WOOD, "down": BARK, "west": RINGS, "east": RINGS}
        element = box(f"Blank0x{i + 1}", (x0, 0, CENTRE - BARE), (x0 + LOG, BARE_TOP, CENTRE + BARE), tex, skip=skip)
        ends = {face: (BARK_THICKNESS, BARK_THICKNESS, LOG - BARK_THICKNESS, LOG) for face in ("west", "east") if face not in skip}
        logs.append(faced(element, {"down": (0, 0, LOG, 2 * BARE), **ends}))
    return logs


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


def blank(n):
    if n == 0:
        return trunk()
    hull = hewing(n)
    return [*hull.strakes(), *(element for name, slices in ends_at(n).items() for element in end(f"Blank{n}{name}", slices, CENTRE, top(n)))]


def bed(n):
    """The hollow's floor as hewing n leaves it, between each two stations: the height and half-width of each."""
    if n == 0:
        return [(x0, x1, BARE_TOP, BARE - wall(0)) for x0, x1 in pieces(HOLLOW, CLAY_PIECE)]
    stations, t = stations_at(n), wall(n)
    spans = []
    for (x0, e0), (x1, e1) in zip(stations, stations[1:]):
        height = max(floor(n, x0), floor(n, x1))
        spans.append((x0, x1, height, min(breadth(e0, height), breadth(e1, height)) - t))
    return spans


def pieces(span, length):
    a, b = span
    count = max(1, round((b - a) / length))
    step = (b - a) / count
    return [(a + step * k, a + step * (k + 1)) for k in range(count)]


def floor_at(n, x):
    return next(height for x0, x1, height, _ in bed(n) if x0 <= x <= x1)


def char(n):
    return [water_plane(f"Char{n}x{i}", x0, x1, CENTRE - half, CENTRE + half, height + CHAR_LIFT, "char")
            for i, (x0, x1, height, half) in enumerate(bed(n), 1)]


def floor_planes(n):
    if n == 0:
        return []
    return [water_plane(f"Blank{n}Floor{i}", x0, x1, CENTRE - half, CENTRE + half, height, WOOD) for i, (x0, x1, height, half) in enumerate(bed(n), 1)]


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
                    skip=("west",) if i == 1 else ("east",) if x1 >= HOLLOW[1] else ()) for i, (x0, x1) in enumerate(pieces(HOLLOW, CLAY_PIECE), 1)]
        starboard = [mirrored(element, "Port", "Starboard") for element in port]
        caps = {name: [box(f"Clay0{name}", (x0, BARE_TOP, CENTRE - BARE), (x1, BARE_TOP + CLAY, CENTRE + BARE), "clay")]
                for name, (x0, x1) in (("Stern", (ROW[0], HOLLOW[0])), ("Bow", (HOLLOW[1], ROW[1])))}
    else:
        rail = hewing(n).rail(f"Clay{n}", "clay", height=CLAY, thickness=wall(n))
        port, starboard = (joined([element for element in rail if side in element["name"]]) for side in ("Port", "Starboard"))
        caps = {name: [box(f"Clay{n}{name}{k}", (x0, top(n), CENTRE - upper), (x1, top(n) + CLAY, CENTRE + upper), "clay")
                       for k, (x0, x1, upper, _, _) in enumerate(slices, 1)] for name, slices in ends_at(n).items()}
    return [*reversed(caps["Stern"]), *port, *caps["Bow"], *reversed(starboard)]


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
    stages = {"Blank": [], "Char": [], "Grass": [], "Wood": [], "Fire": [], "Clay": []}
    for n in range(HOLLOWINGS):
        for name, children in (("Blank", [*blank(n), *floor_planes(n)]), ("Char", char(n)), ("Grass", grass(n)), ("Wood", firewood(n)),
                               ("Fire", fire(n)), ("Clay", clay(n))):
            stages[name].append(group(f"{name}{n}", (0, 0, 0), (0, 0, 0), children))
    return [group("Bark", (0, 0, 0), (0, 0, 0), bark()), *(group(name, (0, 0, 0), (0, 0, 0), children) for name, children in stages.items())]


def hewn(shape, hull, shell, stages):
    """What the dugout's shapes follow: her finished hull, and at each hewing the trunk as that pass leaves it, with
    what each step of its fire shows on it together."""
    shape.within(hull.outline("her hull, its end section carried on", ends=False), shell)
    shape.together(shell)
    level = lambda name, n: stages[name]["children"][n]
    shape.together(stages["Bark"], level("Blank", 0))
    for n in range(HOLLOWINGS):
        if n:
            shape.within(hewing(n).outline(f"the trunk hewn {n} times", ends=False), *blank(n))
        laid, burning = [level("Blank", n), level("Clay", n), level("Grass", n), level("Wood", n)], [level("Blank", n), level("Clay", n), level("Char", n)]
        shape.together(*laid)
        for phase in level("Fire", n)["children"]:
            shape.together(*burning, phase)
        shape.together(level("Blank", n), level("Char", n))


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
    shell = group("Shell", (0, 0, 0), (0, 0, 0), [element for piece in log(hull) for element in (piece if isinstance(piece, list) else [piece])])
    shape.add(shell)
    stages = {stage["name"]: stage for stage in build_stages()}
    shape.add(*stages.values())
    hewn(shape, hull, shell, stages)
    shape.add(hull.hide_water(RIM - 0.3), hull.bilge(RIM - 0.3))
    box, measured = hull_box(shell)
    shape.add(box)
    record("dugout", merged(measured, walls()))
    shape.keep("FrontSeat", "BackSeat", "OarStorage")
    wound(shape, [("", [shell], WOOD), *((level["name"], [level], WOOD) for level in stages["Blank"]["children"])])
    envelope, hold, trunk_litres = volumes()
    print(f"dugout: trunk {trunk_litres:.0f} L, finished envelope {envelope:.0f} L, hold {hold:.0f} L, wood {envelope - hold:.0f} L, "
          f"hewn off outside {trunk_litres - envelope:.0f} L")
    burnout = {"hollowings": HOLLOWINGS, **units.both("hollowDepth", units.metres(RIM - WALL)), **units.both("rim", units.metres(RIM)),
               **units.both("blankDepth", units.metres(LOG)), **units.both("blankWaterplane", units.metres(LOGS * LOG * LOG, 2), 2),
               **units.both("unhewn", trunk_litres - envelope, 3), **units.both("hollowBeam", units.metres(2 * (PROFILE[-1][0] - WALL))),
               **units.both("hollowLength", units.metres(HOLLOW[1] - HOLLOW[0])),
               **units.both("wall", units.metres(WALL)), "barkPieces": 3 * LOGS}
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
