import json
import math

from physics import SWIM_LINE_METRES
from stroke import CENTRE, ROOT, Rower, fit, moved, posed, seat, speed, turn
from transform import angles, apply, euler, mul, nearest, translation, undirect

ASSETS = ROOT / "RoughSeas/assets/roughseas"
ANCHOR = "ItemAnchor"
HAND_POINT_ROTATION = (0, -180, 0)
FRAMES = 30
TOOL = "Tool"
RIGHT = "ToolRight"
LEFT = "ToolLeft"
STROKES = json.loads((ASSETS / "config/propulsion.json").read_text())["strokes"]


def inverse(m):
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    t = [-sum(r[i][k] * m[k][3] for k in range(3)) for i in range(3)]
    return [r[0] + [t[0]], r[1] + [t[1]], r[2] + [t[2]], [0, 0, 0, 1]]


def marker(name, point):
    return {"name": name, "from": list(point), "to": list(point), "faces": {}}


class Tool:

    def __init__(self, item, shape, right, left):
        transform = json.loads((ASSETS / "itemtypes" / f"{item}.json").read_text())["tpHandTransform"]
        if any(transform.get("origin", {}).get(axis, 0) for axis in "xyz") or transform.get("scale", 1) != 1:
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
    ready, forward, backward = poses
    keys = list(range(0, FRAMES, 2))
    plan = [{"code": f"roughseas-{vessel}-{kind}-{name}", "frames": FRAMES, "keys": k, "speed": speed(stroke, FRAMES),
             "pose": pose} for name, pose, k in (("ready", ready, [0]), ("forward", forward, keys), ("backward", backward, keys))]
    _, player = fit(rower, [group], plan, {"R": RIGHT, "L": LEFT}, lean)
    offset = [(s - c) / 16 for s, c in zip(rower.seat, CENTRE)]
    reference = None
    for step, animation in zip(plan, player):
        deepest = math.inf
        previous = reference
        for keyframe, frame in zip(animation["keyframes"], step["keys"]):
            matrices = posed([group], lambda name, pose=step["pose"](frame): pose.get(name, {}))
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
    return player


PADDLE = Tool("tool/paddle", "paddle", (13, 8, 8), (0.5, 8, 8))
POLE = Tool("resource/pole", "pole-3", (12.8, 0.75, 8), (8, 0.75, 8))


def waterline(vessel):
    entity = json.loads((ASSETS / "entities/vessel" / f"{vessel}.json").read_text())
    return entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]


def tools(shape, vessel, seated, idle, lean, line, strokes):
    return [animation for kind, (stroke, tool, poses) in strokes.items()
            for animation in held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, line)]


def phase(frame, drive):
    u = frame / FRAMES
    driving = u < drive
    f = u / drive if driving else (u - drive) / (1 - drive)
    ease = 0.5 - 0.5 * math.cos(math.pi * f)
    return (ease if driving else 1 - ease), (0 if driving else math.sin(math.pi * f))


def paddling(tool, at, ahead, behind, above, out, side=-1):
    x, y, _ = at

    def placed(hand, lean, pitch):
        return tool.held_at(hand, {"rotationX": -side * lean, "rotationZ": pitch})

    def stroke(frame):
        reach, lift = phase(frame, STROKES["paddling"]["driveShare"])
        hand = (x - ahead + (ahead + behind) * reach, y + above + 2 * (1 - reach) ** 2 + 4.5 * lift, out + side * lift)
        return placed(hand, 30 + 20 * lift, -95 + 40 * reach)

    return (lambda frame: placed((x + (behind - ahead) / 2, y + above + 2, out - side), 62, -95), stroke,
            lambda frame: stroke(FRAMES - frame))


def poling(tool, at, ahead, behind, above, out, side=-1):
    x, y, _ = at

    def placed(hand, lean, pitch):
        return tool.held_at(hand, {"rotationX": -side * lean, "rotationZ": pitch})

    def stroke(frame):
        reach, lift = phase(frame, STROKES["pole"]["driveShare"])
        hand = (x - ahead + (ahead + behind) * reach, y + above - 2 * reach + 6 * lift, out)
        return placed(hand, 12, -100 + 30 * reach)

    return (lambda frame: placed((x, y + above + 2, out), 10, -90), stroke, lambda frame: stroke(FRAMES - frame))


def sculling(tool, at, ahead, above, sweep):
    x, y, z = at

    def scull(frame):
        turn_ = 2 * math.pi * frame / FRAMES
        hand = (x - ahead + 0.8 * math.sin(2 * turn_), y + above, z + sweep * math.sin(turn_))
        return tool.held_at(hand, {"rotationX": -25 * math.sin(turn_), "rotationZ": -135})

    return (lambda frame: tool.held_at((x - ahead + 2, y + above + 0.5, z - 3), {"rotationX": 70, "rotationZ": -100}),
            scull, lambda frame: scull(FRAMES - frame))
