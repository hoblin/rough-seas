using System.Globalization;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Propulsion;

public abstract class Tool
{
    public const string AttributeKey = "roughseas:propulsion";
    private const string WoodPrefix = "wood-";

    public abstract string Kind { get; }

    public static Tool? Of(ItemStack? stack, PropulsionConfig config, System.Func<string, double> densityOf)
    {
        var collectible = stack?.Collectible;
        if (collectible?.Attributes?[AttributeKey] is not { Exists: true } attributes) return null;

        return attributes["tool"].AsString() switch
        {
            Pole.Code => new Pole(MetresOf(collectible, attributes), Wood.Of(collectible.Variant) is { } wood ? densityOf(WoodPrefix + wood.Code) : collectible.MaterialDensity, config.Pole),
            Paddle.Code => new Paddle(attributes["bladeSquareMetres"].AsDouble()),
            _ => null
        };
    }

    public abstract Propulsor? Drive(RowerStation station, out string? refusal);

    private static double MetresOf(CollectibleObject collectible, JsonObject attributes) =>
        double.TryParse(collectible.Variant["length"], CultureInfo.InvariantCulture, out var metres) ? metres : attributes["metres"].AsDouble();
}

public sealed class Pole(double metres, double density, PoleConfig config) : Tool
{
    public const string Code = "pole";

    public override string Kind => Code;

    public double Metres => metres;

    public double Kilograms => metres * config.DressedMetres * config.DressedMetres * density;

    public double Reach(double handsAboveWaterMetres) => config.Reach(metres, handsAboveWaterMetres);

    public override Propulsor? Drive(RowerStation station, out string? refusal) => station.Poled(this, out refusal);
}

public sealed class Paddle(double bladeSquareMetres) : Tool
{
    public const string Code = "paddle";

    public override string Kind => Code;

    public override Propulsor? Drive(RowerStation station, out string? refusal) => station.Paddled(bladeSquareMetres, out refusal);
}
