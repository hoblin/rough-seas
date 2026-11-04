import math
from functools import cached_property

from limb import Limb
from transform import angles, apply, cross, depth, direct, dot, euler, nearest, sub, unapply, undirect, unit

GRIP = (1.25, 0.6, 1)
REST_OVERLAP = 0.75 / 16
SHOULDER_SHARE = 0.25
# Joint ranges in degrees, AAOS normal values. SWING is the shoulder's highest elevation in each plane of
# elevation (0 out to the side, 90 straight ahead, -90 straight back): extension 60, horizontal abduction 45,
# horizontal adduction 130.
ELEVATION = (0, 180)
SWING = ((-90, 60), (-45, 180), (130, 180), (270, 60))
# Shoulder rotation is (external, internal); wrist deviation (ulnar, radial), radial tipping the held axis's
# thumb end toward the elbow.
RANGES = {"shoulder rotation": (-90, 70), "elbow flexion": (0, 150), "wrist deviation": (-30, 20)}
# Internal rotation by the arm's elevation: AAOS's 70 is measured with the arm raised to 90°; with the arm at the side,
# elbow bent 90°, it is 80 to 90°, where the trunk stops the forearm (the body check here).
INTERNAL = ((0, 90), (90, 70))
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
    e2 = unit(sub(second, tuple(dot(second, e1) * c for c in e1)))
    return [e1, e2, cross(e1, e2)]


def elevation_limit(plane):
    if plane < SWING[0][0]:
        plane += 360
    for (a, low), (b, high) in zip(SWING, SWING[1:]):
        if a <= plane <= b:
            return low + (high - low) * (plane - a) / (b - a)
    return ELEVATION[1]


class Arm(Limb):

    def __init__(self, chain, idle, outboard):
        super().__init__(chain, idle, outboard)
        self.shoulder = tuple(c / 16 for c in self.upper["rotationOrigin"])
        self.elbow = tuple(c / 16 for c in self.lower["rotationOrigin"])
        self.humerus = tuple(e + f / 16 - s for e, f, s in zip(self.elbow, self.upper["from"], self.shoulder))
        self.forearm = tuple(g / 16 + f / 16 - e for g, f, e in zip(GRIP, self.lower["from"], self.elbow))
        self.rest = [self.upper.get("rotation" + axis, 0) for axis in "XYZ"]
        self.bend = [self.lower.get("rotation" + axis, 0) for axis in "XYZ"]
        self.body_sizes = [size_of(element) for element in self.body]
        self.upper_size, self.lower_size = size_of(self.upper), size_of(self.lower)
        self.below_shoulder = samples(self.upper, SHOULDER_SHARE)
        self.upper_box, self.lower_box = samples(self.upper, 0), samples(self.lower, 0)

    def joints(self, frames):
        *body, upper, lower = frames
        return apply(body[-1], self.shoulder), apply(upper, self.elbow), apply(lower, [c / 16 for c in GRIP])

    def posture(self, pose, trunk=None):
        return Posture(self, pose, trunk)

    def stroke(self, poses, hands, holds):
        swivels = range(0, 360, SWIVEL_STEP)
        trunks = [self.trunk(pose) for pose in poses]
        places = [self.placing(trunk[-1], hand) for trunk, hand in zip(trunks, hands)]
        postures = [[self.posture(pose | place(math.radians(s)), trunk) for s in swivels]
                    for pose, trunk, place in zip(poses, trunks, places)]
        fits = []
        for grips in holds:
            strains = [[self.strain(posture, hand, grip) for posture in row] for row, hand, grip in zip(postures, hands, grips)]
            cost, path = cheapest(strains, SWING_PER_KEY // SWIVEL_STEP)
            fits.append(([self.refine(pose, hand, grip, trunk, place, math.radians(swivels[s]))
                          for pose, hand, grip, trunk, place, s in zip(poses, hands, grips, trunks, places, path)], cost))
        return fits

    def refine(self, pose, hand, grip, trunk, place, swivel):
        def strain_at(at):
            return self.strain(self.posture(pose | place(at), trunk), hand, grip)

        step = math.radians(SWIVEL_STEP / 2)
        while step > math.radians(0.1):
            swivel = min((swivel - step, swivel, swivel + step), key=strain_at)
            step /= 2
        return place(swivel)

    def placing(self, torso, hand):
        target = sub(unapply(torso, hand), self.shoulder)
        upper, fore = math.sqrt(dot(self.humerus, self.humerus)), math.sqrt(dot(self.forearm, self.forearm))
        reach = max(abs(upper - fore) + 1e-6, min(upper + fore - 1e-6, math.sqrt(dot(target, target))))
        bend = self.flexion(reach)
        forearm = direct(euler([*self.bend[:2], self.bend[2] + bend]), self.forearm)
        axis = unit(target)
        down = undirect(torso, (0, -1, 0))
        first = unit(sub(down, tuple(dot(down, axis) * c for c in axis)))
        second = cross(axis, first)
        out = (reach * reach + upper * upper - fore * fore) / (2 * reach)
        radius = math.sqrt(max(0.0, upper * upper - out * out))
        rest = basis(self.humerus, forearm)
        bent = round(bend, 2)

        def place(swivel):
            elbow = tuple(out * a + radius * (math.cos(swivel) * f + math.sin(swivel) * s)
                          for a, f, s in zip(axis, first, second))
            posed = basis(elbow, sub(tuple(reach * a for a in axis), elbow))
            turned = [[sum(posed[k][i] * rest[k][j] for k in range(3)) for j in range(3)] + [0] for i in range(3)]
            total = angles(turned + [[0, 0, 0, 1]])
            return {self.upper["name"]: {"rotation" + a: round(t - r, 2) for a, t, r in zip("XYZ", total, self.rest)},
                    self.lower["name"]: {"rotationZ": bent}}

        return place

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

    def strain(self, posture, hand, grip):
        angles = self.angles(posture, grip)
        limits = sum((excess / half) ** 2 for excess, half in self.faults(posture, hand, angles).values())
        spans = self.spans(angles)
        comfort = sum(off_middle(angles[name], spans[name]) ** 2 for name in COMFORT)
        return LIMIT * limits + comfort + posture.effort

    def effort(self, joints):
        shoulder, elbow, hand = joints
        weights = [(along(shoulder, elbow, UPPER[1]), UPPER[0]), (along(elbow, hand, FORE[1]), FORE[0]), (hand, HAND)]

        def torque(joint, loads):
            return math.hypot(*(sum(BODY * GRAVITY * share * (p[i] - joint[i]) for p, share in loads) for i in (0, 2)))

        return ((torque(shoulder, weights) / SHOULDER_STRENGTH) ** 2
                + (torque(elbow, weights[1:]) / ELBOW_STRENGTH) ** 2)

    def angles(self, posture, grip):
        shoulder, elbow, _ = posture.joints
        *_, torso, upper, lower = posture.frames
        humerus = unit(undirect(torso, sub(elbow, shoulder)))
        held = undirect(lower, grip)
        return {
            "shoulder elevation": math.degrees(math.acos(max(-1.0, min(1.0, -humerus[1])))),
            "shoulder plane": math.degrees(math.atan2(-humerus[0], humerus[2] * self.outboard)),
            "shoulder rotation": self.rotation(torso, upper, humerus),
            "elbow flexion": -posture.pose[self.lower["name"]]["rotationZ"],
            "wrist deviation": math.degrees(math.asin(max(-1.0, min(1.0, held[1])))),
        }

    def spans(self, angles):
        (low, at_side), (high, raised) = INTERNAL
        share = min(1.0, max(0.0, (angles["shoulder elevation"] - low) / (high - low)))
        return RANGES | {"shoulder rotation": (RANGES["shoulder rotation"][0], at_side + (raised - at_side) * share),
                         "shoulder elevation": (ELEVATION[0], elevation_limit(angles["shoulder plane"]))}

    def faults(self, posture, hand, angles):
        faults = {name: (beyond(angles[name], span), (span[1] - span[0]) / 2) for name, span in self.spans(angles).items()}
        faults["arm inside the body"] = (posture.inside, 1)
        faults["hand off the grip"] = (16 * math.dist(hand, posture.joints[-1]), 1)
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

    # The seraph's shoulder turns inside its torso, and its resting arm already lies REST_OVERLAP into the torso's side:
    # the depth past that, of the arm's own boxes below its shoulder.
    def inside(self, frames):
        *body, upper, lower = frames
        points = [apply(upper, p) for p in self.below_shoulder] + [apply(lower, p) for p in self.lower_box]
        sunk = 0.0
        for m, size in zip(body, self.body_sizes):
            for point in points:
                sunk += max(0.0, depth(m, size, point) - REST_OVERLAP) ** 2
        return 16 * math.sqrt(sunk)

    def through(self, pose, other):
        *_, upper, lower = self.frames(pose)
        *_, other_upper, other_lower = other.frames(pose)
        points = [apply(upper, p) for p in self.upper_box] + [apply(lower, p) for p in self.lower_box]
        boxes = ((other_upper, other.upper_size), (other_lower, other.lower_size))
        return 16 * max(0.0, *(depth(m, size, point) for m, size in boxes for point in points))


class Posture:

    def __init__(self, arm, pose, trunk=None):
        self.arm, self.pose = arm, pose
        self.frames = arm.frames(pose, trunk)

    @cached_property
    def joints(self):
        return self.arm.joints(self.frames)

    @cached_property
    def inside(self):
        return self.arm.inside(self.frames)

    @cached_property
    def effort(self):
        return self.arm.effort(self.joints)


def cheapest(strains, swing):
    count = len(strains[0])
    best = None
    for first in range(count):
        cost, back = [math.inf] * count, []
        cost[first] = strains[0][first]
        for row in strains[1:]:
            ring = list(zip(cost, range(count)))
            ring = ring[-swing:] + ring + ring[:swing]
            steps = [min(ring[s:s + 2 * swing + 1]) for s in range(count)]
            back.append([came for _, came in steps])
            cost = [total + strain for (total, _), strain in zip(steps, row)]
        closing = min(range(count), key=lambda s: cost[s] if min(abs(s - first), count - abs(s - first)) <= swing
                      else math.inf)
        if best is None or cost[closing] < best[0]:
            path = [closing]
            for came in reversed(back):
                path.append(came[path[-1]])
            best = (cost[closing], path[::-1])
    return best


def size_of(element):
    return [(b - a) / 16 for a, b in zip(element["from"], element["to"])]


def samples(element, top):
    f, t = element["from"], element["to"]
    shares = (0, 0.5, 1)
    return [((f[0] + (t[0] - f[0]) * i) / 16 - f[0] / 16,
             (t[1] - (t[1] - f[1]) * (top + (1 - top) * j)) / 16 - f[1] / 16,
             (f[2] + (t[2] - f[2]) * k) / 16 - f[2] / 16)
            for i in shares for j in shares for k in shares]
