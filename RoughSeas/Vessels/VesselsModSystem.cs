using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RoughSeas.Vessels;

public class VesselsModSystem : ModSystem
{
    private static readonly AssetLocation MaterialsAsset = new("roughseas:config/vessels/materials.json");
    private static readonly AssetLocation DamageAsset = new("roughseas:config/vessels/damage.json");
    private static readonly AssetLocation RapidsAsset = new("roughseas:config/vessels/rapids.json");

    public MaterialTable Materials { get; private set; } = null!;
    public DamageConversion DamageConversion { get; private set; } = null!;
    public Rapids Rapids { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass(EntityBehaviorVesselIntegrity.Code, typeof(EntityBehaviorVesselIntegrity));
        api.RegisterEntity(EntityVessel.ClassName, typeof(EntityVessel));
        api.RegisterItemClass(ItemVessel.ClassName, typeof(ItemVessel));
        HullReach.Attach();
    }

    public override void Dispose() => HullReach.Detach();

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.OnPlayerInteractEntity += OnPlayerInteractEntity;
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        Materials = new MaterialTable(api.Assets.Get<Dictionary<string, Material>>(MaterialsAsset), Mod.Logger);
        DamageConversion = api.Assets.Get<DamageConversion>(DamageAsset);
        Rapids = api.Assets.Get<Rapids>(RapidsAsset);
    }

    private static void OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
    {
        if (mode == (int)EnumInteractMode.Attack) return;

        if (entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.TryPickUp(byPlayer.Entity) == true)
        {
            handling = EnumHandling.PreventDefault;
        }
    }
}
