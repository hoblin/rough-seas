using HarmonyLib;
using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Compat.Shipwright;

public class ShipwrightCompat : OtherModCompat
{
    public const string Condition = "roughseas-shipwright";
    private const string BoatClass = "shipwright.EntityShipwrightBoat";

    protected override string ModId => "shipwright";

    protected override string ModName => "Shipwright";

    protected override string LogTag => "shipwright";

    public override double ExecuteOrder() => 0.04;

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        api.World.Config?.SetBool(Condition, Tested);
    }

    public override void AssetsFinalize(ICoreAPI api) => api.World.Config?.RemoveAttribute(Condition);

    protected override void Meet(ICoreAPI api, string version)
    {
        Boats.Add(AccessTools.TypeByName(BoatClass));
        Log("Shipwright {0}: its boats are vessels of ours, and take our sea", version);
    }
}
