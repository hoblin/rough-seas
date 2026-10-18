using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class MaterialTable
{
    private const string WoodPrefix = "wood-";
    private const string UnknownWood = "wood";

    private readonly Dictionary<string, Material> materials;
    private readonly ILogger logger;
    private readonly HashSet<string> warned = [];

    public MaterialTable(Dictionary<string, Material> materials, ILogger logger)
    {
        this.materials = materials;
        this.logger = logger;
    }

    public double StrengthFactor(IEnumerable<VesselPart> parts) =>
        parts.Aggregate(1.0, (factor, part) => factor * StrengthRatio(part.Material, part.Reference));

    public double BreakingKilonewtons(string rope)
    {
        if (materials.TryGetValue(rope, out var known)) return known.Strength;

        logger.Warning("Rough Seas has no breaking load for {0}; it counts as unbreakable", rope);
        return double.PositiveInfinity;
    }

    private double StrengthRatio(string material, string reference) =>
        Strength(material) is { } built && Strength(reference) is { } baseline ? built / baseline : 1;

    private double? Strength(string material)
    {
        if (materials.TryGetValue(material, out var exact)) return exact.Strength;

        if (!material.StartsWith(WoodPrefix))
        {
            WarnOnce(material, "Rough Seas has no strength for {0}; vessels of it count as their reference build");
            return null;
        }

        var wood = Wood.Parse(material[WoodPrefix.Length..]);
        foreach (var key in wood.Keys)
        {
            if (materials.TryGetValue(WoodPrefix + key, out var species)) return species.Strength;
        }

        WarnOnce(material, "Rough Seas has no strength for {0}; it takes that of an unknown wood");
        return materials[UnknownWood].Strength;
    }

    private void WarnOnce(string material, string message)
    {
        if (warned.Add(material)) logger.Warning(message, material);
    }
}
