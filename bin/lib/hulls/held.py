import json
import math

from physics import STANDING_HIP_METRES, SWIM_LINE_METRES
from stroke import CENTRE, ROOT, Rower, centre, drive_share, facing, fit, moved, named, posed, rowed, seat, turn, unturn
from transform import angles, apply, euler, mul, nearest, translation, unapply, undirect

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
CHANGE = "to-"
LOOPED = {"sculling": 2}
STANDING = "roughseas-stand"
VOXELS_A_METRE = 16
PLANT_DEPTH_METRES = 1
HAND_SPAN = 5
STRIDE = 3
FOOT_HALF = 1.5
DROP = 2
HIP_TURN = 45
HANDS_INBOARD = 1
PLANT = (12, 40)
SEATED_CHEST = 7
SEATED_REACH = (-3, 3)
REST = 8
CLEARANCE = 0.05


def unit(v):
    length = math.sqrt(sum(c * c for c in v))
    return [c / length for c in v]


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

    def held_at(self, hand, turned, at=None):
        reach = turn(turned, at or self.right)
        return {TOOL: moved((0, 0, 0), (0, 0, 0), [h - r for h, r in zip(hand, reach)], turned)}

    def gripped(self, hand, toward, grips):
        """The tool laid from the hand toward a point, each grip at its distance along the tool's x, the hand on the
        nearest."""
        d = unit([t - h for t, h in zip(toward, hand)])
        turned = {"rotationX": math.degrees(math.atan2(-d[2], -d[1])), "rotationZ": -math.degrees(math.acos(d[0]))}
        marks = {RIGHT: self.right, LEFT: self.left}
        upper = min(grips, key=grips.get)
        return self.held_at(hand, turned, (grips[upper], *marks[upper][1:])) | {
            name: {"offsetX": grips[name] - mark[0]} for name, mark in marks.items()}


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


def held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, waterline, hull=()):
    stance = poses if isinstance(poses, Stance) else None
    rower = Rower(stance.at, stance.idle) if stance else Rower(seat(shape, seated), idle, facing(shape, vessel, seated))
    if stance:
        shift = [round((a - s) / VOXELS_A_METRE, 3) for a, s in zip(stance.at, seat(shape, seated))]
        hands = round(stance.at[1] / VOXELS_A_METRE - waterline + STANDING_HIP_METRES, 2)
        print(f"{vessel}-{kind}: stands {', '.join(f'{a} {v:+.3f}' for a, v in zip('xyz', shift))} m from {seated}, "
              f"his hands {hands:.2f} m over the water at the end of the shove")
        poling = propulsion_of(vessel)["pole"]
        written = poling.get("stance", {}).get("riderOffset", {})
        if [written.get(a, 0) for a in "xyz"] != shift or poling.get("handsAboveWaterMetres") != hands:
            raise ValueError(f"{vessel}: her pole stance in JSON, riderOffset {written} and handsAboveWaterMetres "
                             f"{poling.get('handsAboveWaterMetres')}, is not the one fitted: write "
                             f"{dict(zip('xyz', shift))} and {hands}")
    group = tool.group()
    poses = stance.poses if stance else poses
    plan = named(f"{vessel}-{kind}", FRAMES, {name: pose for name, (pose, *_) in poses.items()}, stroke,
                 LOOPED.get(stroke, 1))
    for step, (name, (pose, grips, *body)) in zip(plan, poses.items()):
        step |= {"grips": grips, "body": body[0](rower) if body else None}
        if name.startswith(CHANGE):
            step |= {"keys": step["keys"] + [FRAMES - 1], "ends": "Hold"}
    walls = hull_walls(shape, hull) if hull else []
    if hull and not walls:
        raise ValueError(f"{vessel}: no element of her shape is named {' or '.join(hull)}, so nothing checks the tool")
    if walls:
        middle = sum(m[2][3] for _, _, m, _ in walls) / len(walls)
        for step in plan:
            rests = {frame: rested(walls, tool, step["pose"](frame), middle) for frame in step["keys"]}
            step["pose"] = lambda frame, rests=rests: rests[frame][0]
            push, frame = max((d, f) for f, (_, d) in rests.items())
            if round(push, 1):
                print(f"{step['code']}: the hull holds the tool out by {push:.1f} at frame {frame}")
    if stance:
        for step in plan:
            faults = {}
            for frame in step["keys"]:
                body = step["body"](frame)
                for side, foot in stance.feet.items():
                    for name, excess in rower.legs[side].faults(body, rower.body(foot)).items():
                        faults[f"{side} {name}"] = max(faults.get(f"{side} {name}", (0, 0)), (excess, frame))
            for name, (excess, frame) in faults.items():
                if round(excess, 1):
                    print(f"{step['code']}: {name} by {excess:.1f} at frame {frame}")
    own, player = fit(rower, [group], plan, STARBOARD, lean)
    if hasattr(shape, "path"):
        rowed(shape, rower, own, player, f"{vessel}-{kind}", [group])
    through = (0, 0, None, None)
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
            anchor = wrist(rower, tool, body, mul(rower.placing, matrices[TOOL]))
            if previous is not None:
                turned = nearest([anchor["rotation" + a] for a in "XYZ"], previous)
                anchor |= {"rotation" + a: round(v, 2) for a, v in zip("XYZ", turned)}
            previous = [anchor["rotation" + a] for a in "XYZ"]
            reference = reference or previous
            keyframe["elements"][ANCHOR] = anchor
            deepest = min(deepest, lowest(matrices, tool.elements) - waterline)
            if walls:
                depth, part, wall = into(walls, along_tool(matrices, tool.elements))
                through = max(through, (16 * depth, frame, part, wall), key=lambda t: t[0])
        print(f"{animation['code']}: the tool reaches {-deepest:.2f} m under the water" if deepest < 0
              else f"{animation['code']}: the tool stays {deepest:.2f} m over the water")
        if walls and round(through[0], 1):
            print(f"  the {through[2]} through the hull's {through[3]} by {through[0]:.1f} at frame {through[1]}")
        through = (0, 0, None, None)
        if step["code"].endswith("starboard-forward"):
            print(f"{animation['code']}: the blade drives {sum(abeam) / len(abeam):.2f} m abeam of the seat")
    return player


def hull_walls(shape, names):
    matrices = posed(shape.elements, lambda name: {})
    walls = []

    def collect(elements):
        for element in elements:
            if element.get("faces"):
                size = [(t - f) / 16 for f, t in zip(element["from"], element["to"])]
                m = matrices[element["name"]]
                corners = [apply(m, (i * size[0], j * size[1], k * size[2])) for i in (0, 1) for j in (0, 1) for k in (0, 1)]
                walls.append((element["name"], size, m, [[f(c[i] for c in corners) for i in range(3)] for f in (min, max)]))
            collect(element.get("children", []))

    collect([e for e in shape.elements if e["name"].startswith(names)])
    return walls


def along_tool(matrices, elements):
    points = []
    for element in elements:
        f, t = element["from"], element["to"]
        steps = max(1, round(t[0] - f[0]))
        points += [(element["name"], apply(matrices[element["name"]],
                                           [(t[0] - f[0]) * k / steps / 16, (t[1] - f[1]) / 32, (t[2] - f[2]) / 32]))
                   for k in range(steps + 1)]
    return points


def into(walls, points):
    return max(((min(min(v, s - v) for v, s in zip(unapply(m, p), size)), part, wall)
                for wall, size, m, _ in walls for part, p in points), default=(0, None, None))


def near(walls, points, margin):
    low = [min(p[i] for _, p in points) - margin for i in range(3)]
    high = [max(p[i] for _, p in points) + margin for i in range(3)]
    return [wall for wall in walls if all(wall[3][0][i] <= high[i] and wall[3][1][i] >= low[i] for i in range(3))]


def rested(walls, tool, pose, middle):
    """The pose with its tool moved the shortest way out of the walls, outboard of `middle` or up, and how far in
    voxels: a stopgap over guessed stroke lines, #252."""
    points = along_tool(posed([tool.group()], lambda name: pose.get(name, {})), tool.elements)
    touching = near(walls, points, REST / VOXELS_A_METRE)

    def clear(direction, metres):
        moved_points = [(part, [c + metres * u for c, u in zip(p, direction)]) for part, p in points]
        return VOXELS_A_METRE * into(touching, moved_points)[0] < CLEARANCE

    if not touching or clear((0, 0, 0), 0):
        return pose, 0
    side = 1 if sum(p[2] for _, p in points) / len(points) > middle else -1
    pushes = []
    for direction in ((0, 0, side), (0, 1, 0)):
        low, high = 0, REST / VOXELS_A_METRE
        if clear(direction, high):
            for _ in range(10):
                low, high = (low, (low + high) / 2) if clear(direction, (low + high) / 2) else ((low + high) / 2, high)
            pushes.append((high, direction))
    if not pushes:
        return pose, 0
    metres, direction = min(pushes)
    shift = unturn(pose[TOOL], [VOXELS_A_METRE * metres * u for u in direction])
    return pose | {TOOL: pose[TOOL] | {"offset" + a: pose[TOOL].get("offset" + a, 0) + s for a, s in zip("XYZ", shift)}}, \
        VOXELS_A_METRE * metres


PADDLE = Tool("tool/paddle", "paddle", (13, 8, 8), (0.5, 8, 8))
POLE = Tool("resource/pole", "pole-3", (12.8, 0.75, 8), (8, 0.75, 8))


def propulsion_of(vessel):
    path = ASSETS / "entities/vessel" / f"{vessel}.json"
    if path.exists():
        return json.loads(path.read_text())["behaviorConfigs"]["roughseas:propulsion"]
    patches = json.loads((ASSETS / "patches/propulsion-vessels.json").read_text())
    return next(op["value"] for op in patches if op["path"] == "/behaviorConfigs/roughseas:propulsion"
                and op["file"].endswith(f"boat-{vessel}.json"))


def waterline(vessel):
    entity = json.loads((ASSETS / "entities/vessel" / f"{vessel}.json").read_text())
    return entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]


def tools(shape, vessel, seated, idle, lean, line, by_kind, hull=()):
    return [animation for kind, (stroke, tool, poses) in by_kind.items()
            for animation in held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, line, hull)]


def phase(frame, drive):
    u = frame / FRAMES
    driving = u < drive
    f = u / drive if driving else (u - drive) / (1 - drive)
    ease = 0.5 - 0.5 * math.cos(math.pi * f)
    return (ease if driving else 1 - ease), (0 if driving else math.sin(math.pi * f))


def eased(u):
    return 0.5 - 0.5 * math.cos(math.pi * min(1, max(0, u)))


def paddling(tool, at, ahead, behind, above, out, catch=None, fore=3):
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
            u = frame / (FRAMES - 1)
            across, regrip = eased(u), eased((u - 0.2) / 0.6)
            hand = [s + (e - s) * across for s, e in zip(start, end)]
            hand[0] -= fore * math.sin(math.pi * u)
            hand[1] += 6 * math.sin(math.pi * u)
            return placed(hand, lean * (1 - 2 * across), -95) | {
                RIGHT: {"offsetX": (tool.left[0] - tool.right[0]) * regrip, "offsetY": 2 * math.sin(math.pi * regrip), "offsetZ": 0},
                LEFT: {"offsetX": (tool.right[0] - tool.left[0]) * regrip, "offsetY": 0, "offsetZ": 0}}

        return at_frame

    def side_strokes(side, name, grips):
        forward = stroke(side)
        return {f"{name}-ready": (lambda frame: placed(*ready(side), -95), grips),
                f"{name}-forward": (forward, grips),
                f"{name}-backward": (lambda frame: forward(FRAMES - frame), grips),
                f"{CHANGE}{name}": (switch(side), PORT if side < 0 else STARBOARD)}

    return side_strokes(-1, "starboard", STARBOARD) | side_strokes(1, "port", PORT)


class Stance:
    """Where the rower stands to work the tool: his soles at `at`, in the shape's voxels, on `feet`; each of `poses`
    carries his hips and legs."""

    def __init__(self, at, idle, poses, feet):
        self.at, self.idle, self.poses, self.feet = at, idle, poses, feet


def standing_poling(tool, at, line, out, chine, side=-1, floor_at=None):
    x, floor, z = at
    floor_at = floor_at or (lambda foot_x, across: floor)
    bed = line - VOXELS_A_METRE * PLANT_DEPTH_METRES
    reach_x = [x - 2, x + 4]
    reach_y = [floor + 21, floor + 19]
    plant = [x + reach for reach in PLANT]

    def lerp(span, share):
        return span[0] + (span[1] - span[0]) * share

    def placed(frame):
        reach, lift = phase(frame, drive_share("pole"))
        hand = (lerp(reach_x, reach), lerp(reach_y, reach) - DROP * reach, out - side * HANDS_INBOARD)
        foot = (lerp(plant, reach), bed + 20 * lift, out)
        upper = 48 - math.dist(hand, foot)
        return tool.gripped(hand, foot, {LEFT: upper, RIGHT: upper + HAND_SPAN})

    feet = {"R": (x + STRIDE, floor_at(x + STRIDE, chine), z + side * (chine - FOOT_HALF)),
            "L": (x - STRIDE, floor_at(x - STRIDE, 2 * FOOT_HALF), z - side * FOOT_HALF)}

    def body(rower, frame):
        reach, _ = phase(frame, drive_share("pole"))
        hips = {"LowerTorso": {"offsetY": -DROP * reach, "rotationY": side * HIP_TURN}}
        hips["UpperTorso"] = {"rotationY": toward_grips(rower, tool, placed(frame), hips), "rotationZ": 10 + 15 * reach}
        for leg_side, foot in feet.items():
            hips |= rower.legs[leg_side].plant(hips, rower.body(foot))
        return hips

    return Stance(at, STANDING, three(placed, body), feet)


def toward_grips(rower, tool, pose, hips):
    markers = posed([tool.group()], lambda name: pose.get(name, {}))
    grips = [rower.body([VOXELS_A_METRE * c for c in apply(markers[name], (0, 0, 0))]) for name in (RIGHT, LEFT)]
    return rower.facing(hips, [sum(c) / 2 for c in zip(*grips)])


def three(placed, body):
    frames = {"ready": lambda frame: 0, "forward": lambda frame: frame, "backward": lambda frame: FRAMES - frame}
    return {name: (lambda frame, at=at: placed(at(frame)), STARBOARD,
                   lambda rower, at=at: lambda frame: body(rower, at(frame)))
            for name, at in frames.items()}


def seated_poling(tool, at, line, out, side=-1):
    x, seat_y, _ = at
    bed = line - VOXELS_A_METRE * PLANT_DEPTH_METRES

    def lerp(span, share):
        return span[0] + (span[1] - span[0]) * share

    def placed(frame):
        reach, lift = phase(frame, drive_share("seatedpole"))
        hand = (lerp(SEATED_REACH, reach) + x, seat_y + SEATED_CHEST, out - side * HANDS_INBOARD)
        foot = (x + lerp(PLANT, reach), bed + 20 * lift, out)
        upper = 48 - math.dist(hand, foot)
        return tool.gripped(hand, foot, {LEFT: upper, RIGHT: upper + HAND_SPAN})

    def body(rower, frame):
        return {"UpperTorso": {"rotationY": toward_grips(rower, tool, placed(frame), {})}}

    return three(placed, body)


def sculling(tool, at, ahead, above, sweep):
    x, y, z = at

    def scull(frame):
        turn_ = 2 * math.pi * frame / FRAMES
        hand = (x - ahead + 0.8 * math.sin(2 * turn_), y + above, z + sweep * math.sin(turn_))
        return tool.held_at(hand, {"rotationX": -25 * math.sin(turn_), "rotationZ": -135})

    return {"ready": (lambda frame: tool.held_at((x - ahead + 2, y + above + 0.5, z - 3), {"rotationX": 70, "rotationZ": -100}), STARBOARD),
            "forward": (scull, STARBOARD), "backward": (lambda frame: scull(FRAMES - frame), STARBOARD)}
