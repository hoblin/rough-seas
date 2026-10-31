using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

public class ShipyardModSystem : ModSystem
{
    private static readonly string[] LyingRotations = ["we", "ns"];
    private static readonly string[] LogPaths = ["log-placed-{0}-{1}", "logsection-placed-{0}-ne-{1}", "logsection-placed-{0}-se-{1}", "logsection-placed-{0}-sw-{1}", "logsection-placed-{0}-nw-{1}"];

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockBehaviorClass(BlockBehaviorBlankLog.Code, typeof(BlockBehaviorBlankLog));
        api.RegisterEntityBehaviorClass(EntityBehaviorBurnout.Code, typeof(EntityBehaviorBurnout));
        api.RegisterCollectibleBehaviorClass(CollectibleBehaviorHew.Code, typeof(CollectibleBehaviorHew));
        CharcoalPitOnLogs.Attach();
    }

    public override void Dispose() => CharcoalPitOnLogs.Detach();

    // The server sends its blocks, behaviours included, to clients only after this phase.
    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        foreach (var type in api.World.EntityTypes.Where(BurnsOut))
        {
            if (Wood.Of(type.Variant) is not { } wood) continue;

            var vessel = $"{type.Code.Domain}:{type.Code.Path[..^(wood.Code.Length + 1)]}";
            foreach (var log in LyingLogs(api.World, wood))
            {
                var behavior = new BlockBehaviorBlankLog(log);
                behavior.Initialize(new JsonObject(new JObject { ["vessel"] = vessel }));
                log.BlockBehaviors = log.BlockBehaviors.Append(behavior);
                log.CollectibleBehaviors = log.CollectibleBehaviors.Append(behavior);
            }
        }
        foreach (var axe in api.World.Items.Where(item => item?.Code != null && item.Tool == EnumTool.Axe))
        {
            axe.CollectibleBehaviors = axe.CollectibleBehaviors.Append(new CollectibleBehaviorHew(axe));
        }
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Mod.Logger.Debug("[shipyard] " + string.Format(CultureInfo.InvariantCulture,
            $"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args));

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[shipyard] " + string.Format(CultureInfo.InvariantCulture, format, args));

    private static IEnumerable<Block> LyingLogs(IWorldAccessor world, Wood wood) =>
        LyingRotations.SelectMany(rotation => LogPaths.Select(path => new AssetLocation(wood.Domain, string.Format(path, wood.Species, rotation))))
            .Select(world.GetBlock)
            .OfType<Block>()
            .Where(log => log.Id > 0);

    private static bool BurnsOut(EntityProperties type) =>
        type.Server?.BehaviorsAsJsonObj?.Any(behavior => behavior["code"].AsString() == EntityBehaviorBurnout.Code) == true;
}
