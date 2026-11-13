"""A vessel's holds as `roughseas:cargo` reads them (RoughSeas/Cargo): each hold's box and the places of its piles.
Builders call it by its name, `cargo.hold`, so none of its names lands among theirs."""
import units
from shape import box, num

PILE = units.voxels(1) / 3
ABOVE_RIM = 1


def hold(name, code, places, hull, inset, rim):
    laid = [(x, rest(hull, inset, x, z), z) for x, z in places]
    half = PILE / 2
    frm = (min(x for x, _, _ in laid) - half, min(y for _, y, _ in laid), min(z for _, _, z in laid) - half)
    to = (max(x for x, _, _ in laid) + half, rim + ABOVE_RIM, max(z for _, _, z in laid) + half)
    element = box(name, frm, to, {})
    element["attachmentpoints"] = [{"code": code, "posX": str(num((to[0] - frm[0]) / 2)), "posY": "0", "posZ": str(num((to[2] - frm[2]) / 2)),
                                    "rotationX": "0", "rotationY": "0", "rotationZ": "0"}]
    return element, (code, {"metres": [[num(units.metres(c)) for c in at] for at in laid], "voxels": [[num(c) for c in at] for at in laid]})


def block(start, along, across, centre):
    return [(start + PILE * (i + 0.5), centre + PILE * (j - (across - 1) / 2)) for i in range(along) for j in range(across)]


def rest(hull, inset, x, z):
    half = PILE / 2
    inner, outer = max(0, abs(z - hull.cz) - half), abs(z - hull.cz) + half
    return max(max(hull.floor(along, inner, inset), hull.floor(along, outer)) for along in (x - half, x, x + half))


def config(*made):
    return {"behaviorConfigs": {"roughseas:cargo": {"pileScale": num(PILE / units.voxels(1)), "places": dict(places for _, places in made)}}}
