import math
from dataclasses import dataclass

import units
import wind

DOWN = (0, -1, 0)


def add(a, b):
    return tuple(p + q for p, q in zip(a, b))


def sub(a, b):
    return tuple(p - q for p, q in zip(a, b))


def scale(k, a):
    return tuple(k * p for p in a)


def dot(a, b):
    return sum(p * q for p, q in zip(a, b))


def norm(a):
    return math.sqrt(dot(a, a))


def unit(a):
    return scale(1 / norm(a), a)


def across_of(a, axis):
    """The part of a square to the axis, made a unit."""
    return unit(sub(a, scale(dot(a, axis), axis)))


def mirrored(point):
    x, y, z = point
    return (2 * wind.ORIGIN[0] - x, y, z)


@dataclass(frozen=True)
class Rig:
    """A crab claw as the shunting canoes set it, in voxels of her model: the yard's heel in a step at either end, the
    yard hung by a halyard from the head of a mast stepped amidships and raked toward whichever end has the tack, the
    boom lashed to the yard's heel, the sheet bent on the boom and led to a beam, running stays from the masthead to
    both ends, a shroud and a shore out to the float. The tack at `pin` is the end she is built with; the other end is
    the same pin mirrored across her middle."""
    pin: tuple
    heel: tuple
    mast: float
    rake_degrees: float
    yard: float
    boom: float
    opening_degrees: float
    bend: float
    halyard: float
    bridle: float
    leads: tuple
    shroud_foot: tuple
    shore_up: float
    shore_foot: tuple

    def tack(self, end):
        return self.pin if end > 0 else mirrored(self.pin)

    def lead(self, end):
        return self.leads[0 if end > 0 else 1]

    def stern_pin(self, end):
        return self.tack(-end)

    def masthead(self, end):
        toward = math.copysign(1, self.tack(end)[0] - self.heel[0])
        rake = math.radians(self.rake_degrees)
        return add(self.heel, (toward * self.mast * math.sin(rake), self.mast * math.cos(rake), 0))

    def hoisted(self, end):
        """The rig with the tack at `end` and the sail hanging as low as its halyard lets it: its plane the upright one
        through the tack and the masthead, the line it turns about."""
        tack, head = self.tack(end), self.masthead(end)
        axis = unit(sub(head, tack))
        reach = norm(sub(head, tack))
        bent, hung = self.bend * self.yard, self.halyard * self.mast
        along = (bent ** 2 - hung ** 2 + reach ** 2) / (2 * reach)
        radius = math.sqrt(bent ** 2 - along ** 2)
        held = add(add(tack, scale(along, axis)), scale(radius, across_of(DOWN, axis)))
        yard = unit(sub(held, tack))
        return self.spars(end, tack, head, held, yard, across_of(DOWN, yard))

    def lowered(self, end):
        """The rig with its spars let down and laid fore and aft on her lee side, the sail furled on them (Hornell 1936,
        fig 231)."""
        tack, head, lead = self.tack(end), self.masthead(end), self.lead(end)
        yard = unit(sub((tack[0] + math.copysign(self.yard, self.heel[0] - tack[0]), lead[1], lead[2]), tack))
        held = add(tack, scale(self.bend * self.yard, yard))
        return self.spars(end, tack, head, held, yard, across_of((0, 0, -1), yard), opening=0)

    def spars(self, end, tack, head, held, yard, down, opening=None):
        opening = math.radians(self.opening_degrees if opening is None else opening)
        boom = add(scale(math.cos(opening), yard), scale(math.sin(opening), down))
        lead = self.lead(end)
        shore_top = add(self.heel, scale(self.shore_up, sub(head, self.heel)))
        return {
            "tack": tack, "masthead": head, "heel": self.heel, "halyardBend": held,
            "peak": add(tack, scale(self.yard, yard)), "clew": add(tack, scale(self.boom, boom)),
            "bridle": add(tack, scale(self.bridle * self.boom, boom)), "lead": lead,
            "sternPin": self.stern_pin(end), "shroudFoot": self.shroud_foot,
            "shoreTop": shore_top, "shoreFoot": self.shore_foot,
        }

    def config(self):
        def points(pose):
            return {k: v for name, at in pose.items() for k, v in wind.corner(name, at).items()}
        return {"ends": [{"hoisted": points(self.hoisted(end)), "lowered": points(self.lowered(end))} for end in (1, -1)],
                **units.both("yard", units.metres(self.yard)), **units.both("boom", units.metres(self.boom))}

    def cloth(self, end):
        pose = self.hoisted(end)
        return (pose["tack"], pose["tack"], pose["peak"], pose["clew"])
