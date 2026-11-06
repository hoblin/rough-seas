using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RoughSeas.Propulsion;

public class PropulsionModSystem : ModSystem
{
    private const double KilogramsPerCubicMetre = 1000;
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/propulsion.json");

    public PropulsionConfig Config { get; private set; } = null!;

    public double WaterDensity => VesselMass.SeawaterKgPerLitre * KilogramsPerCubicMetre;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass(EntityBehaviorPropulsion.Code, typeof(EntityBehaviorPropulsion));
        Rowing.Attach();
    }

    public override void StartClientSide(ICoreClientAPI api) => DrawnPole.Attach(this);

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<PropulsionConfig>(ConfigAsset);

    public override void Dispose()
    {
        Rowing.Detach();
        DrawnPole.Detach();
    }
}
