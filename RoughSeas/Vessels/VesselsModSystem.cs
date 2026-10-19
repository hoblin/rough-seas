using System.Collections.Generic;
using System.Globalization;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
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
    private static readonly AssetLocation ImpactAsset = new("roughseas:config/vessels/impact.json");
    private const string Channel = "roughseas:vessels";

    public MaterialTable Materials { get; private set; } = null!;
    public DamageConversion DamageConversion { get; private set; } = null!;
    public Rapids Rapids { get; private set; } = null!;
    public ImpactMass ImpactMass { get; private set; } = null!;
    public TickBudget Budget { get; } = new("vessels");

    private ICoreAPI api = null!;
    private IClientNetworkChannel? reports;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        api.RegisterEntityBehaviorClass(EntityBehaviorVesselIntegrity.Code, typeof(EntityBehaviorVesselIntegrity));
        api.RegisterEntity(EntityVessel.ClassName, typeof(EntityVessel));
        api.RegisterItemClass(ItemVessel.ClassName, typeof(ItemVessel));
        api.RegisterItemClass(ItemBark.ClassName, typeof(ItemBark));
        api.RegisterCollectibleBehaviorClass(CollectibleBehaviorHullMaterial.Code, typeof(CollectibleBehaviorHullMaterial));
        HullReach.Attach();
        api.Network.RegisterChannel(Channel).RegisterMessageType<ImpactReport>();
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        HullLabel.Attach();
        reports = capi.Network.GetChannel(Channel);
    }

    public void Report(Entity vessel, double speedLost) =>
        reports?.SendPacket(new ImpactReport { EntityId = vessel.EntityId, SpeedLost = speedLost });

    public override void Dispose()
    {
        HullReach.Detach();
        if (api?.Side == EnumAppSide.Client) HullLabel.Detach();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.OnPlayerInteractEntity += OnPlayerInteractEntity;
        api.Network.GetChannel(Channel).SetMessageHandler<ImpactReport>((player, report) =>
            api.World.GetEntityById(report.EntityId)?.GetBehavior<EntityBehaviorVesselIntegrity>()?.Reported(player, report.SpeedLost));
        Budget.CountTicks(api);
        api.ChatCommands.Create("vessels")
            .WithDescription("Rough Seas: vessel hulls")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("budget")
                .WithDescription("Server time the vessel hulls spent since the last report")
                .HandleWith(_ => TextCommandResult.Success(Budget.Report()))
            .EndSubCommand();
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        Materials = new MaterialTable(api.Assets.Get<Dictionary<string, Material>>(MaterialsAsset), Mod.Logger);
        DamageConversion = api.Assets.Get<DamageConversion>(DamageAsset);
        Rapids = api.Assets.Get<Rapids>(RapidsAsset);
        ImpactMass = api.Assets.Get<ImpactMass>(ImpactAsset);
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Mod.Logger.Debug("[vessels] " + string.Format(CultureInfo.InvariantCulture,
            $"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args));

    private static void OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
    {
        if (mode == (int)EnumInteractMode.Attack) return;

        if (entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.TryPickUp(byPlayer.Entity) == true)
        {
            handling = EnumHandling.PreventDefault;
        }
    }
}
