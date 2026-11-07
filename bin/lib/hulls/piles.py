"""Laths piled on the ground in vanilla's ground storage, one element a lath in the order they are laid: a pile of N
laths draws the shape's first N elements (BlockEntityGroundStorage tessellates CuboidsPerModel times the count)."""
from pathlib import Path

import units
from shape import box, write

LAYERS = 10
TEXTURES = {"wood": "game:block/wood/debarked/oak"}
PILES = Path(__file__).resolve().parents[3] / "RoughSeas/assets/roughseas/shapes/block/pile"


def lattice(laths, thickness):
    """The lathpile of 2 m laths: each layer the coracle's flat lattice as her build lays it, ``laths`` the corners of
    its boxes in laying order."""
    layer = 2 * thickness
    pile = [box(f"Lath{k * len(laths) + n}", shifted(frm, k * layer), shifted(to, k * layer), "wood")
            for k in range(LAYERS) for n, (frm, to) in enumerate(laths, 1)]
    save(2, pile)
    return len(laths), units.metres(layer)


def long(metres, width, thickness):
    """A lathpile of laths longer than a block, lengthwise across it from the block its footprint starts in, laid from
    its two sides in toward its middle."""
    across = int(units.voxels(1))
    order = [k for pair in zip(range(across // 2), reversed(range(across // 2, across))) for k in pair]
    x0 = -((metres - 1) // 2) * across
    x1 = x0 + metres * across
    pile = [box(f"Lath{layer * across + n}", (x0, layer * thickness, k + (1 - width) / 2), (x1, (layer + 1) * thickness, k + (1 + width) / 2), "wood")
            for layer in range(LAYERS) for n, k in enumerate(order, 1)]
    save(metres, pile)
    return across, units.metres(thickness)


def shifted(point, rise):
    return [point[0], point[1] + rise, point[2]]


def save(metres, elements):
    path = PILES / f"lathpile-{metres}.json"
    write(path, {"textureWidth": 16, "textureHeight": 16, "textures": TEXTURES, "elements": elements})
    print(f"{path}: {len(elements)} laths")
