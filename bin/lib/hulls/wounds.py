import json
import math

from physics import ASSETS
from shape import box, group, seed
from transform import apply, direct, identity, local, mul, rotation, unapply

# The places, their shares and the forms are the game's: HullPlace, HullShape and HullDamage.WoundForms, held together
# by VesselShapeTests.
PLACES = ("PortBow", "StarboardBow", "PortSide", "StarboardSide", "PortQuarter", "StarboardQuarter", "Bottom")
FORMS = ("Scratched", "Cracked", "Split", "StoveIn")
END_SHARE = 0.4
BOTTOM_SHARE = 0.25
MARK_REACH = 0.8
SIDE_REACH = 0.9
BREACH = "breach"
PROUD = 0.06
WALL = 1.5
SHORTEST = 1.5
LENGTHS = {"Scratched": 0.3, "Cracked": 0.6, "Split": 1}
GAPS = {"Scratched": 0.1, "Cracked": 0.2, "Split": 0.45}
SEGMENTS = 3
WANDER = 0.6
LIP = 1.2
MIN_LIP = 0.2
HOLE_SHARE = 0.5
TAIL_SHARE = 0.25
SPRUNG_DEGREES = {"carved": 12, "rigid": 15, "skin": 50}


class HullBox:
    def __init__(self, element):
        (x0, y0, z0), (x1, y1, z1) = element["from"], element["to"]
        self.half_length, self.half_beam, self.depth = (x1 - x0) / 2, (z1 - z0) / 2, y1 - y0
        self.along, self.across, self.keel = (x0 + x1) / 2, (z0 + z1) / 2, y0

    def place(self, point):
        along, height, across = point[0] - self.along, point[1] - self.keel, point[2] - self.across
        if height < BOTTOM_SHARE * self.depth and abs(across) < self.half_beam / 2:
            return "Bottom"
        end = "Bow" if along < -END_SHARE * self.half_length else "Quarter" if along > END_SHARE * self.half_length else "Side"
        return ("Port" if across < 0 else "Starboard") + end

    def point(self, place):
        if place == "Bottom":
            return self.along, self.keel, self.across
        along = -MARK_REACH if place.endswith("Bow") else MARK_REACH if place.endswith("Quarter") else 0
        across = -SIDE_REACH if place.startswith("Port") else SIDE_REACH
        return self.along + along * self.half_length, self.keel + self.depth / 2, self.across + across * self.half_beam

    @staticmethod
    def outward(place):
        return (0, -1, 0) if place == "Bottom" else (0, 0, -1 if place.startswith("Port") else 1)


def wound(shape, levels, lining=0):
    """Draws, on each level of the hull's skin a build shows, a wound at each place on the board nearest the place's
    mark, at every stage its crack can reach: Wound{Place}{Form}{level} under Wound{Place}{level}, beside the board
    in its frame, through its wall and the lining inside it. A level is (name, elements, texture); the finished hull's
    is named ""."""
    entity = json.loads((ASSETS / "entities/vessel" / shape.path.name).read_text())
    shell = entity["behaviorConfigs"]["roughseas:vesselintegrity"]["shell"]
    span = shell["spanMetres"] * 16
    sprung = SPRUNG_DEGREES[shell.get("kind", "rigid")]
    hull = HullBox(next(element for element in shape.elements if element["name"] == "Hull"))
    for level, elements, texture in levels:
        boards = list(skin(elements, texture))
        for place in PLACES:
            board, m, siblings = host(boards, hull, place)
            frame = Frame(board, m, hull.outward(place), hull.point(place), lining)
            forms = [group(f"Wound{place}{form}{level}", (0, 0, 0), (0, 0, 0),
                           draw(frame, f"Wound{place}{form}{level}", form, span, sprung, texture))
                     for form, draw in zip(FORMS, (crack_of, crack_of, split, stove_in))]
            siblings.append(beside(board, f"Wound{place}{level}", forms))
    shape.textures[BREACH] = "game:block/black"


def beside(board, name, children):
    """A group with the board's own place and turn, so its children lie in the board's frame."""
    wound = {"name": name, "from": board["from"], "to": board["from"]}
    for key in ("rotationOrigin", "rotationX", "rotationY", "rotationZ"):
        if key in board:
            wound[key] = board[key]
    return {**wound, "faces": {}, "children": children}


def host(boards, hull, place):
    target = hull.point(place)
    placed = [found for found in boards if hull.place(centre(*found[:2])) == place] or boards
    return min(placed, key=lambda found: math.dist(centre(*found[:2]), target))


def skin(elements, texture, parent=None):
    for element in list(elements):
        m = mul(parent or identity(), local(element, {}))
        if any(face["texture"] == "#" + texture for face in element.get("faces", {}).values()):
            yield element, m, elements
        yield from skin(element.get("children", []), texture, m)


def size(element):
    return [b - a for a, b in zip(element["from"], element["to"])]


def centre(element, m):
    return tuple(c * 16 for c in apply(m, [s / 32 for s in size(element)]))


class Frame:
    """A board's own axes: through it, the one facing out of the hull toward the place; along it, of the other two,
    the one lying most along the hull, as the grain does; and across it. A board thicker than a wall, a solid log, is
    wounded in its face toward the place, as deep as a wall. A lining against the skin's inside breaks with it."""

    def __init__(self, board, m, outward, target, lining):
        sizes = size(board)
        directions = [direct(m, [float(i == axis) for i in range(3)]) for axis in range(3)]
        facing = [sum(d * o for d, o in zip(direction, outward)) / math.hypot(*direction) for direction in directions]
        self.through = max(range(3), key=lambda axis: abs(facing[axis]))
        rest = [axis for axis in range(3) if axis != self.through]
        self.along = max(rest, key=lambda axis: abs(directions[axis][0]) / math.hypot(*directions[axis]))
        self.across = next(axis for axis in rest if axis != self.along)
        self.length, self.breadth = sizes[self.along], sizes[self.across]
        self.inward = -1 if facing[self.through] > 0 else 1
        depth = min(sizes[self.through], WALL)
        self.wall = (0, depth + lining) if self.inward > 0 else (sizes[self.through] - depth - lining, sizes[self.through])
        self.thickness = depth
        aimed = [c * 16 for c in unapply(m, [t / 16 for t in target])]
        self.aim = (aimed[self.along], aimed[self.across])

    def middle(self, length, breadth):
        return tuple(min(max(aim, extent / 2 + 0.5), size - extent / 2 - 0.5) if size > extent + 1 else size / 2
                     for aim, extent, size in zip(self.aim, (length, breadth), (self.length, self.breadth)))

    def through_wall(self):
        return self.wall[0] - PROUD, self.wall[1] + PROUD

    def inner(self, offset=0):
        face = self.wall[1] if self.inward > 0 else self.wall[0]
        return face + self.inward * offset

    def piece(self, name, along, across, through, texture, *, hinge=None, degrees=0):
        frm, to = [0, 0, 0], [0, 0, 0]
        for axis, (low, high) in ((self.along, along), (self.across, across), (self.through, sorted(through))):
            frm[axis], to[axis] = low, high
        if hinge is None:
            return box(name, frm, to, texture, grain="xyz"[self.along])
        origin = list(frm)
        origin[self.across], origin[self.through] = hinge
        rot = [0, 0, 0]
        rot[self.along] = degrees * self.swing()
        return box(name, frm, to, texture, origin=origin, rot=tuple(rot), grain="xyz"[self.along])

    def swing(self):
        unit = [float(axis == self.across) for axis in range(3)]
        turned = direct(rotation("XYZ"[self.along], 90), unit)
        return 1 if turned[self.through] * self.inward > 0 else -1


def crack(frame, name, length, gap, middle):
    mid_along, mid_across = middle
    step = length / SEGMENTS
    start = mid_along - length / 2
    return [frame.piece(f"{name}x{n}", (start + step * (n - 1), start + step * n),
                        (mid_across + offset - gap / 2, mid_across + offset + gap / 2), frame.through_wall(), BREACH)
            for n in range(1, SEGMENTS + 1)
            for offset in [(seed(name, n) - 0.5) * WANDER]]


def reach(frame, share, span):
    return min(max(share * span, SHORTEST), frame.length - 1)


def crack_of(frame, name, form, span, sprung, texture):
    length = reach(frame, LENGTHS[form], span)
    return crack(frame, name, length, GAPS[form], frame.middle(length, WANDER + GAPS[form]))


def split(frame, name, form, span, sprung, texture):
    gap, length = GAPS[form], reach(frame, LENGTHS[form], span)
    lip = min(LIP, frame.breadth / 2 - gap / 2 - WANDER / 2)
    along, across = frame.middle(length, 2 * (gap / 2 + WANDER / 2 + lip))
    edge = across + gap / 2 + WANDER / 2
    step = max(0.3, frame.thickness / 2)
    lips = [frame.piece(f"{name}Lip", (along - length / 2, along + length / 2), (edge, edge + lip),
                        (frame.inner(-frame.thickness + step), frame.inner(step)), texture)] if lip >= MIN_LIP else []
    return [*crack(frame, name, length, gap, (along, across)), *lips]


def stove_in(frame, name, form, span, sprung, texture):
    broken = reach(frame, 1, span)
    length, tail = broken / (1 + 2 * TAIL_SHARE), broken * TAIL_SHARE / (1 + 2 * TAIL_SHARE)
    hole = max(1, min(HOLE_SHARE * frame.breadth, frame.breadth - 0.6))
    middle, mid_across = frame.middle(broken, hole)
    along = (middle - length / 2, middle + length / 2)
    across = (mid_across - hole / 2, mid_across + hole / 2)
    hinge = (across[0], frame.inner())
    return [frame.piece(f"{name}Breach", along, across, frame.through_wall(), BREACH),
            frame.piece(f"{name}Shard", (along[0] + 0.1, along[1] - 0.1), across,
                        (frame.inner(PROUD), frame.inner(PROUD + frame.thickness)), texture, hinge=hinge, degrees=sprung),
            *crack(frame, f"{name}Bow", tail, GAPS["Split"], (along[0] - tail / 2, mid_across)),
            *crack(frame, f"{name}Stern", tail, GAPS["Split"], (along[1] + tail / 2, mid_across))]
