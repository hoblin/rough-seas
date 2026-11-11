using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>A hide boat bound from a pile of laths on the ground: the laths her build starts from, and binding them into her blank where they lie.</summary>
public sealed class LathBinding
{
    private const string CacheKey = "roughseas:lathbindings";

    private readonly EntityProperties type;
    private readonly CoveringConfig config;
    private readonly LathStart start;

    private LathBinding(EntityProperties type, CoveringConfig config, LathStart start)
    {
        (this.type, this.config, this.start) = (type, config, start);
        Rope = new RopeLaying(new AssetLocation(config.Rope));
    }

    public RopeLaying Rope { get; }

    public double Seconds => config.Stages[1].Seconds;

    public static IReadOnlyList<LathBinding> All(IWorldAccessor world) =>
        ObjectCacheUtil.GetOrCreate(world.Api, CacheKey, () => world.EntityTypes
            .Select(type => (type, config: CoveringConfig.Of(type)))
            .Where(entry => entry.config is { Stages.Length: >= 2, Start: not null })
            .Select(entry =>
            {
                entry.config!.Measure(entry.type.Client?.Shape?.Base is { } shape ? FramingConfig.ShapeAt(world.Api, shape) : null);
                return new LathBinding(entry.type, entry.config, entry.config.Start!);
            })
            .ToArray());

    /// <summary>The vessel a pile of exactly <paramref name="laths"/> starts, all of one wood, whatever their state.</summary>
    public static LathBinding? For(IWorldAccessor world, ItemStack laths) => All(world).FirstOrDefault(binding => binding.Starts(laths));

    public static bool Wet(ItemStack lath) => lath.Collectible.Variant["state"] == "wet";

    /// <summary>Spawns her blank at <paramref name="at"/>, lying along <paramref name="yaw"/>, her first stage laid, setting by <paramref name="setsAt"/>. Server only.</summary>
    public Entity Bind(IWorldAccessor world, Vec3d at, float yaw, Wood wood, double setsAt)
    {
        var vessel = world.ClassRegistry.CreateEntity(type);
        vessel.Pos.SetPos(at);
        vessel.Pos.Yaw = yaw;
        EntityBehaviorCovering.Begin(vessel, config, wood.Code, setsAt);
        EntityBehaviorVesselIntegrity.Ungreased(vessel);
        world.SpawnEntity(vessel);
        return vessel;
    }

    private bool Starts(ItemStack laths) =>
        WildcardUtil.Match(new AssetLocation(config.Laths), laths.Collectible.Code) && laths.StackSize == start.Laths
        && int.TryParse(laths.Collectible.Variant["length"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var metres) && metres == start.Metres;
}
