using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// A mechanic of the sea that a world setting of its own switches off, read live from the world's configuration on
/// either side. A world that never set it has it on.
/// </summary>
public sealed class Mechanic
{
    public const string LegacyDangerousOceans = "dangerousOceans";

    public static readonly Mechanic BilgeWater = new("bilgeWater");
    public static readonly Mechanic HullDamage = new("hullDamage");
    public static readonly Mechanic Lightning = new("stormLightning");
    public static readonly Mechanic MooringStrain = new("mooringStrain");
    public static readonly Mechanic Currents = new("oceanCurrents");
    public static readonly Mechanic RowerFatigue = new("rowerFatigue");
    public static readonly Mechanic PoleDepth = new("poleDepth");
    public static readonly Mechanic SwimmerBreakers = new("swimmerBreakers");

    public static readonly Mechanic[] All = [BilgeWater, HullDamage, Lightning, MooringStrain, Currents, RowerFatigue, PoleDepth, SwimmerBreakers];

    private Mechanic(string code) => Code = code;

    public string Code { get; }

    public bool On(IWorldAccessor world) => On(world.Config);

    public bool On(ITreeAttribute config) => config.GetBool(Code, true);

    /// <summary>
    /// Gives every mechanic the value of a world's old dangerous-oceans setting and removes that setting, so it runs
    /// once per world.
    /// </summary>
    /// <returns>The old setting's value, or null when the world carried none.</returns>
    public static bool? Migrate(ITreeAttribute config)
    {
        if (!config.HasAttribute(LegacyDangerousOceans)) return null;

        bool dangerous = config.GetBool(LegacyDangerousOceans, true);
        foreach (var mechanic in All) config.SetBool(mechanic.Code, dangerous);
        config.RemoveAttribute(LegacyDangerousOceans);
        return dangerous;
    }

    public override string ToString() => Code;
}
