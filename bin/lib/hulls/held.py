import json
import math

from physics import SWIM_LINE_METRES
from stroke import CENTRE, ROOT, Rower, centre, drive_share, fit, moved, named, posed, seat, turn
from transform import angles, apply, euler, mul, nearest, translation, undirect

ASSETS = ROOT / "RoughSeas/assets/roughseas"
ANCHOR = "ItemAnchor"
HAND_POINT_ROTATION = (0, -180, 0)
FRAMES = 30
TOOL = "Tool"
RIGHT = "ToolRight"
LEFT = "ToolLeft"
BLADE = "Blade"
STARBOARD = {"R": RIGHT, "L": LEFT}
PORT = {"R": LEFT, "L": RIGHT}
LOOPED = {"sculling": 2}


def inverse(m):
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    t = [-sum(r[i][k] * m[k][3] for k in range(3)) for i in range(3)]
    return [r[0] + [t[0]], r[1] + [t[1]], r[2] + [t[2]], [0, 0, 0, 1]]


def marker(name, point):
    return {"name": name, "from": list(point), "to": list(point), "faces": {}}


class Tool:

    def __init__(self, item, shape, right, left):
        transform = json.loads((ASSETS / "itemtypes" / f"{item}.json").read_text())["tpHandTransform"]
        if "origin" not in transform or any(transform["origin"].get(axis, 0) for axis in "xyz") or transform.get("scale", 1) != 1:
            raise ValueError(f"{item}: a held tool's tpHandTransform turns about its hand point, unscaled")
        rotation = [transform.get("rotation", {}).get(axis, 0) + r for axis, r in zip("xyz", HAND_POINT_ROTATION)]
        self.placing = mul(translation([transform.get("translation", {}).get(axis, 0) for axis in "xyz"]), euler(rotation))
        self.elements = json.loads((ASSETS / "shapes/item" / f"{shape}.json").read_text())["elements"]
        self.right, self.left = right, left

    def group(self):
        return {"name": TOOL, "from": [0, 0, 0], "to": [0, 0, 0], "rotationOrigin": [0, 0, 0], "faces": {},
                "children": self.elements + [marker(RIGHT, self.right), marker(LEFT, self.left)]}

    def held_at(self, hand, turned):
        reach = turn(turned, self.right)
        return {TOOL: moved((0, 0, 0), (0, 0, 0), [h - r for h, r in zip(hand, reach)], turned)}


def lowest(matrices, elements):
    corners = []
    for element in elements:
        f, t = element["from"], element["to"]
        corners += [apply(matrices[element["name"]], [(c[i] - f[i]) / 16 for i in range(3)])
                    for c in ((x, y, z) for x in (f[0], t[0]) for y in (f[1], t[1]) for z in (f[2], t[2]))]
    return min(corner[1] for corner in corners)


# The game draws a held item from the hand's ItemAnchor through the item's tpHandTransform (EntityShapeRenderer.RenderItem),
# so turning ItemAnchor puts the tool where the stroke has it.
def wrist(rower, tool, body, matrix):
    lower = rower.arms["R"].frames(body)[-1]
    anchor = rower.chain(ANCHOR)[-1]
    wanted = mul(mul(inverse(lower), matrix), inverse(tool.placing))
    origin = [c / 16 for c in anchor.get("rotationOrigin", (0, 0, 0))]
    turned = angles(wanted)
    rest = undirect(wanted, [wanted[i][3] - origin[i] for i in range(3)])
    shift = [16 * (r + o) - f for r, o, f in zip(rest, origin, anchor["from"])]
    return {"rotation" + a: round(v, 2) for a, v in zip("XYZ", turned)} | {"offset" + a: round(v, 3) for a, v in zip("XYZ", shift)}


def held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, waterline):
    rower = Rower(seat(shape, seated), idle)
    group = tool.group()
    plan = named(f"{vessel}-{kind}", FRAMES, {name: pose for name, (pose, _) in poses.items()}, stroke,
                 LOOPED.get(stroke, 1))
    for step, (pose, grips) in zip(plan, poses.values()):
        step["grips"] = grips
    _, player = fit(rower, [group], plan, STARBOARD, lean)
    offset = [(s - c) / 16 for s, c in zip(rower.seat, CENTRE)]
    reference = None
    for step, animation in zip(plan, player):
        deepest = math.inf
        previous = reference
        abeam = []
        for keyframe, frame in zip(animation["keyframes"], step["keys"]):
            matrices = posed([group], lambda name, pose=step["pose"](frame): pose.get(name, {}))
            if step["code"].endswith("starboard-forward") and frame < FRAMES * drive_share(stroke):
                abeam.append(abs(centre(tool.elements, matrices, BLADE)[2] - rower.seat[2]) / 16)
            body = keyframe["elements"]
            anchor = wrist(rower, tool, body, mul(translation([-o for o in offset]), matrices[TOOL]))
            if previous is not None:
                turned = nearest([anchor["rotation" + a] for a in "XYZ"], previous)
                anchor |= {"rotation" + a: round(v, 2) for a, v in zip("XYZ", turned)}
            previous = [anchor["rotation" + a] for a in "XYZ"]
            reference = reference or previous
            keyframe["elements"][ANCHOR] = anchor
            deepest = min(deepest, lowest(matrices, tool.elements) - waterline)
        print(f"{animation['code']}: the tool reaches {-deepest:.2f} m under the water" if deepest < 0
              else f"{animation['code']}: the tool stays {deepest:.2f} m over the water")
        if step["code"].endswith("starboard-forward"):
            print(f"{animation['code']}: the blade drives {sum(abeam) / len(abeam):.2f} m abeam of the seat")
    return player


PADDLE = Tool("tool/paddle", "paddle", (13, 8, 8), (0.5, 8, 8))
POLE = Tool("resource/pole", "pole-3", (12.8, 0.75, 8), (8, 0.75, 8))


def waterline(vessel):
    entity = json.loads((ASSETS / "entities/vessel" / f"{vessel}.json").read_text())
    return entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]


def tools(shape, vessel, seated, idle, lean, line, by_kind):
    return [animation for kind, (stroke, tool, poses) in by_kind.items()
            for animation in held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, line)]


def phase(frame, drive):
    u = frame / FRAMES
    driving = u < drive
    f = u / drive if driving else (u - drive) / (1 - drive)
    ease = 0.5 - 0.5 * math.cos(math.pi * f)
    return (ease if driving else 1 - ease), (0 if driving else math.sin(math.pi * f))


def eased(u):
    return 0.5 - 0.5 * math.cos(math.pi * min(1, max(0, u)))


def paddling(tool, at, ahead, behind, above, out, lift=6, catch=None, fore=3):
    x, y, z = at
    catch = ahead if catch is None else catch
    sides = {-1: out, 1: 2 * z - out}

    def placed(hand, lean, pitch):
        return tool.held_at(hand, {"rotationX": lean, "rotationZ": pitch})

    def ready(side):
        return (x + (behind - ahead) / 2, y + above + 2, sides[side] - side), -side * 62

    def stroke(side):
        def at_frame(frame):
            reach, rise = phase(frame, drive_share("paddling"))
            hand = (x - catch + (catch + behind) * reach, y + above + 2 * (1 - reach) ** 2 + 4.5 * rise, sides[side] + side * rise)
            return placed(hand, -side * (30 + 20 * rise), -95 + 40 * reach)

        return at_frame

    def switch(side):
        (start, lean), (end, _) = ready(-side), ready(side)

        def at_frame(frame):
            u = frame / FRAMES
            across, regrip = eased(u), eased((u - 0.2) / 0.6)
            hand = [s + (e - s) * across for s, e in zip(start, end)]
            hand[0] -= fore * math.sin(math.pi * u)
            hand[1] += lift * math.sin(math.pi * u)
            return placed(hand, lean * (1 - 2 * across), -95) | {
                RIGHT: {"offsetX": (tool.left[0] - tool.right[0]) * regrip, "offsetY": 2 * math.sin(math.pi * regrip), "offsetZ": 0},
                LEFT: {"offsetX": (tool.right[0] - tool.left[0]) * regrip, "offsetY": 0, "offsetZ": 0}}

        return at_frame

    def side_strokes(side, name, grips):
        forward = stroke(side)
        return {f"{name}-ready": (lambda frame: placed(*ready(side), -95), grips),
                f"{name}-forward": (forward, grips),
                f"{name}-backward": (lambda frame: forward(FRAMES - frame), grips),
                f"to-{name}": (switch(side), PORT if side < 0 else STARBOARD)}

    return side_strokes(-1, "starboard", STARBOARD) | side_strokes(1, "port", PORT)


def poling(tool, at, ahead, behind, above, out, side=-1):
    x, y, _ = at

    def placed(hand, lean, pitch):
        return tool.held_at(hand, {"rotationX": -side * lean, "rotationZ": pitch})

    def stroke(frame):
        reach, lift = phase(frame, drive_share("pole"))
        hand = (x - ahead + (ahead + behind) * reach, y + above - 2 * reach + 6 * lift, out)
        return placed(hand, 12, -100 + 30 * reach)

    return {"ready": (lambda frame: placed((x, y + above + 2, out), 10, -90), STARBOARD), "forward": (stroke, STARBOARD),
            "backward": (lambda frame: stroke(FRAMES - frame), STARBOARD)}


def sculling(tool, at, ahead, above, sweep):
    x, y, z = at

    def scull(frame):
        turn_ = 2 * math.pi * frame / FRAMES
        hand = (x - ahead + 0.8 * math.sin(2 * turn_), y + above, z + sweep * math.sin(turn_))
        return tool.held_at(hand, {"rotationX": -25 * math.sin(turn_), "rotationZ": -135})

    return {"ready": (lambda frame: tool.held_at((x - ahead + 2, y + above + 0.5, z - 3), {"rotationX": 70, "rotationZ": -100}), STARBOARD),
            "forward": (scull, STARBOARD), "backward": (lambda frame: scull(FRAMES - frame), STARBOARD)}
