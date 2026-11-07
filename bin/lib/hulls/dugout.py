import math
from held import ARM_REACH_METRES, worked_from_middle, PADDLE, paddling, poling, tools, waterline_metres
from stroke import find, player_patch, seat
from physics import afloat, kilograms, stands
from fit import APART
import units
from entity import merged, record
from shape import BILGE, FACES, ORIGIN, Hull, across, bilge, bilge_levels, corners_of, plank, bounds, hull_box, windage, Shape, box, breadth, group, num, seed, water_plane
from wounds import wound

LOG = 16
LOGS = 5
ROW = (8 - LOGS * LOG / 2, 8 + LOGS * LOG / 2)
CENTRE = 8
PROFILE = [(0, 0), (3.84, 0.4), (6.24, 2.0), (7.08, 4.7), (7.2, 9)]
WOOD = "inside"
HEWN = "hewn"
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
HOLLOW = (-22, 38)
POLER_AT = HOLLOW[0] + 5
CUTS = (ROW[0] + 2 * LOG, ROW[0] + 3 * LOG)
SPANS = list(zip((HOLLOW[0], *CUTS), (*CUTS, HOLLOW[1])))
STEP_STRIPS = 5
STEP_BANDS = 4
STEP_RUN = (1, 2.5)
LEVEL = (0, 0, 0)


def section(x, breadth, lift):
    return (x, [(h * breadth, y + lift * (1 - y / RIM)) for h, y in PROFILE])


# The section the log's end is left at: its bottom raked up 5 voxels, above her laden waterline, the face over it the
# log's own sawn end.
LOG_END = (0.72, 5)
RAKE = (0.3, 0.5, 0.7, 0.85, 1)
FOREFOOT = 0.8
SHOULDERS = ((-15, 0.92, 0.45), (31, 0.92, 0.45))


def rise(share):
    return (1 - math.sqrt(1 - (FOREFOOT * share) ** 2)) / (1 - math.sqrt(1 - FOREFOOT ** 2))


def raked(shoulder, end):
    x, wide, lift = shoulder
    return [section(x + (end - x) * share, wide + (LOG_END[0] - wide) * share, lift + (LOG_END[1] - lift) * rise(share)) for share in RAKE]


STATIONS = [
    *reversed(raked(SHOULDERS[0], ROW[0])),
    section(*SHOULDERS[0]),
    section(-5, 1, 0),
    section(8, 1, 0),
    section(21, 1, 0),
    section(*SHOULDERS[1]),
    *raked(SHOULDERS[1], ROW[1]),
]
BOWL_RINGS = 6
BOWL_STAVES = 8
PLATE = 0.45
RIM_PIECE = 2
SURFACE_PIECE = 0.5
SAMPLE = 0.05
CAP = 1.5
UNDER_BOWL = 1


def samples(a, b):
    count = max(1, math.ceil((b - a) / SAMPLE))
    return [a + (b - a) * k / count for k in range(count + 1)]


class Bowl:
    """One end of the hollow as hewing n leaves it: half an elliptical bowl built as the coracle's is, rings by height
    and staves by angle, its sides lapped into the walls; over the solid end round it the rim, and where the log ends
    its end grain."""

    def __init__(self, n, side, floored=None):
        self.n, self.side, self.t, self.top = n, side, wall(n), top(n)
        self.x, self.way = (HOLLOW[0], -1) if side == "Stern" else (HOLLOW[1], 1)
        self.skin = hewing(n)
        self.loft = self.skin.outline("the trunk", ends=False)
        if n == HOLLOWINGS:
            self.floor = self.skin.section(self.x)[0][1] + self.t
        else:
            spans = bed_of(sliced(stations_at(n), *SPANS[0 if side == "Stern" else -1]), lambda x: floor(n if floored is None else floored, x), lambda edges, height: self.t)
            self.floor = spans[0 if side == "Stern" else -1][2]
        self.length = self.skin.half(self.x, self.top, inset=self.t)
        self.end = ROW[0] if side == "Stern" else ROW[1]
        self.across = lambda y: self.skin.half(self.x, y, inset=self.t) + self.t / 8
        self.outer = lambda x, y: self.skin.half(x, y)

    @classmethod
    def trench(cls, side, end, floor, top, half):
        """The first hewing's trench in the square trunk, closed round: as wide as the trench, from its floor to the
        trunk's top, its tip at end."""
        bowl = cls.__new__(cls)
        bowl.n, bowl.side, bowl.t, bowl.top, bowl.floor, bowl.length, bowl.end = 0, side, wall(1), top, floor, half, end
        bowl.way = -1 if side == "Stern" else 1
        bowl.x = end - bowl.way * half
        bowl.across = lambda y: half + bowl.t / 8
        bowl.outer = lambda x, y: half
        return bowl

    def ring(self, j):
        angle = math.pi / 2 * j / BOWL_RINGS
        y = self.top - (self.top - self.floor) * math.cos(angle)
        return y, self.length * math.sin(angle), self.across(y)

    def point(self, j, k, out=0):
        y, along, across = self.ring(j)
        angle = math.pi * (k / BOWL_STAVES - 0.5)
        return (self.x + self.way * (along + out) * math.cos(angle), y + out, CENTRE + (across + out) * math.sin(angle))

    def hollow(self, y):
        depth = self.top - self.floor
        if not self.floor <= y <= self.top or depth <= 0:
            return 0
        return math.pi / 2 * self.length * math.sqrt(max(0, 1 - ((self.top - y) / depth) ** 2)) * self.skin.half(self.x, y, inset=self.t)

    def staves(self, name):
        drawn = []
        for j in range(BOWL_RINGS):
            for k in range(BOWL_STAVES):
                out = APART * (k % 2)
                a, b, c, d = self.point(j, k, out), self.point(j, k + 1, out), self.point(j + 1, k + 1, out), self.point(j + 1, k, out)
                drawn.append(self.stave(f"{name}Bowl{j + 1}x{k + 1}", a, b, c, d))
        return drawn

    def stave(self, name, a, b, c, d):
        rise, flare = across(a, b, [((p - q) + (r - s)) / 2 for p, q, r, s in zip(d, a, c, b)])
        overlap = max(0, (math.dist(c, d) - math.dist(a, b)) / 2)
        middle = [(p + q) / 2 for p, q in zip(a, b)]
        hollow = (self.x, middle[1], CENTRE)
        made = [plank(name, a, b, rise, self.t / 3, side * turn * flare, side, WOOD, HEWN, edge=WOOD, overlap=overlap)
                for side in (-1, 1) for turn in (-1, 1)]
        reach = lambda board: min(math.dist(d, p) for p in corners_of(board)) + min(math.dist(c, p) for p in corners_of(board))
        outward = lambda board: sum(math.dist(p, hollow) for p in corners_of(board)) / 8 > math.dist(middle, hollow)
        return min((board for board in made if outward(board)), key=reach)

    def mouth(self, x):
        along = abs(x - self.x) / self.length
        return 0 if along >= 1 else self.across(self.top) * math.sqrt(1 - along ** 2)

    def pieces(self):
        xs = samples(*sorted((self.x, self.end)))
        xs = xs[::-1] if self.side == "Stern" else xs
        rim = lambda x: (self.mouth(x), self.outer(x, self.top))
        drawn, start, seen = [], xs[0], [rim(xs[0])]
        for x in xs[1:]:
            seen.append(rim(x))
            if any(max(v) - min(v) > self.t / 3 for v in zip(*seen)) or abs(x - start) > RIM_PIECE:
                drawn.append(tuple(sorted((start, x))))
                start, seen = x, [rim(x)]
        if start != xs[-1]:
            drawn.append(tuple(sorted((start, xs[-1]))))
        return drawn

    def rim(self, name, low=None, high=None, tex=WOOD):
        """The top of the end in strips along it, from the bowl's mouth out to the hull's side and across beyond it, by
        side as the clay goes on; low and high lay the clay over it instead."""
        laid = {"across": [], "Port": [], "Starboard": []}
        for k, (a, b) in enumerate(self.pieces(), 1):
            xs = samples(a, b)
            top = self.top - 2 * APART - APART * (k % 3) if high is None else high + APART * (k % 3)
            bottom = top - PLATE if low is None else low + APART * (k % 3)
            edge, outer = min(self.mouth(x) for x in xs), min(self.outer(x, min(bottom, self.top)) for x in xs)
            if edge <= APART:
                laid["across"].append(box(f"{name}Rim{k}", (a, bottom, CENTRE - outer), (b, top, CENTRE + outer), tex))
            else:
                laid["Port"].append(box(f"{name}Rim{k}Port", (a, bottom, CENTRE - outer), (b, top, CENTRE - edge), tex))
                laid["Starboard"].append(box(f"{name}Rim{k}Starboard", (a, bottom, CENTRE + edge), (b, top, CENTRE + outer), tex))
        return laid

    def width(self, x, y):
        if not self.floor < y <= self.top:
            return 0
        along = self.length * math.sqrt(max(0, 1 - ((self.top - y) / (self.top - self.floor)) ** 2))
        ratio = abs(x - self.x) / along if along else 1
        return 0 if ratio >= 1 else self.across(y) * math.sqrt(1 - ratio ** 2)

    def surface(self, prefix, height, texture="null"):
        drawn, x = [], self.x
        tip = self.x + self.way * self.length
        while (tip - x) * self.way > APART:
            end = x + self.way * min(SURFACE_PIECE, abs(tip - x))
            half = max(self.width(at, height) for at in samples(*sorted((x, end))))
            if half > APART:
                drawn.append(water_plane(f"{prefix}{len(drawn) + 1}", *sorted((x, end)), CENTRE - half, CENTRE + half, height, texture))
            x = end
        return drawn

    def plates(self, name):
        return [piece for pieces in self.rim(name).values() for piece in pieces]

    def cap(self, name):
        x, edges = self.skin.stations[0] if self.side == "Stern" else self.skin.stations[-1]
        x0, x1 = sorted((x - self.way * APART, x - self.way * (APART + CAP)))
        outer = "west" if self.side == "Stern" else "east"
        drawn, y, rim = [], edges[0][1], edges[-1][1] - 5 * APART
        while y < rim - APART:
            high = y
            while high < rim and breadth(edges, high) - breadth(edges, y) < self.t / 2:
                high = min(rim, high + 0.05)
            half = breadth(edges, y)
            piece = box(f"{name}Cap{len(drawn) + 1}", (x0, y, CENTRE - half), (x1, high, CENTRE + half), {**{face: WOOD for face in FACES}, outer: RINGS})
            drawn.append(faced(piece, {outer: end_window(CENTRE - half, CENTRE + half, y, high)}))
            y = high
        return drawn


def log(hull):
    bowls = [Bowl(HOLLOWINGS, side) for side in ("Stern", "Bow")]
    return [hull.strakes(), *(part for bowl in bowls for part in (bowl.staves(bowl.side), bowl.plates(bowl.side), bowl.cap(bowl.side)))]


def hold_surface(prefix, height, texture="null"):
    hollow = Hull("", sliced(STATIONS, *HOLLOW), CENTRE, WALL, WOOD, WOOD)
    return [*hollow.surface(prefix, height, texture), *(plane for side in ("Stern", "Bow") for plane in Bowl(HOLLOWINGS, side).surface(f"{prefix}{side}", height, texture))]


def hold_water():
    """The hold's water mask and bilge levels, over the hollow's run and its ends' bowls."""
    floor = min(edges[0][1] for _, edges in sliced(STATIONS, *HOLLOW)) + WALL
    mask = RIM - 0.3
    return group("hideWater", LEVEL, LEVEL, hold_surface("hideWater", mask)), bilge(floor, mask, lambda prefix, height: hold_surface(prefix, height, "bilge"))


def mouth():
    stern, bow = (Bowl(HOLLOWINGS, side) for side in ("Stern", "Bow"))
    return HOLLOW[0] - stern.length, HOLLOW[1] + bow.length


def finished_bottom(x):
    return Hull("", STATIONS, CENTRE, WALL, WOOD, WOOD).section(x)[0][1]


def worked(n):
    return n / HOLLOWINGS


def top(n):
    return BARE_TOP + (RIM - BARE_TOP) * worked(n)


def wall(n):
    return WALL + (BARE - PROFILE[-1][0]) * (1 - worked(n))


def floor(n, x):
    return max(finished_bottom(x) * worked(n) + wall(n), top(n) - (RIM - WALL) * worked(n))


def hollowed(x):
    return HOLLOW[0] < x < HOLLOW[1]


def bare_section():
    return [(0, 0), (BARE, 0), *((BARE, y * BARE_TOP / RIM) for _, y in PROFILE[2:])]


def stations_at(n):
    f = worked(n)
    return [(x, [(h0 + (h - h0) * f, y0 + (y - y0) * f) for (h0, y0), (h, y) in zip(bare_section(), edges)]) for x, edges in STATIONS]


def hewing(n):
    return Hull(f"Blank{n}Side", stations_at(n), CENTRE, wall(n), WOOD, WOOD, edge=WOOD)


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


def trench_log(i):
    hull = Hull("", stations_at(1), CENTRE, wall(1), WOOD, WOOD)
    x0, x1 = ROW[0] + i * LOG, ROW[0] + (i + 1) * LOG
    a, b = max(x0, HOLLOW[0]), min(x1, HOLLOW[1])
    along = [a, *(x for x, _ in STATIONS if a < x < b), b]
    bottom = max(floor(1, x) for x in along)
    return a, b, bottom, min(hull.half(x, y, inset=wall(1)) for x in along for y in (bottom, top(1)))


def trench():
    """Its outer faces are mapped as the logs' faces, so the trunk's outside doesn't change while its inside is cut."""
    sections = {j: step(0, j) for j in range(1, len(SPANS) + 1)}
    for i in range(LOGS):
        x0, x1 = ROW[0] + i * LOG, ROW[0] + (i + 1) * LOG
        a, b, bottom, half = trench_log(i)
        name, joints = f"Hewn0Log{i + 1}", joints_of(i)
        pieces = [faced(box(f"{name}Bed", (x0, 0, CENTRE - BARE), (x1, bottom, CENTRE + BARE), {**LOG_TEXTURES, "up": HEWN}, skip=joints),
                        {"down": (0, 0, LOG, 2 * BARE), "north": side_window(0, bottom), "south": side_window(0, bottom),
                         **ends_of_log(i, CENTRE - BARE, CENTRE + BARE, 0, bottom)})]
        for label, outer, inner, z0, z1 in (("Port", "north", "south", CENTRE - BARE, CENTRE - half), ("Starboard", "south", "north", CENTRE + half, CENTRE + BARE)):
            pieces.append(faced(box(f"{name}{label}", (x0, bottom, z0), (x1, BARE_TOP, z1), {**LOG_TEXTURES, inner: HEWN}, skip=("down", *joints)),
                                {outer: side_window(bottom, BARE_TOP), "up": top_window(x0, x1, x0, z0, z1), **ends_of_log(i, z0, z1, bottom, BARE_TOP)}))
        for f0, f1, inner, outer in ((x0, a, "east", "west"), (b, x1, "west", "east")):
            if f1 - f0 < 1e-6:
                continue
            solid = box(f"{name}Solid{outer.capitalize()}", (f0, bottom, CENTRE - half), (f1, BARE_TOP, CENTRE + half), LOG_TEXTURES,
                        skip=("north", "south", "down", *((outer,) if outer in joints else ())))
            solid["faces"][inner]["texture"] = "#" + HEWN
            pieces.append(faced(solid, {"up": top_window(f0, f1, x0, CENTRE - half, CENTRE + half),
                                        **{face: window for face, window in ends_of_log(i, CENTRE - half, CENTRE + half, bottom, BARE_TOP).items() if face == outer}}))
        for side, end in (("Stern", a), ("Bow", b)):
            if end in HOLLOW:
                bowl = Bowl.trench(side, end, bottom, BARE_TOP, half)
                pieces += [*bowl.staves(f"{name}{side}"), *bowl.plates(f"{name}{side}")]
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
    return Hull(f"{name}Side", stations, CENTRE, thickness, WOOD, HEWN, edge=WOOD).strakes(cuts)


def hewn_to(name, n, floors=True):
    """A section's Inside is what EntityBehaviorBurnout hides once the next hewing has cut that section's inside."""
    if n == 0:
        logs = trunk()
        return [group(f"{name}x{j}", LEVEL, LEVEL, [group(f"{name}x{j}Inside", LEVEL, LEVEL, [log for log in logs if section_of(log["from"][0]) == j])])
                for j in range(1, len(SPANS) + 1)]
    sections, cuts, bowls = [], [], {side: Bowl(n, side) for side in ("Stern", "Bow")}
    for j, (a, b) in enumerate(SPANS, 1):
        side = "Stern" if j == 1 else "Bow" if j == len(SPANS) else None
        reach = (ROW[0] if j == 1 else a, ROW[1] if j == len(SPANS) else b)
        staves = bowls[side].staves(f"{name}x{j}") if side else []
        inside = [group(f"{name}x{j}Inside", LEVEL, LEVEL, [*floor_planes(f"{name}x{j}Floor", under_bowl(sliced(stations_at(n), a, b), j), n, n), *staves])] if floors else staves
        sections.append(group(f"{name}x{j}", LEVEL, LEVEL, [*skin(f"{name}x{j}", sliced(stations_at(n), *reach), wall(n), cuts), *inside]))
    return [*sections, *(group(f"{name}{side}", LEVEL, LEVEL, [*bowl.plates(f"{name}{side}"), *bowl.cap(f"{name}{side}")]) for side, bowl in bowls.items())]


def under_bowl(stations, j):
    (x0, first), (x1, last) = stations[0], stations[-1]
    return [*([(x0 - UNDER_BOWL, first)] if j == 1 else []), *stations, *([(x1 + UNDER_BOWL, last)] if j == len(SPANS) else [])]


def floor_planes(name, stations, walled, floored):
    """The floor hewing floored leaves, inside the walls hewing walled left."""
    return [water_plane(f"{name}{i}", x0, x1, CENTRE - half, CENTRE + half, height, HEWN)
            for i, (x0, x1, height, half) in enumerate(bed_of(stations, lambda x: floor(floored, x), lambda edges, height: wall(walled)), 1)]


def inside_hewn(n):
    """The walls stay as hewing n left them: the next hewing's inner faces lie no further out than these."""
    if n == 0:
        return trench()
    bowls = {1: Bowl(n, "Stern", n + 1), len(SPANS): Bowl(n, "Bow", n + 1)}
    return [group(f"Hewn{n}x{j}", LEVEL, LEVEL, [*floor_planes(f"Hewn{n}x{j}Floor", under_bowl(sliced(stations_at(n), a, b), j), n, n + 1),
                                                 *(bowls[j].staves(f"Hewn{n}x{j}") if j in bowls else []), *step(n, j)])
            for j, (a, b) in enumerate(SPANS, 1)]


def hewn_bed(n, j):
    if n == 0:
        _, _, bottom, half = trench_log(round((CUTS[j - 1] - ROW[0]) / LOG) - 1)
        return bottom, half
    _, _, height, half = bed_of(sliced(stations_at(n), *SPANS[j - 1]), lambda x: floor(n + 1, x), lambda edges, height: wall(n))[-1]
    return height, half


def step(n, j):
    """The face the axe leaves where section j, hewn, meets the char of the next: upright bands as wide as the hollow
    at each height, and before them a lip of strips worked back into the floor."""
    if j == len(SPANS):
        return []
    at = CUTS[j - 1]
    low, half = hewn_bed(n, j)
    high = beds(n)[j][0][2] + CHAR_LIFT
    rise, name = high - low, f"Hewn{n}x{j}Step"
    width = (lambda y0, y1: half) if n == 0 else (lambda y0, y1: min(hollow_half_at(n, at, y) for y in (y0, y1)))
    risers = [box(f"{name}Riser{k}", (at - APART, y0, CENTRE - width(y0, y1)), (at - APART, y1, CENTRE + width(y0, y1)), {"west": HEWN})
              for k, (y0, y1) in enumerate(pieces((low, high), rise / STEP_BANDS), 1)]
    lips = []
    for k, (z0, z1) in enumerate(pieces((CENTRE - half, CENTRE + half), 2 * half / STEP_STRIPS), 1):
        run = rise * (STEP_RUN[0] + (STEP_RUN[1] - STEP_RUN[0]) * seed(name, k))
        lips.append(box(f"{name}Lip{k}", (at - run, low, z0), (at - run + math.hypot(run, rise), low, z1), {"up": HEWN},
                        origin=(at - run, low, z0), rot=(0, 0, math.degrees(math.atan2(rise, run)))))
    return [group(name, LEVEL, LEVEL, [*risers, *lips])]


def hollow_half_at(n, x, y):
    return Hull("", stations_at(n), CENTRE, wall(n), WOOD, WOOD).half(x, y, inset=wall(n))


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
        rail = Hull(f"Blank{n}Side", cut(sliced(stations_at(n), *HOLLOW)), CENTRE, wall(n), WOOD, WOOD).rail(f"Clay{n}", "clay", height=CLAY, thickness=wall(n))
        port, starboard = (joined(balled([element for element in rail if side in element["name"]])) for side in ("Port", "Starboard"))
        caps = {side: Bowl(n, side).rim(f"Clay{n}{side}", low=top(n) + 4 * APART, high=top(n) + 4 * APART + CLAY, tex="clay") for side in ("Stern", "Bow")}
        stern, bow = caps["Stern"], caps["Bow"]
        return [*reversed(stern["across"]), *reversed(stern["Port"]), *port, *bow["Port"], *bow["across"], *reversed(bow["Starboard"]),
                *reversed(starboard), *stern["Starboard"]]
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
    """What each stage of hewing n shows, as EntityBehaviorBurnout.Hewing picks it: insides cut from the first
    section on, then outsides trimmed from the last."""
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
    step, hull = 0.25, Hull("", STATIONS, CENTRE, WALL, WOOD, WOOD)
    bowls = [Bowl(HOLLOWINGS, side) for side in ("Stern", "Bow")]
    envelope = hold = 0
    x = STATIONS[0][0] + step / 2
    while x < STATIONS[-1][0]:
        y = finished_bottom(x) + step / 2
        while y < RIM:
            envelope += 2 * hull.half(x, y) * step * step
            if hollowed(x) and y > floor(HOLLOWINGS, x):
                hold += 2 * hull.half(x, y, inset=WALL) * step * step
            y += step
        x += step
    y = step / 2
    while y < RIM:
        hold += sum(bowl.hollow(y) for bowl in bowls) * step
        y += step
    trunk_litres = units.litres(TRUNK_METRES ** 2 * LOGS * TRUNK_METRES)
    return units.litres(units.metres(envelope, 3)), units.litres(units.metres(hold, 3)), trunk_litres


def deck():
    step, hull = 0.25, Hull("", STATIONS, CENTRE, WALL, WOOD, WOOD)
    area, x = 0, HOLLOW[0] + step / 2
    while x < HOLLOW[1]:
        area += 2 * hull.half(x, RIM, inset=WALL) * step
        x += step
    area += sum(math.pi / 2 * bowl.length * bowl.skin.half(bowl.x, RIM, inset=WALL) for bowl in (Bowl(HOLLOWINGS, side) for side in ("Stern", "Bow")))
    return units.metres(area, 2)


def level_boxes(stages):
    boxes = [bounds(stages["Blank"]["children"][n], *([stages["Bark"]] if n == 0 else [])) for n in range(HOLLOWINGS)]
    return {"hullBoxesVoxels": [{"from": frm, "to": to} for frm, to in boxes],
            "hullBoxesMetres": [{end: [num(units.metres(c - o)) for c, o in zip(corner, ORIGIN)] for end, corner in (("from", frm), ("to", to))}
                                for frm, to in boxes]}


def walls():
    """Her walls as WALL draws them; a breaker bends a side as a cantilever, from her bottom's edge to her gunwale."""
    return {"behaviorConfigs": {"roughseas:vesselintegrity": {"shell": {"metres": units.metres(WALL), "voxels": WALL,
                                                                        **units.both("span", units.metres(RIM - PROFILE[1][1]))}}}}


def build(path):
    shape = Shape(path, {WOOD: "game:block/wood/debarked/oak", HEWN: "game:block/wood/charred", BARK: "game:block/wood/bark/oak-h", RINGS: "game:block/wood/treetrunk/oak",
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
    shape.add(*hold_water())
    box, measured = hull_box(shell)
    shape.add(box)
    record("dugout", merged(measured, walls()))
    shape.keep("FrontSeat", "BackSeat", "OarStorage")
    trench = stages["Hewn"]["children"][0]
    wound(shape, [("", [shell], WOOD), *((built["name"], [built], WOOD) for built in [*stages["Blank"]["children"], trench])])
    envelope, hold, trunk_litres = volumes()
    print(f"dugout: trunk {trunk_litres:.0f} L, finished envelope {envelope:.0f} L, hold {hold:.0f} L, wood {envelope - hold:.0f} L, "
          f"hewn off outside {trunk_litres - envelope:.0f} L")
    burnout = {"hollowings": HOLLOWINGS, **units.both("hollowDepth", units.metres(RIM - WALL)), **units.both("rim", units.metres(RIM)),
               **units.both("blankDepth", units.metres(LOG)), **units.both("blankWaterplane", units.metres(LOGS * LOG * LOG, 2), 2),
               **units.both("unhewn", trunk_litres - envelope, 3), **units.both("hollowBeam", units.metres(2 * (PROFILE[-1][0] - WALL))),
               **units.both("hollowLength", units.metres(HOLLOW[1] - HOLLOW[0])),
               **units.both("wall", units.metres(WALL)), "barkPieces": 3 * LOGS,
               "hewnSectionsMetres": [[num(units.metres(x - ORIGIN[0])) for x in span] for span in SPANS],
               "hewnSectionsVoxels": [[num(x - ORIGIN[0]) for x in span] for span in SPANS],
               **level_boxes(stages)}
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
