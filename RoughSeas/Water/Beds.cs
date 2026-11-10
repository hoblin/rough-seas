using System;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Water;

public sealed class Beds(BedConfig[] beds)
{
    private readonly BedConfig hard = beds.FirstOrDefault(bed => bed.Material == "stone") ?? new BedConfig();

    public BedConfig Of(EnumBlockMaterial material) =>
        Array.Find(beds, bed => string.Equals(bed.Material, material.ToString(), StringComparison.OrdinalIgnoreCase)) ?? hard;
}
