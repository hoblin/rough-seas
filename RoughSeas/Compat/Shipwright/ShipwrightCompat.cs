using System;
using HarmonyLib;
using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Compat.Shipwright;

public class ShipwrightCompat : OtherModCompat
{
    public const string Condition = "roughseas-shipwright";
    private const string BoatClassName = "shipwright.EntityShipwrightBoat";

    private Type? boat;

    protected override string ModId => "shipwright";

    protected override string LogTag => "shipwright";

    /// <summary>Before vanilla's patch loader (0.05), which reads <see cref="Condition"/> for patches/compat-shipwright.json.</summary>
    public override double ExecuteOrder() => 0.04;

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        api.World.Config?.SetBool(Condition, Tested);
    }

    /// <summary>The condition is only the patch loader's: it leaves the world config before the world saves it.</summary>
    public override void AssetsFinalize(ICoreAPI api) => api.World.Config?.RemoveAttribute(Condition);

    protected override void Meet(ICoreAPI api, string name, string version)
    {
        boat = AccessTools.TypeByName(BoatClassName);
        Boats.Add(boat);
        Log("{0} {1}: its boats are vessels of ours, and take our sea", name, version);
    }

    public override void Dispose()
    {
        if (boat != null) Boats.Remove(boat);
    }
}
