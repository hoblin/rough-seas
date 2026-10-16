using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class MaterialTable
{
    private readonly Dictionary<string, Material> materials;
    private readonly ILogger logger;

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

    private double StrengthRatio(string material, string reference)
    {
        if (materials.TryGetValue(material, out var built) && materials.TryGetValue(reference, out var baseline))
        {
            return built.Strength / baseline.Strength;
        }

        logger.Warning("Rough Seas has no strength for {0} against {1}; the vessel counts as its reference build", material, reference);
        return 1;
    }
}
