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

    public double WeakestRatio(IEnumerable<VesselPart> parts) =>
        parts.Select(part => StrengthRatio(part.Material, part.Reference)).DefaultIfEmpty(1).Min();

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
