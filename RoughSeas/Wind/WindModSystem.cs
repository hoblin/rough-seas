using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Wind;

public class WindModSystem : ModSystem
{
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/wind.json");

    public WindConfig Config { get; private set; } = null!;

    public override void Start(ICoreAPI api) => api.RegisterEntityBehaviorClass(EntityBehaviorWindage.Code, typeof(EntityBehaviorWindage));

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<WindConfig>(ConfigAsset);

    public void Log(Entity entity, string format, params object[] args) =>
        Mod.Logger.Debug("[wind] " + string.Format(CultureInfo.InvariantCulture,
            $"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args));
}
