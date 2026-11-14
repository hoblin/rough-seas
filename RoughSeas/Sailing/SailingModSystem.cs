using System.Globalization;
using RoughSeas.Lines;
using RoughSeas.Vessels;
using RoughSeas.Wind;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace RoughSeas.Sailing;

public class SailingModSystem : ModSystem
{
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/sailing.json");
    private const string Channel = "roughseas-sail";

    private IClientNetworkChannel? reports;

    public SailingConfig Config { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass(EntityBehaviorSail.Code, typeof(EntityBehaviorSail));
        api.Network.RegisterChannel(Channel).RegisterMessageType<RigShape>();
        SailedByWind.Attach();
        VanillaRopes.Attach();
    }

    public override void StartClientSide(ICoreClientAPI capi) => reports = capi.Network.GetChannel(Channel);

    public override void StartServerSide(ICoreServerAPI sapi) =>
        sapi.Network.GetChannel(Channel).SetMessageHandler<RigShape>((player, shape) =>
            sapi.World.GetEntityById(shape.EntityId)?.GetBehavior<EntityBehaviorSail>()?.Reported(player, shape));

    public void Report(RigShape shape) => reports?.SendPacket(shape);

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<SailingConfig>(ConfigAsset);

    public override void Dispose()
    {
        SailedByWind.Detach();
        VanillaRopes.Detach();
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Mod.Logger.Debug("[sail] " + string.Format(CultureInfo.InvariantCulture,
            $"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args));
}
