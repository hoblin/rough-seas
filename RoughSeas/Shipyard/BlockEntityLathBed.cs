using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// Laths laid on the ground whose middles lie in this block, each at its full length along or across. Rope bound on
/// laths laid out in a vessel's layout ties them into her blank where they lie.
/// </summary>
public class BlockEntityLathBed : BlockEntity, IWorkedHull
{
    public const string ClassName = "roughseas:LathBed";

    private const string LathsKey = "laths";
    private const double LathMetres = 0.01;
    private const int Reach = 4;
    private const int Gap = 2;

    private readonly List<BedLath> laths = [];

    public bool Empty => laths.Count == 0;

    public void Add(ItemStack lath, double x, double z, EnumAxis along)
    {
        laths.Add(new BedLath(lath, x - Pos.X, z - Pos.Z, along));
        Changed();
    }

    /// <summary>Takes back the lath whose middle lies nearest <paramref name="x"/>, <paramref name="z"/>.</summary>
    public ItemStack? TakeNearest(double x, double z)
    {
        if (laths.MinBy(lath => Math.Pow(Pos.X + lath.X - x, 2) + Math.Pow(Pos.Z + lath.Z - z, 2)) is not { } nearest) return null;

        laths.Remove(nearest);
        Changed();
        return nearest.Stack;
    }

    public IEnumerable<ItemStack> Stacks => laths.Select(lath => lath.Stack);

    public (double Seconds, string? Refusal)? WorkWith(ItemSlot slot)
    {
        if (Bound() is not { } bound || !bound.Rope.Takes(slot)) return null;

        return (bound.Stage.Seconds, bound.Refusal);
    }

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        if (Bound() is not { Refusal: null } bound || !LandClaims.MayBreak(byEntity, bound.Beds.Select(bed => bed.Pos))) return;

        var world = Api.World;
        var vessel = world.ClassRegistry.CreateEntity(bound.Type);
        vessel.Pos.SetPos(bound.Laid.X, Pos.Y, bound.Laid.Z);
        vessel.Pos.Yaw = bound.Laid.Along == EnumAxis.X ? GameMath.PIHALF : 0;
        double hours = world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours;
        EntityBehaviorCovering.Begin(vessel, bound.Config, bound.Laid.Wood.Code, hours + bound.WetHours);
        EntityBehaviorVesselIntegrity.Ungreased(vessel);
        foreach (var bed in bound.Beds) world.BlockAccessor.SetBlock(0, bed.Pos);
        if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative) bound.Rope.Spend(slot, byEntity);
        slot.MarkDirty();
        world.SpawnEntity(vessel);
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), vessel, null, true, 16);
        world.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log(vessel, "bound by {0} from {1} {2} laths, setting in {3:F0} h",
            byEntity.GetName(), bound.Laid.Laths.Length, bound.Laid.Wood.Code, bound.WetHours);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        foreach (var group in laths.GroupBy(lath => lath.Stack.GetName())) dsc.AppendLine($"{group.Count()} × {group.Key}");
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Api is not ICoreClientAPI capi) return true;

        foreach (var lath in laths)
        {
            if (Mesh(capi, lath.Stack) is not { } mesh) continue;

            var laid = mesh.Clone().Scale(new Vec3f(0, 0, 0), (float)Metres(lath.Stack), 1, 1);
            if (lath.Along == EnumAxis.Z) laid.Rotate(new Vec3f(0, 0, 0), 0, GameMath.PIHALF, 0);
            laid.Translate((float)lath.X, lath.Along == EnumAxis.Z ? (float)LathMetres : 0, (float)lath.Z);
            mesher.AddMeshData(laid);
        }
        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        var list = new TreeAttribute();
        for (int i = 0; i < laths.Count; i++)
        {
            var lath = new TreeAttribute();
            lath.SetItemstack("stack", laths[i].Stack);
            lath.SetDouble("x", laths[i].X);
            lath.SetDouble("z", laths[i].Z);
            lath.SetString("along", laths[i].Along.ToString());
            list[i.ToString()] = lath;
        }
        tree[LathsKey] = list;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        laths.Clear();
        if (tree.GetTreeAttribute(LathsKey) is not { } list) return;

        foreach (var (_, value) in list.OrderBy(entry => int.Parse(entry.Key)))
        {
            if (value is not ITreeAttribute lath || lath.GetItemstack("stack") is not { } stack || !stack.ResolveBlockOrItem(worldForResolving)) continue;

            laths.Add(new BedLath(stack, lath.GetDouble("x"), lath.GetDouble("z"), Enum.Parse<EnumAxis>(lath.GetString("along"))));
        }
        MarkMeshDirty();
    }

    /// <summary>Whether the lath in <paramref name="slot"/> is still wet enough to bend.</summary>
    public static bool Wet(ItemStack lath) => lath.Collectible.Variant["state"] == "wet";

    public static double Metres(ItemStack lath) => double.Parse(lath.Collectible.Variant["length"] ?? "1", System.Globalization.CultureInfo.InvariantCulture);

    private void Changed()
    {
        MarkDirty(true);
        MarkMeshDirty();
    }

    private void MarkMeshDirty()
    {
        if (Api?.Side == EnumAppSide.Client) Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    private Binding? Bound()
    {
        var beds = Beds().ToArray();
        var stacks = beds.SelectMany(bed => bed.laths.Select(lath => (bed, lath))).ToArray();
        var laid = stacks.Select(entry => entry.bed.Laid(entry.lath)).Where(lath => lath != null).Cast<LaidLath>().ToArray();
        foreach (var (type, config) in Covered(Api.World))
        {
            if (config.Stages.Length < 2 || LathLayout.Of(config.Layout) is not { } layout) continue;
            if (layout.Find(laid, config.Stages[0].Metres.DefaultIfEmpty(0).Max()) is not { } found) continue;

            var wetHours = stacks.Select(entry => WetHours(entry.lath.Stack)).DefaultIfEmpty(0).Max();
            var refusal = stacks.All(entry => Wet(entry.lath.Stack)) ? null : "shipyard-soak-laths";
            return new Binding(type, config, found, beds, new RopeLaying(new AssetLocation(config.Rope)), config.Stages[1], wetHours, refusal);
        }
        return null;
    }

    private LaidLath? Laid(BedLath lath) =>
        Wood.Of(lath.Stack.Collectible.Variant) is { } wood ? new LaidLath(wood, Pos.X + lath.X, Pos.Z + lath.Z, lath.Along, Metres(lath.Stack)) : null;

    private double WetHours(ItemStack lath) =>
        lath.Collectible.UpdateAndGetTransitionState(Api.World, new DummySlot(lath), EnumTransitionType.Dry)?.FreshHoursLeft ?? 0;

    private IEnumerable<BlockEntityLathBed> Beds()
    {
        var found = new Dictionary<BlockPos, BlockEntityLathBed> { [Pos] = this };
        var queue = new Queue<BlockEntityLathBed>([this]);
        while (queue.TryDequeue(out var bed))
        {
            for (int dx = -Gap; dx <= Gap; dx++)
            {
                for (int dz = -Gap; dz <= Gap; dz++)
                {
                    var at = bed.Pos.AddCopy(dx, 0, dz);
                    if (found.ContainsKey(at) || Math.Abs(at.X - Pos.X) > Reach || Math.Abs(at.Z - Pos.Z) > Reach) continue;
                    if (Api.World.BlockAccessor.GetBlockEntity(at) is not BlockEntityLathBed next) continue;

                    found[at] = next;
                    queue.Enqueue(next);
                }
            }
        }
        return found.Values;
    }

    private static IEnumerable<(Vintagestory.API.Common.Entities.EntityProperties Type, CoveringConfig Config)> Covered(IWorldAccessor world) =>
        ObjectCacheUtil.GetOrCreate(world.Api, $"{ClassName}/covered", () => world.EntityTypes
            .Select(type => (type, config: CoveringConfig.Of(type)))
            .Where(entry => entry.config?.Layout != null)
            .Select(entry =>
            {
                entry.config!.Measure(entry.type.Client?.Shape?.Base is { } shape ? FramingConfig.ShapeAt(world.Api, shape) : null);
                return (entry.type, entry.config);
            })
            .ToArray());

    private static MeshData? Mesh(ICoreClientAPI capi, ItemStack lath)
    {
        if (Wood.Of(lath.Collectible.Variant) is not { } wood || capi.World.GetBlock(wood.LyingLog) is not { Id: > 0 } log) return null;

        return ObjectCacheUtil.GetOrCreate(capi, $"{ClassName}/mesh/{wood.Code}", () =>
        {
            var shape = Shape.TryGet(capi, new AssetLocation("roughseas:shapes/block/laidlath.json"));
            var source = capi.Tesselator.GetTextureSource(log);
            capi.Tesselator.TesselateShape(ClassName, shape, out var mesh, new LogFace(source, "inside-north"));
            return mesh;
        });
    }

    private sealed record BedLath(ItemStack Stack, double X, double Z, EnumAxis Along);

    private sealed record Binding(Vintagestory.API.Common.Entities.EntityProperties Type, CoveringConfig Config, LaidOut Laid,
        BlockEntityLathBed[] Beds, RopeLaying Rope, FramingStage Stage, double WetHours, string? Refusal);

    private sealed class LogFace(ITexPositionSource log, string face) : ITexPositionSource
    {
        public TextureAtlasPosition this[string textureCode] => log[face];

        public Size2i AtlasSize => log.AtlasSize;
    }
}
