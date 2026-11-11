"""Laths piled on the ground in vanilla's ground storage, one element a lath in the order they are laid: a pile of N
laths draws the shape's first N elements (BlockEntityGroundStorage tessellates CuboidsPerModel times the count)."""
import json
from pathlib import Path

import units
from shape import box, write

LAYERS = 10
TEXTURES = {"wood": "game:block/wood/debarked/oak"}
ASSETS = Path(__file__).resolve().parents[3] / "RoughSeas/assets/roughseas"
SHAPES = ASSETS / "shapes"
RECIPES = ASSETS / "recipes/grid/lathbundles.json"
STONE_TEXTURE = "game:block/stone/rock/granite1"
WATER = 1000
GRANITE = 2700
LATH_SECTION_SQUARE_METRES = 0.01 * 0.06
LATH_WET_DENSITY = 400 * 4 / 3
PILES = SHAPES / "block/pile"
BUNDLES = SHAPES / "entity"
ICONS = SHAPES / "item"
ICON_STONES = 9
BUNDLE_LATHS = 32
BUNDLE_ACROSS = 4
CENTRE = 8


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


def bundle(metres, width, thickness):
    """A bundle of a full stack of laths of ``metres``, under the stones its recipe weights it with."""
    path = BUNDLES / f"lathbundle-{metres}.json"
    count = bundle_stones(metres)
    write(path, bundled(units.voxels(metres), count, width, thickness))
    print(f"{path}: {BUNDLE_LATHS} laths under {count} stones of {units.metres(stone_voxels()) * 100:.1f} cm")


def bundle_icon(width, thickness):
    """The bundle's icon: a bundle a block long, so its laths and stones show at the size of a slot."""
    path = ICONS / "lathbundle-icon.json"
    write(path, bundled(units.voxels(1), ICON_STONES, width, thickness))
    print(f"{path}: the icon")


def bundled(length, count, width, thickness):
    """BUNDLE_LATHS laths of ``length`` voxels, BUNDLE_ACROSS side by side in layers, lying lengthwise on the middle,
    with ``count`` stones spread in rows along their top, as many abreast as its breadth holds."""
    x0 = CENTRE - length / 2
    z0 = CENTRE - BUNDLE_ACROSS * width / 2
    layers = BUNDLE_LATHS // BUNDLE_ACROSS
    laths = [box(f"Lath{layer * BUNDLE_ACROSS + k + 1}", (x0, layer * thickness, z0 + k * width), (x0 + length, (layer + 1) * thickness, z0 + (k + 1) * width), "wood")
             for layer in range(layers) for k in range(BUNDLE_ACROSS)]
    side = stone_voxels()
    breadth = BUNDLE_ACROSS * width
    across = int(breadth // side)
    pitch = length / -(-count // across)
    top = layers * thickness
    stones = [box(f"Stone{n + 1}", (x, top, z), (x + side, top + side, z + side), "stone")
              for n in range(count)
              for x, z in [(x0 + (n // across + 0.5) * pitch - side / 2, z0 + (n % across + 0.5) * breadth / across - side / 2)]]
    return {"textureWidth": 16, "textureHeight": 16, "textures": {**TEXTURES, "stone": STONE_TEXTURE}, "elements": laths + stones}


def stone_voxels():
    """The side of a stone as heavy as the recipe makes it: a full stack holds down 32 wet 5 m laths, so one stone
    weighs under water what those laths lift, shared by the stack, and is a cube of granite of that weight."""
    lift = (WATER - LATH_WET_DENSITY) * BUNDLE_LATHS * 5 * LATH_SECTION_SQUARE_METRES
    cubic_metres = lift / bundle_stones(5) / (GRANITE - WATER)
    return units.voxels(cubic_metres ** (1 / 3))


def bundle_stones(metres):
    """The stones the recipe weights a bundle of ``metres`` laths with."""
    recipes = json.loads(RECIPES.read_text())
    return next(recipe["ingredients"]["S"]["quantity"] for recipe in recipes if recipe["output"]["code"] == f"roughseas:lathbundle-{{wood}}-{metres}-dry")


def shifted(point, rise):
    return [point[0], point[1] + rise, point[2]]


def save(metres, elements):
    path = PILES / f"lathpile-{metres}.json"
    write(path, {"textureWidth": 16, "textureHeight": 16, "textures": TEXTURES, "elements": elements})
    print(f"{path}: {len(elements)} laths")
