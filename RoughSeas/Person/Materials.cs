using System;
using System.Linq;

namespace RoughSeas.Person;

/// <summary>
/// What carried things are made of, by density. A part measured in one material keeps its volume made of another, so a
/// copper head of a steel head's size is heavier and a flint one lighter.
/// </summary>
public sealed class Materials
{
    private readonly CarriedConfig config;

    public Materials(CarriedConfig config)
    {
        this.config = config;
        var named = config.Tools.Values.SelectMany(tool => new[] { tool.Head.Of, tool.Haft?.Of })
            .Concat(config.Alike.Values).Append(config.UnknownArmourMaterial).Append(config.HandlePrefix);
        var unknown = named.Where(material => material != null && !config.Densities.ContainsKey(material)).Distinct().ToArray();
        if (unknown.Length > 0) throw new ArgumentException($"config/person.json names materials with no density: {string.Join(", ", unknown)}");
    }

    public double? DensityOf(string? code)
    {
        if (code == null) return null;
        var material = config.Alike.TryGetValue(code, out var alike) ? alike : code;
        return config.Densities.TryGetValue(material, out var density) ? density : null;
    }

    public Carried Part(PartConfig measured, string? madeOf)
    {
        var litres = measured.Kg / config.Densities[measured.Of] * Body.LitresPerCubicMetre;
        var density = DensityOf(madeOf) ?? config.Densities[measured.Of];
        return new Carried(litres * density / Body.LitresPerCubicMetre, litres);
    }

    public Carried Worn(double kg, string? madeOf) =>
        new(kg, kg / (DensityOf(madeOf) ?? config.Densities[config.UnknownArmourMaterial]) * Body.LitresPerCubicMetre);

    public (string? Head, string? Handle) Split(string? material) =>
        material != null && material.Length > config.HandlePrefix.Length && material.StartsWith(config.HandlePrefix)
            ? (material[config.HandlePrefix.Length..], config.HandlePrefix)
            : (material, null);
}
