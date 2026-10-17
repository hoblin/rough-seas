import json
import math
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SERAPH = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/game/shapes/entity/humanoid/seraph-faceless.json"
POSES = ROOT / "RoughSeas/assets/roughseas/patches/player-poses.json"
PATCHES = ROOT / "RoughSeas/assets/roughseas/patches"

ARMS = {"R": ("UpperArmR", "LowerArmR"), "L": ("UpperArmL", "LowerArmL")}
# Outweighs the seat pose (80) so far that the fit can leave it out of the arms and torso.
WEIGHT = 10000
SPEED = 0.75
ELBOW = 0.3
GRIP = (1.25, 0.6, 1)
CENTRE = (8, 0, 8)


def identity():
    return [[float(i == j) for j in range(4)] for i in range(4)]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def translation(v):
    m = identity()
    for i in range(3):
        m[i][3] = v[i]
    return m


def rotation(axis, degrees):
    c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    i, j = [(1, 2), (2, 0), (0, 1)]["XYZ".index(axis)]
    m = identity()
    m[i][i], m[i][j], m[j][i], m[j][j] = c, -s, s, c
    return m


def unturn(rotations, v):
    m = identity()
    for axis in "XYZ":
        m = mul(m, rotation(axis, rotations.get("rotation" + axis, 0)))
    return tuple(sum(m[k][i] * c for k, c in enumerate(v)) for i in range(3))


def turn(rotations, v):
    m = identity()
    for axis in "XYZ":
        m = mul(m, rotation(axis, rotations.get("rotation" + axis, 0)))
    return apply(m, v)


def gripped(rest, throat, hand, turned):
    origin, resting = rest
    reach = turn(turned, throat)
    return {"Paddle": moved(origin, resting, [h - r for h, r in zip(hand, reach)], turned)}


def paddling(vessel, rest, throat, catch, finish, height, out, side=-1, frames=30):
    def placed(hand, lean, pitch):
        return gripped(rest, throat, hand, {"rotationX": -side * lean, "rotationZ": pitch})

    def stroke(frame):
        u = frame / frames
        drive = u < 0.55
        f = u / 0.55 if drive else (u - 0.55) / 0.45
        ease = 0.5 - 0.5 * math.cos(math.pi * f)
        reach = ease if drive else 1 - ease
        lift = 0 if drive else math.sin(math.pi * f)
        hand = (catch + (finish - catch) * reach, height + 1.5 * lift, out + side * lift)
        return placed(hand, 18 + 30 * lift, -100 + 38 * reach)

    return strokes(vessel, frames, lambda frame: placed(((catch + finish) / 2, height + 2, out - side), 62, -95),
                   stroke, lambda frame: stroke(frames - frame))


def strokes(vessel, frames, ready, forward, backward):
    keys = list(range(0, frames, 2))
    return [{"code": f"roughseas-{vessel}-{name}", "frames": frames, "keys": keys if name != "ready" else [0],
             "pose": pose} for name, pose in (("ready", ready), ("forward", forward), ("backward", backward))]


def moved(origin, rest, target, turned):
    shift = unturn(turned, [t - o for t, o in zip(target, origin)])
    pose = {"offset" + axis: value for axis, value in zip("XYZ", shift)}
    return pose | {"rotation" + axis: turned.get("rotation" + axis, 0) - r for axis, r in zip("XYZ", rest)}


def apply(m, p):
    return tuple(sum(m[i][k] * c for k, c in enumerate((*p, 1))) for i in range(3))


# The engine's element transform for animations of version 0, as bin/lib/vsmodel/pose.py has it for Blender.
def local(element, pose):
    origin = [c / 16 for c in element.get("rotationOrigin", (0, 0, 0))]
    shift = [(f + pose.get("offset" + a, 0)) / 16 - o for f, a, o in zip(element["from"], "XYZ", origin)]
    m = translation(origin)
    for axis in "XYZ":
        m = mul(m, rotation(axis, element.get("rotation" + axis, 0) + pose.get("rotation" + axis, 0)))
    return mul(m, translation(shift))


def posed(elements, pose_of):
    matrices = {}

    def descend(children, parent):
        for element in children:
            matrices[element["name"]] = mul(parent, local(element, pose_of(element["name"])))
            descend(element.get("children", []), matrices[element["name"]])

    descend(elements, identity())
    return matrices


def find(elements, name):
    for element in elements:
        if element["name"] == name:
            return element
        found = find(element.get("children", []), name)
        if found:
            return found
    return None


def centre(elements, matrices, name):
    element = find(elements, name)
    size = [t - f for f, t in zip(element["from"], element["to"])]
    return tuple(16 * c for c in apply(matrices[name], [s / 32 for s in size]))


def seat(shape, name):
    element = find(shape.elements, name)
    point = element["attachmentpoints"][0]
    return tuple(f + float(point["pos" + axis]) for f, axis in zip(element["from"], "XYZ"))


def minimise(cost, start, step=8.0, tolerance=0.05):
    best, value = list(start), cost(start)
    while step > tolerance:
        improved = False
        for i in range(len(best)):
            for delta in (step, -step):
                trial = list(best)
                trial[i] += delta
                trial_value = cost(trial)
                if trial_value < value:
                    best, value, improved = trial, trial_value, True
        if not improved:
            step /= 2
    return best, value


class Rower:

    def __init__(self, seat, idle):
        self.seraph = json.loads(SERAPH.read_text())
        self.seat = seat
        animations = self.seraph["animations"] + [op["value"] for op in json.loads(POSES.read_text())
                                                  if op["path"] == "/animations/-"]
        sitting = next(a for a in animations if a["code"] == idle)
        self.idle = next(k["elements"] for k in sitting["keyframes"] if k["frame"] == 0)
        self.chains = {side: self.chain(lower) for side, (_, lower) in ARMS.items()}

    def chain(self, name):
        def descend(elements, path):
            for element in elements:
                found = [*path, element] if element["name"] == name else descend(element.get("children", []),
                                                                                 [*path, element])
                if found:
                    return found
            return None

        return descend(self.seraph["elements"], [])

    def joints(self, pose, side):
        m = identity()
        for element in self.chains[side]:
            m = mul(m, local(element, pose.get(element["name"]) or self.idle.get(element["name"]) or {}))
        forearm = self.chains[side][-1]
        elbow = [(o - f) / 16 for o, f in zip(forearm["rotationOrigin"], forearm["from"])]
        return [tuple(s + 16 * c - h for s, c, h in zip(self.seat, apply(m, p), CENTRE))
                for p in (elbow, [c / 16 for c in GRIP])]

    def hand(self, pose, side):
        return self.joints(pose, side)[1]

    def reach(self, torso, targets, start):
        pose = {"UpperTorso": torso}
        error = 0
        for side, target in targets.items():
            upper, lower = ARMS[side]
            names = [(upper, "rotationX"), (upper, "rotationY"), (upper, "rotationZ"), (lower, "rotationX"),
                     (lower, "rotationZ")]
            initial = [start.get(element, {}).get(channel, 0) for element, channel in names]

            def arms(values):
                solved = {upper: {}, lower: {}}
                for (element, channel), value in zip(names, values):
                    solved[element][channel] = value
                return solved

            def cost(values):
                elbow, hand = self.joints(pose | arms(values), side)
                miss = math.dist(hand, target)
                return miss * miss + ELBOW * elbow[1] + 1e-4 * sum((v - s) ** 2 for v, s in zip(values, initial))

            solved, _ = minimise(cost, initial)
            pose |= arms([round(value, 2) for value in solved])
            error = max(error, math.dist(self.hand(pose, side), target))
        return pose, error


def crew(shape, vessel, seated, idle, strokes, grips, lean):
    rower = Rower(seat(shape, seated), idle)
    shape.animations, player = fit(rower, shape.elements, strokes, grips, lean)
    shape.write()
    player_patch(f"player-{vessel}", player)
    rowed(shape, rower, shape.animations, player)


def player_patch(name, animations):
    ops = []
    for animation in animations:
        ops.append({"op": "add", "file": "game:shapes/entity/humanoid/seraph-faceless.json", "path": "/animations/-",
                    "value": animation})
    for animation in animations:
        elements = sorted({e for k in animation["keyframes"] for e in k["elements"]})
        ops.append({"op": "add", "file": "game:entities/humanoid/player.json", "path": "/client/animations/-",
                    "side": "Server", "value": {
                        "code": animation["code"], "animation": animation["code"], "animationSpeed": SPEED,
                        "blendMode": "Average",
                        "easeInSpeed": 4, "easeOutSpeed": 4,
                        "elementWeight": {e: WEIGHT for e in elements},
                        "elementBlendMode": {e: "Average" for e in elements}}})
    path = PATCHES / f"{name}.json"
    path.write_text(json.dumps(ops, indent=4) + "\n")
    print(f"{path}: {len(animations)} animations")


def rowed(shape, rower, vessel, player):
    offset = [s - h for s, h in zip(rower.seat, CENTRE)]
    seated = {"name": "Rower", "from": offset, "to": offset, "faces": {}, "children": rower.seraph["elements"]}
    animations = []
    for own, arms in zip(vessel, player):
        keyframes = [{"frame": a["frame"], "elements": every_axis(rower.idle) | a["elements"] | b["elements"]}
                     for a, b in zip(own["keyframes"], arms["keyframes"])]
        animations.append(own | {"keyframes": keyframes})
    path = Path(os.environ.get("BENCH_TMP", "/tmp")) / "model" / f"{shape.path.stem}-rowed.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"textureWidth": 16, "textureHeight": 16,
                                "textures": shape.textures | rower.seraph["textures"],
                                "elements": [e for e in shape.elements if e["name"] not in ("hideWater", "Bilge")]
                                + [seated], "animations": animations}))
    print(f"{path}: the vessel rowed, for bin/model pose")


def every_axis(pose):
    keyed = {}
    for name, channels in pose.items():
        keyed[name] = {}
        for prefix in ("offset", "rotation"):
            if any(key.startswith(prefix) for key in channels):
                keyed[name] |= {prefix + axis: round(channels.get(prefix + axis, 0), 2) for axis in "XYZ"}
    return keyed


def fit(rower, elements, strokes, grips, lean):
    vessel, player = [], []
    for stroke in strokes:
        start = {element: {"rotationZ": angle} for arm in ARMS.values() for element, angle in zip(arm, (-60, -30))}
        own, arms = [], []
        worst = 0
        for frame in stroke["keys"]:
            pose = stroke["pose"](frame)
            matrices = posed(elements, lambda name: pose.get(name, {}))
            targets = {side: centre(elements, matrices, grip) for side, grip in grips.items()}
            body, error = rower.reach(lean, targets, start)
            start = body
            worst = max(worst, error)
            own.append({"frame": frame, "elements": every_axis(pose)})
            arms.append({"frame": frame, "elements": every_axis(body)})
        print(f"{stroke['code']}: hands within {worst:.2f} of the grips")
        common = {"code": stroke["code"], "quantityframes": stroke["frames"], "onActivityStopped": "EaseOut",
                  "onAnimationEnd": "Repeat"}
        vessel.append({"name": stroke["code"], **common, "keyframes": own})
        player.append({"name": stroke["code"], **common, "keyframes": arms})
    return vessel, player
