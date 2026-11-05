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
