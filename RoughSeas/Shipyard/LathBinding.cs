using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>A hide boat bound from laths laid out on the ground: the layout her laths must make, and binding them into her blank.</summary>
public sealed class LathBinding
{
    private const string CacheKey = "roughseas:lathbindings";

    private readonly EntityProperties type;
    private readonly CoveringConfig config;
    private readonly LathLayout layout;

    private LathBinding(EntityProperties type, CoveringConfig config, LathLayout layout)
    {
        (this.type, this.config, this.layout) = (type, config, layout);
        Rope = new RopeLaying(new AssetLocation(config.Rope));
    }

    public RopeLaying Rope { get; }

    public double Seconds => config.Stages[1].Seconds;

    public static IReadOnlyList<LathBinding> All(IWorldAccessor world) =>
        ObjectCacheUtil.GetOrCreate(world.Api, CacheKey, () => world.EntityTypes
            .Select(type => (type, config: CoveringConfig.Of(type)))
            .Where(entry => entry.config is { Stages.Length: >= 2 } && LathLayout.Of(entry.config.Layout) != null)
            .Select(entry =>
            {
                entry.config!.Measure(entry.type.Client?.Shape?.Base is { } shape ? FramingConfig.ShapeAt(world.Api, shape) : null);
                return new LathBinding(entry.type, entry.config, LathLayout.Of(entry.config.Layout)!);
            })
            .ToArray());

    public static (LathBinding Binding, LaidOut Laid)? Find(IWorldAccessor world, IReadOnlyList<LaidLath> laths) =>
        All(world).Select(binding => (binding, laid: binding.layout.Find(laths, binding.config.Stages[0].Metres.DefaultIfEmpty(0).Max())))
            .Where(found => found.laid != null)
            .Select(found => ((LathBinding, LaidOut)?)(found.binding, found.laid!))
            .FirstOrDefault();

    /// <summary>Spawns her blank where <paramref name="laid"/> lies, her first stage laid, setting by <paramref name="setsAt"/>. Server only.</summary>
    public Entity Bind(IWorldAccessor world, LaidOut laid, double y, double setsAt)
    {
        var vessel = world.ClassRegistry.CreateEntity(type);
        vessel.Pos.SetPos(laid.X, y, laid.Z);
        vessel.Pos.Yaw = laid.Along == EnumAxis.X ? GameMath.PIHALF : 0;
        EntityBehaviorCovering.Begin(vessel, config, laid.Wood.Code, setsAt);
        EntityBehaviorVesselIntegrity.Ungreased(vessel);
        world.SpawnEntity(vessel);
        return vessel;
    }
}
