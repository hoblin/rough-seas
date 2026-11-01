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
/// laths laid out in a vessel's layout ties them into her blank where they lie (<see cref="LathBinding"/>).
/// </summary>
public class BlockEntityLathBed : BlockEntity, IWorkedHull
{
    public const string ClassName = "roughseas:LathBed";

    private const string LathsKey = "laths";
    private const double CrossingLiftMetres = 0.01875;
    private const int ReachBlocks = 4;
    private const int GapBlocks = 2;

    private readonly List<BedLath> laths = [];

    public bool Empty => laths.Count == 0;

    public void Add(ItemStack lath, double x, double z, EnumAxis along)
    {
        laths.Add(new BedLath(lath, x - Pos.X, z - Pos.Z, along));
        Changed();
    }

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
        if (!LathBinding.All(Api.World).Any(binding => binding.Rope.Takes(slot)) || Bound() is not { } bound) return null;

        return (bound.Binding.Seconds, bound.Wet ? null : "shipyard-soak-laths");
    }

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        if (Bound() is not { Wet: true } bound || !LandClaims.MayBreak(byEntity, bound.Beds.Select(bed => bed.Pos))) return;

        var world = Api.World;
        double hours = world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours;
        var vessel = bound.Binding.Bind(world, bound.Laid, Pos.Y, hours + bound.WetHours);
        foreach (var bed in bound.Beds) world.BlockAccessor.SetBlock(0, bed.Pos);
        if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative) bound.Binding.Rope.Spend(slot, byEntity);
        slot.MarkDirty();
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
            laid.Translate((float)lath.X, lath.Along == EnumAxis.Z ? (float)CrossingLiftMetres : 0, (float)lath.Z);
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

    private Layout? Bound()
    {
        var beds = Beds().ToArray();
        var laid = beds.SelectMany(bed => bed.laths.Select(bed.Laid)).OfType<LaidLath>().ToArray();
        if (LathBinding.Find(Api.World, laid) is not var (binding, found)) return null;

        var wetHours = beds.SelectMany(bed => bed.WetHours()).ToArray();
        return new Layout(binding, found, beds, wetHours.DefaultIfEmpty(0).Max(), wetHours.All(hours => hours > 0));
    }

    private LaidLath? Laid(BedLath lath) =>
        Wood.Of(lath.Stack.Collectible.Variant) is { } wood ? new LaidLath(wood, Pos.X + lath.X, Pos.Z + lath.Z, lath.Along, Metres(lath.Stack)) : null;

    private IEnumerable<double> WetHours()
    {
        bool dried = false;
        var hours = laths.Select(lath =>
        {
            var code = lath.Stack.Collectible.Code;
            var left = lath.Stack.Collectible.UpdateAndGetTransitionState(Api.World, new DummySlot(lath.Stack), EnumTransitionType.Dry)?.FreshHoursLeft ?? 0;
            dried |= lath.Stack.Collectible.Code != code;
            return Wet(lath.Stack) ? (double)left : 0;
        }).ToArray();
        if (dried && Api.Side == EnumAppSide.Server) MarkDirty(true);
        return hours;
    }

    private IEnumerable<BlockEntityLathBed> Beds()
    {
        var found = new Dictionary<BlockPos, BlockEntityLathBed> { [Pos] = this };
        var queue = new Queue<BlockEntityLathBed>([this]);
        while (queue.TryDequeue(out var bed))
        {
            for (int dx = -GapBlocks; dx <= GapBlocks; dx++)
            {
                for (int dz = -GapBlocks; dz <= GapBlocks; dz++)
                {
                    var at = bed.Pos.AddCopy(dx, 0, dz);
                    if (found.ContainsKey(at) || Math.Abs(at.X - Pos.X) > ReachBlocks || Math.Abs(at.Z - Pos.Z) > ReachBlocks) continue;
                    if (Api.World.BlockAccessor.GetBlockEntity(at) is not BlockEntityLathBed next) continue;

                    found[at] = next;
                    queue.Enqueue(next);
                }
            }
        }
        return found.Values;
    }

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

    private sealed record Layout(LathBinding Binding, LaidOut Laid, BlockEntityLathBed[] Beds, double WetHours, bool Wet);

    private sealed class LogFace(ITexPositionSource log, string face) : ITexPositionSource
    {
        public TextureAtlasPosition this[string textureCode] => log[face];

        public Size2i AtlasSize => log.AtlasSize;
    }
}
