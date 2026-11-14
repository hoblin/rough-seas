using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace RoughSeas.Wind;

public class WindModSystem : ModSystem
{
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/wind.json");


    public WindConfig Config { get; private set; } = null!;

    private const string Channel = "roughseas-wind";

    private IClientNetworkChannel? reports;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass(EntityBehaviorWindage.Code, typeof(EntityBehaviorWindage));
        api.Network.RegisterChannel(Channel).RegisterMessageType<RigShape>();
        SailedByWind.Attach();
        RoughSeas.Lines.VanillaRopes.Attach();
    }

    public override void StartClientSide(ICoreClientAPI capi) => reports = capi.Network.GetChannel(Channel);

    public override void StartServerSide(ICoreServerAPI sapi) =>
        sapi.Network.GetChannel(Channel).SetMessageHandler<RigShape>((player, shape) =>
            sapi.World.GetEntityById(shape.EntityId)?.GetBehavior<EntityBehaviorWindage>()?.Reported(player, shape));

    public void Report(RigShape shape) => reports?.SendPacket(shape);

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<WindConfig>(ConfigAsset);

    public override void Dispose()
    {
        SailedByWind.Detach();
        RoughSeas.Lines.VanillaRopes.Detach();
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Mod.Logger.Debug("[wind] " + string.Format(CultureInfo.InvariantCulture,
            $"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args));
}
