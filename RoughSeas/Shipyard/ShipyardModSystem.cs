using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

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
        api.RegisterEntityBehaviorClass(EntityBehaviorFraming.Code, typeof(EntityBehaviorFraming));
        api.RegisterEntityBehaviorClass(EntityBehaviorRigging.Code, typeof(EntityBehaviorRigging));
        api.RegisterEntityBehaviorClass(EntityBehaviorRiggable.Code, typeof(EntityBehaviorRiggable));
        api.RegisterCollectibleBehaviorClass(CollectibleBehaviorFraming.Code, typeof(CollectibleBehaviorFraming));
        api.RegisterCollectibleBehaviorClass(CollectibleBehaviorCarriedBuild.Code, typeof(CollectibleBehaviorCarriedBuild));
        api.RegisterBlockBehaviorClass(BlockBehaviorStrippable.Code, typeof(BlockBehaviorStrippable));
        api.RegisterBlockClass(BlockWedge.ClassName, typeof(BlockWedge));
        api.RegisterBlockClass(BlockBundles.ClassName, typeof(BlockBundles));
        FireOnLogs.Attach();
        LashingRope.Attach();
    }

    public override void Dispose()
    {
        FireOnLogs.Detach();
        LashingRope.Detach();
    }

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
        var builds = new List<FramingConfig>();
        foreach (var type in api.World.EntityTypes)
        {
            var framing = FramingConfig.Of(type);
            if (framing != null)
            {
                if (framing.BarkSeconds > 0 && Wood.Of(type.Variant) is { } wood) Strippable(api.World, type, wood, framing);
                if (api.World.GetItem(type.Code) is { } item) item.CollectibleBehaviors = item.CollectibleBehaviors.Append(new CollectibleBehaviorCarriedBuild(item));
            }
            if ((framing ?? RiggingConfig.Of(type)) is { } built) builds.Add(built);
        }
        Framed(api.World, builds);
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

    private void Strippable(IWorldAccessor world, EntityProperties type, Wood wood, FramingConfig framing)
    {
        var grown = world.GetBlock(new AssetLocation(wood.Domain, $"log-grown-{wood.Species}-ud"));
        var debarked = world.GetBlock(new AssetLocation(Mod.Info.ModID, $"debarkedtrunk-{wood.Code}"));
        if (grown == null || debarked == null)
        {
            Log("no standing trunk of {0} to strip for {1}: its bark needs a debarkedtrunk variant", wood.Code, type.Code);
            return;
        }

        DebarkedTrunk.Dress(world, debarked, grown, wood);
        var behavior = new BlockBehaviorStrippable(grown);
        behavior.Initialize(new JsonObject(new JObject
        {
            ["vessel"] = type.Code.ToString(),
            ["debarked"] = debarked.Code.ToString(),
            ["seconds"] = framing.BarkSeconds,
            ["trunkBlocks"] = framing.TrunkBlocks
        }));
        grown.BlockBehaviors = grown.BlockBehaviors.Append(behavior);
        grown.CollectibleBehaviors = grown.CollectibleBehaviors.Append(behavior);
    }

    /// <summary>Lets rope, cloth, axes and every liquid container lay a vessel's pieces, as her laths and poles do by their own JSON.</summary>
    private static void Framed(IWorldAccessor world, List<FramingConfig> builds)
    {
        var codes = builds.SelectMany(built => new[] { built.Rope, built.Cloth, built.Planks }).Where(code => code != "").Distinct().Select(code => new AssetLocation(code)).ToArray();
        bool hews = builds.Any(built => built.Stages.Any(stage => stage.Work == FramingWork.Hew));
        var tools = world.Collectibles.Where(collectible => collectible?.Code != null
            && (collectible is BlockLiquidContainerBase || hews && collectible.Tool == EnumTool.Axe || codes.Any(code => WildcardUtil.Match(code, collectible.Code))));
        foreach (var tool in tools.Where(tool => !tool.HasBehavior<CollectibleBehaviorFraming>()))
        {
            tool.CollectibleBehaviors = tool.CollectibleBehaviors.Append(new CollectibleBehaviorFraming(tool));
        }
    }

    private static bool BurnsOut(EntityProperties type) =>
        type.Server?.BehaviorsAsJsonObj?.Any(behavior => behavior["code"].AsString() == EntityBehaviorBurnout.Code) == true;
}
