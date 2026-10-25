import math

from transform import angles, apply, direct, euler, identity, local, mul, nearest, unapply, undirect

GRIP = (1.25, 0.6, 1)
# Joint ranges in degrees, AAOS normal values. SWING is the shoulder's highest elevation in each plane of
# elevation (0 out to the side, 90 straight ahead, -90 straight back): extension 60, horizontal abduction 45,
# horizontal adduction 130.
ELEVATION = (0, 180)
SWING = ((-90, 60), (-45, 180), (130, 180), (270, 60))
# Shoulder rotation is (external, internal); wrist deviation (ulnar, radial), radial tipping the held axis's
# thumb end toward the elbow.
RANGES = {"shoulder rotation": (-90, 70), "elbow flexion": (0, 150), "wrist deviation": (-30, 20)}
COMFORT = ("shoulder rotation", "wrist deviation")
# A passed limit outweighs any discomfort, so the arm passes one only when no pose keeps them all.
LIMIT = 100
SWIVEL_STEP = 5
# How far the elbow swings round the shoulder-hand line from one key to the next, 0.09 s later: about 340°/s.
SWING_PER_KEY = 30
BODY = 75
GRAVITY = 9.81
# Share of the body's mass and the centre of mass from the proximal joint: de Leva 1996, J Biomech 29(9).
UPPER = (0.0271, 0.577)
FORE = (0.0162, 0.457)
HAND = 0.0061
# Isometric torque in N·m: shoulder flexion of men at 90° (PMC12821543); elbow flexion of 18 young men at its
# 120° peak (experts.mcmaster.ca/scholarly-works/236014).
SHOULDER_STRENGTH = 77.2
ELBOW_STRENGTH = 69.5


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def minus(a, b):
    return tuple(x - y for x, y in zip(a, b))


def unit(v):
    length = math.sqrt(dot(v, v))
    return tuple(c / length for c in v)


def along(a, b, share):
    return tuple(x + share * (y - x) for x, y in zip(a, b))


def beyond(value, span):
    low, high = span
    return max(0.0, low - value) + max(0.0, value - high)


def off_middle(value, span):
    low, high = span
    return (value - (low + high) / 2) / ((high - low) / 2)


def basis(first, second):
    e1 = unit(first)
    e2 = unit(minus(second, tuple(dot(second, e1) * c for c in e1)))
    return [e1, e2, cross(e1, e2)]


def elevation_limit(plane):
    if plane < SWING[0][0]:
        plane += 360
    for (a, low), (b, high) in zip(SWING, SWING[1:]):
        if a <= plane <= b:
            return low + (high - low) * (plane - a) / (b - a)
    return ELEVATION[1]


class Arm:

    def __init__(self, chain, idle, outboard):
        *self.body, self.upper, self.lower = chain
        self.idle = idle
        self.outboard = outboard
        self.shoulder = tuple(c / 16 for c in self.upper["rotationOrigin"])
        self.elbow = tuple(c / 16 for c in self.lower["rotationOrigin"])
        self.humerus = tuple(e + f / 16 - s for e, f, s in zip(self.elbow, self.upper["from"], self.shoulder))
        self.forearm = tuple(g / 16 + f / 16 - e for g, f, e in zip(GRIP, self.lower["from"], self.elbow))
        self.rest = [self.upper.get("rotation" + axis, 0) for axis in "XYZ"]
        self.bend = [self.lower.get("rotation" + axis, 0) for axis in "XYZ"]

    def frames(self, pose):
        m = identity()
        out = []
        for element in [*self.body, self.upper, self.lower]:
            m = mul(m, local(element, pose.get(element["name"]) or self.idle.get(element["name"]) or {}))
            out.append(m)
        return out

    def joints(self, pose):
        *body, upper, lower = self.frames(pose)
        return apply(body[-1], self.shoulder), apply(upper, self.elbow), apply(lower, [c / 16 for c in GRIP])

    def stroke(self, poses, hands, grips):
        swivels = range(0, 360, SWIVEL_STEP)
        swing = SWING_PER_KEY // SWIVEL_STEP
        strains = [[self.strain(pose | self.place(pose, hand, math.radians(s)), hand, grip) for s in swivels]
                   for pose, hand, grip in zip(poses, hands, grips)]
        count = len(swivels)
        best = None
        for first in range(count):
            cost, back = [math.inf] * count, []
            cost[first] = strains[0][first]
            for row in strains[1:]:
                steps = [min(((cost[(s + d) % count], (s + d) % count) for d in range(-swing, swing + 1))) for s in
                         range(count)]
                back.append([came for _, came in steps])
                cost = [total + strain for (total, _), strain in zip(steps, row)]
            closing = min(range(count), key=lambda s: cost[s] if min(abs(s - first), count - abs(s - first)) <= swing
                          else math.inf)
            if best is None or cost[closing] < best[0]:
                path = [closing]
                for came in reversed(back):
                    path.append(came[path[-1]])
                best = (cost[closing], path[::-1])
        held = [self.refine(pose, hand, grip, math.radians(swivels[s]))
                for pose, hand, grip, s in zip(poses, hands, grips, best[1])]
        return held, best[0]

    def refine(self, pose, hand, grip, swivel):
        def strain_at(at):
            return self.strain(pose | self.place(pose, hand, at), hand, grip)

        step = math.radians(SWIVEL_STEP / 2)
        while step > math.radians(0.1):
            swivel = min((swivel - step, swivel, swivel + step), key=strain_at)
            step /= 2
        return self.place(pose, hand, swivel)

    def place(self, pose, hand, swivel):
        torso = self.frames(pose)[len(self.body) - 1]
        target = minus(unapply(torso, hand), self.shoulder)
        upper, fore = math.sqrt(dot(self.humerus, self.humerus)), math.sqrt(dot(self.forearm, self.forearm))
        reach = max(abs(upper - fore) + 1e-6, min(upper + fore - 1e-6, math.sqrt(dot(target, target))))
        bend = self.flexion(reach)
        forearm = direct(euler([*self.bend[:2], self.bend[2] + bend]), self.forearm)
        axis = unit(target)
        down = undirect(torso, (0, -1, 0))
        first = unit(minus(down, tuple(dot(down, axis) * c for c in axis)))
        second = cross(axis, first)
        out = (reach * reach + upper * upper - fore * fore) / (2 * reach)
        radius = math.sqrt(max(0.0, upper * upper - out * out))
        elbow = tuple(out * a + radius * (math.cos(swivel) * f + math.sin(swivel) * s)
                      for a, f, s in zip(axis, first, second))
        to_hand = minus(tuple(reach * a for a in axis), elbow)
        posed, rest = basis(elbow, to_hand), basis(self.humerus, forearm)
        turned = [[sum(posed[k][i] * rest[k][j] for k in range(3)) for j in range(3)] + [0] for i in range(3)]
        total = angles(turned + [[0, 0, 0, 1]])
        return {self.upper["name"]: {"rotation" + a: round(t - r, 2) for a, t, r in zip("XYZ", total, self.rest)},
                self.lower["name"]: {"rotationZ": round(bend, 2)}}

    def follow(self, arm, previous):
        name = self.upper["name"]
        turned = [arm[name]["rotation" + axis] + rest for axis, rest in zip("XYZ", self.rest)]
        before = [previous[name]["rotation" + axis] + rest for axis, rest in zip("XYZ", self.rest)]
        return arm | {name: {"rotation" + axis: round(value - rest, 2)
                             for axis, value, rest in zip("XYZ", nearest(turned, before), self.rest)}}

    def flexion(self, reach):
        tilt = undirect(euler(self.bend[:2]), self.humerus)
        x, y, z = self.forearm
        wanted = (reach * reach - dot(self.humerus, self.humerus) - dot(self.forearm, self.forearm)) / 2
        cosine, sine = tilt[0] * x + tilt[1] * y, tilt[1] * x - tilt[0] * y
        middle = math.atan2(sine, cosine)
        spread = math.acos(max(-1.0, min(1.0, (wanted - tilt[2] * z) / math.hypot(cosine, sine))))
        bends = [(math.degrees(middle + side * spread) - self.bend[2] + 180) % 360 - 180 for side in (1, -1)]
        return min(bends, key=lambda bend: beyond(-bend, RANGES["elbow flexion"]))

    def strain(self, pose, hand, grip):
        limits = sum((excess / half) ** 2 for excess, half in self.faults(pose, hand, grip).values())
        angles = self.angles(pose, grip)
        comfort = sum(off_middle(angles[name], RANGES[name]) ** 2 for name in COMFORT)
        return LIMIT * limits + comfort + self.effort(pose)

    def effort(self, pose):
        shoulder, elbow, hand = self.joints(pose)
        weights = [(along(shoulder, elbow, UPPER[1]), UPPER[0]), (along(elbow, hand, FORE[1]), FORE[0]), (hand, HAND)]

        def torque(joint, loads):
            return math.hypot(*(sum(BODY * GRAVITY * share * (p[i] - joint[i]) for p, share in loads) for i in (0, 2)))

        return ((torque(shoulder, weights) / SHOULDER_STRENGTH) ** 2
                + (torque(elbow, weights[1:]) / ELBOW_STRENGTH) ** 2)

    def angles(self, pose, grip):
        shoulder, elbow, _ = self.joints(pose)
        *_, torso, upper, lower = self.frames(pose)
        humerus = unit(undirect(torso, minus(elbow, shoulder)))
        held = undirect(lower, grip)
        return {
            "shoulder elevation": math.degrees(math.acos(max(-1.0, min(1.0, -humerus[1])))),
            "shoulder plane": math.degrees(math.atan2(-humerus[0], humerus[2] * self.outboard)),
            "shoulder rotation": self.rotation(torso, upper, humerus),
            "elbow flexion": -pose[self.lower["name"]]["rotationZ"],
            "wrist deviation": math.degrees(math.asin(max(-1.0, min(1.0, held[1])))),
        }

    def faults(self, pose, hand, grip):
        angles = self.angles(pose, grip)
        shoulder, elbow, reached = self.joints(pose)
        spans = RANGES | {"shoulder elevation": (ELEVATION[0], elevation_limit(angles["shoulder plane"]))}
        faults = {name: (beyond(angles[name], span), (span[1] - span[0]) / 2) for name, span in spans.items()}
        faults["arm inside the body"] = (self.inside(pose, [along(shoulder, elbow, k) for k in (0.5, 0.75, 1)]
                                                     + [along(elbow, reached, k) for k in (0.25, 0.5, 0.75, 1)]), 1)
        faults["hand off the grip"] = (16 * math.dist(hand, reached), 1)
        return {name: fault for name, fault in faults.items() if fault[0] > 0}

    # Internal rotation is positive; 0 is the upper arm turned as it hangs at rest, its -x face forward.
    def rotation(self, torso, upper, humerus):
        forward = undirect(torso, direct(upper, (-1, 0, 0)))
        hanging = (0, -1, 0)
        axis = cross(hanging, humerus)
        length = math.sqrt(dot(axis, axis))
        if length > 1e-6:
            axis = tuple(c / length for c in axis)
            angle = -math.acos(max(-1.0, min(1.0, dot(hanging, humerus))))
            c, s = math.cos(angle), math.sin(angle)
            twisted = cross(axis, forward)
            forward = tuple(f * c + t * s + a * dot(axis, forward) * (1 - c)
                            for f, t, a in zip(forward, twisted, axis))
        return math.degrees(math.atan2(-forward[2] * self.outboard, -forward[0]))

    # Measured on the arm's centre line: vanilla's resting arm lies with its centre line on the torso's side.
    def inside(self, pose, points):
        depth = 0.0
        for element, m in zip(self.body, self.frames(pose)):
            size = [(b - a) / 16 for a, b in zip(element["from"], element["to"])]
            for point in points:
                q = unapply(m, point)
                depth += max(0.0, min(min(v, s - v) for v, s in zip(q, size))) ** 2
        return 16 * math.sqrt(depth)
