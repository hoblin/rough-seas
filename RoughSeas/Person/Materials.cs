namespace RoughSeas.Person;

/// <summary>
/// What carried things are made of, by density. A part measured in one material keeps its volume made of another, so a
/// copper head of a steel head's size is heavier and a flint one lighter.
/// </summary>
public sealed class Materials(CarriedConfig config)
{
    public double? DensityOf(string? code)
    {
        if (code == null) return null;
        var material = config.Alike.TryGetValue(code, out var alike) ? alike : code;
        return config.Densities.TryGetValue(material, out var density) ? density : null;
    }

    public Carried Part(PartConfig measured, string? madeOf)
    {
        var litres = measured.Kg / DensityOf(measured.Of)!.Value * Body.LitresPerCubicMetre;
        var density = DensityOf(madeOf) ?? DensityOf(measured.Of)!.Value;
        return new Carried(litres * density / Body.LitresPerCubicMetre, litres);
    }

    public Carried Worn(double kg, string? madeOf) =>
        new(kg, kg / (DensityOf(madeOf) ?? DensityOf(config.ArmourMaterial)!.Value) * Body.LitresPerCubicMetre);

    public (string? Head, string? Handle) Split(string? material) =>
        material != null && material.Length > config.Handle.Length && material.StartsWith(config.Handle)
            ? (material[config.Handle.Length..], config.Handle)
            : (material, null);
}
