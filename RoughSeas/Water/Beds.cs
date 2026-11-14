using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Water;

public sealed class Beds
{
    private readonly BedConfig hard;
    private readonly Dictionary<EnumBlockMaterial, BedConfig> byMaterial;

    public Beds(BedConfig[] beds)
    {
        hard = beds.FirstOrDefault(bed => bed.Material == "stone") ?? new BedConfig();
        byMaterial = Enum.GetValues<EnumBlockMaterial>().ToDictionary(material => material,
            material => Array.Find(beds, bed => string.Equals(bed.Material, material.ToString(), StringComparison.OrdinalIgnoreCase)) ?? hard);
    }

    public BedConfig Of(EnumBlockMaterial material) => byMaterial.GetValueOrDefault(material, hard);
}
