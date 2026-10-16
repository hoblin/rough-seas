using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class VesselsModSystem : ModSystem
{
    private static readonly AssetLocation MaterialsAsset = new("roughseas:config/vessels/materials.json");

    public MaterialTable Materials { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass(EntityBehaviorVesselIntegrity.Code, typeof(EntityBehaviorVesselIntegrity));
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        Materials = new MaterialTable(api.Assets.Get<Dictionary<string, Material>>(MaterialsAsset), Mod.Logger);
    }
}
