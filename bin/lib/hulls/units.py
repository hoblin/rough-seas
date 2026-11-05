import json
from pathlib import Path

VOXELS_A_METRE = json.loads((Path(__file__).resolve().parents[3] / "RoughSeas/Units/voxel.json").read_text())["voxelsAMetre"]
LITRES_A_CUBIC_METRE = 1000


def metres(voxels, power=1):
    return voxels / VOXELS_A_METRE ** power


def voxels(metres, power=1):
    return metres * VOXELS_A_METRE ** power


def litres(cubic_metres):
    return cubic_metres * LITRES_A_CUBIC_METRE


FORMS = {1: ("Metres", "Voxels"), 2: ("SquareMetres", "SquareVoxels"), 3: ("Litres", "CubicVoxels")}


def both(name, value, power=1):
    """A measured quantity as a generated file holds it: in metres (a volume in litres) and in voxels, the unit in each
    name."""
    si, model = FORMS[power]
    cubic_metres = value / LITRES_A_CUBIC_METRE if power == 3 else value
    return {name + si: round(value, 1 if power == 3 else 4), name + model: round(voxels(cubic_metres, power), 3)}
