using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class EntityBehaviorMooring(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:mooring";

    private const string TreeKey = "roughseas:mooring";
    private const string LoweredKey = "lowered";
    private const int DataVersion = 1;
    private const double Unmoored = -1;
    private const double NewtonsPerKilonewton = 1000;
    private const double ReelInBlocksPerTick = 0.01;
    private static readonly AssetLocation RopeRipSound = new("game:sounds/effect/roperip");

    private StormsModSystem storms = null!;
    private ISeaWeather sea = null!;
    private MaterialTable materials = null!;
    private ClothManager cloths = null!;
    private EntityBehaviorSeaForces? forces;
    private EntityBehaviorAttachable? accessories;
    private EntityBehaviorRopeTieable? tieable;
    private IMountable? mountable;
    private AshoreRoll? ashore;
    private float ashoreIn;
    private float sinceUpdate;
    private bool hanging;

    public Anchor? Anchor
    {
        get
        {
            var tree = Tree?.GetTreeAttribute("anchor");
            return tree == null
                ? null
                : new Anchor(tree.GetDouble("x"), tree.GetDouble("y"), tree.GetDouble("z"), tree.GetDouble("rodeMetres"),
                    tree.GetString("rope"), tree.GetDouble("holdingNewtons"));
        }
        private set
        {
            var tree = Tree;
            tree.RemoveAttribute("anchor");
            if (value is not { } anchor) return;

            var anchorTree = tree.GetOrAddTreeAttribute("anchor");
            anchorTree.SetDouble("x", anchor.X);
            anchorTree.SetDouble("y", anchor.Y);
            anchorTree.SetDouble("z", anchor.Z);
            anchorTree.SetDouble("rodeMetres", anchor.RodeMetres);
            anchorTree.SetString("rope", anchor.Rope);
            anchorTree.SetDouble("holdingNewtons", anchor.HoldingNewtons);
        }
    }

    public double Exposure => Anchor != null ? storms.Config.Mooring.AnchoredExposure : 1;

    private ITreeAttribute Tree => entity.Attributes.GetTreeAttribute(TreeKey);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var mods = entity.Api.ModLoader;
        storms = mods.GetModSystem<StormsModSystem>();
        sea = mods.GetModSystem<SeaWeatherSystem>();
        materials = mods.GetModSystem<VesselsModSystem>().Materials;
        cloths = mods.GetModSystem<ClothManager>();
        mountable = entity.GetInterface<IMountable>();
        if (Tree == null)
        {
            var tree = new TreeAttribute();
            tree.SetInt("version", DataVersion);
            entity.Attributes.SetAttribute(TreeKey, tree);
        }
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        forces = entity.GetBehavior<EntityBehaviorSeaForces>();
        accessories = entity.GetBehavior<EntityBehaviorAttachable>();
        tieable = entity.GetBehavior<EntityBehaviorRopeTieable>();
        if (forces != null && entity.World.Side == EnumAppSide.Server) forces.Wave += Strain;
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (forces != null) forces.Wave -= Strain;
        base.OnEntityDespawn(despawn);
    }

    public override void OnEntityLoaded()
    {
        if (entity.World.Side != EnumAppSide.Server || !sea.DangerousOceans) return;

        var lastSeen = Tree.GetDouble("lastSeenHours", Unmoored);
        var reach = Tree.GetDouble("reach", Unmoored);
        if (lastSeen >= 0 && reach >= 0 && sea.NowHours - lastSeen >= storms.Config.Ashore.MinHours)
        {
            ashore = new AshoreRoll(storms, sea, entity, Tree.GetDouble("pointX"), Tree.GetDouble("pointZ"), lastSeen, reach);
            ashoreIn = (float)storms.Config.Ashore.RollDelaySeconds;
        }
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || forces == null || !entity.Alive) return;

        using var _ = storms.Budget.Measure();
        if (Anchor is { } leashed) Leash(leashed);
        else if (StakeRope() is { } rope && Stake(rope) is { } stake) Hold(stake.X + 0.5, stake.Z + 0.5, storms.Config.Mooring.StakeReachMetres);

        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        FollowAnchor();
        if (ashore != null)
        {
            ashoreIn -= sinceUpdate;
            sinceUpdate = 0;
            if (ashoreIn > 0) return;

            if (sea.DangerousOceans) ashore.Roll(forces, entity.GetBehavior<EntityBehaviorVesselIntegrity>());
            ashore = null;
        }
        sinceUpdate = 0;
        Tree.SetDouble("lastSeenHours", sea.NowHours);
        RecordMooring();
    }

    private void RecordMooring()
    {
        var (x, z, reach) = Anchor is { } anchor
            ? (anchor.X, anchor.Z, anchor.Reach(Depth(anchor)))
            : StakeRope() is { } rope && Stake(rope) is { } stake
                ? (stake.X + 0.5, stake.Z + 0.5, storms.Config.Mooring.StakeReachMetres)
                : (0, 0, Unmoored);
        Tree.SetDouble("pointX", x);
        Tree.SetDouble("pointZ", z);
        Tree.SetDouble("reach", reach);
    }

    private void Strain(SeaState state)
    {
        var force = forces!.Force(state);
        if (Anchor is { } anchor) Strain(anchor, force, state);
        else if (StakeRope() is { } rope) Strain(rope, force);
    }

    private void FollowAnchor()
    {
        var slot = AnchorSlot();
        if (slot?.Itemstack is not { } stack || !stack.Attributes.GetBool(LoweredKey))
        {
            hanging = false;
            if (Anchor == null) return;

            Anchor = null;
            storms.Log(entity, "anchor weighed");
            return;
        }
        var attributes = stack.Collectible.Attributes;
        var aboard = attributes["rodeMetres"].AsDouble() + SpareRodeMetres();
        if (Anchor is { } down)
        {
            if (aboard < down.RodeMetres) Shorten(down, aboard);
            return;
        }
        if (!entity.FeetInLiquid) return;

        var depth = forces!.WaterDepth((int)Math.Ceiling(aboard) + 1);
        var pos = entity.Pos;
        var fullScope = storms.Config.Mooring.AnchorFullScope;
        var anchor = Storms.Anchor.LetGo(pos.X, Math.Floor(pos.Y) - depth, pos.Z, depth, aboard, fullScope,
            attributes["rope"].AsString(""), attributes["holdingNewtons"].AsDouble());
        if (anchor == null)
        {
            if (!hanging) storms.Log(entity, "anchor hangs: {0:F0} m of rode aboard, deeper water below", aboard);
            hanging = true;
            return;
        }

        hanging = false;
        Anchor = anchor;
        storms.Log(entity, "anchor down in {0:F0} m, paid out {1:F0} of {2:F0} m, holds {3:F0} N",
            depth, anchor.Value.RodeMetres, aboard, anchor.Value.Holding(depth, fullScope));
    }

    private void Shorten(Anchor anchor, double aboard)
    {
        var depth = Depth(anchor);
        if (depth > aboard)
        {
            Anchor = null;
            hanging = true;
            storms.Log(entity, "rode taken in to {0:F0} m: the anchor no longer reaches the bottom {1:F0} m down", aboard, depth);
            return;
        }

        Anchor = anchor with { RodeMetres = aboard };
        storms.Log(entity, "rode taken in to {0:F0} m, holds {1:F0} N", aboard, Anchor.Value.Holding(depth, storms.Config.Mooring.AnchorFullScope));
    }

    private ItemSlot? AnchorSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemAnchor);

    private ItemSlot? SpareRodeSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is { } coil and not ItemAnchor && coil.Attributes?["rodeMetres"].Exists == true);

    private double SpareRodeMetres() =>
        SpareRodeSlot()?.Itemstack is { } coil ? coil.StackSize * coil.Collectible.Attributes["rodeMetres"].AsDouble() : 0;

    /// <summary>A steered vessel drags its anchor along: the rider's client moves it, and the server cannot hold it back.</summary>
    private void Leash(Anchor anchor)
    {
        var reach = anchor.Reach(Depth(anchor));
        if (mountable?.IsBeingControlled() != true)
        {
            Hold(anchor.X, anchor.Z, reach);
            return;
        }

        var pos = entity.Pos;
        var (dx, dz) = (pos.X - anchor.X, pos.Z - anchor.Z);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        if (distance <= reach) return;

        var over = (distance - reach) / distance;
        Anchor = anchor.DraggedBy(dx * over, dz * over);
    }

    private void Hold(double x, double z, double reach)
    {
        var pos = entity.Pos;
        var (dx, dz) = (pos.X - x, pos.Z - z);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        if (distance <= reach) return;

        var outward = (pos.Motion.X * dx + pos.Motion.Z * dz) / distance;
        var pull = Math.Max(0, outward) + Math.Min(distance - reach, ReelInBlocksPerTick);
        pos.Motion.X -= pull * dx / distance;
        pos.Motion.Z -= pull * dz / distance;
    }

    private static BlockPos? Stake(ClothSystem rope) => rope.FirstPoint.PinnedToBlockPos ?? rope.LastPoint.PinnedToBlockPos;

    private void Strain(Anchor anchor, double force, SeaState state)
    {
        var rand = entity.World.Rand;
        var rope = RopeNewtons(anchor.Rope);
        if (rand.NextDouble() < Waves.ChanceToExceed(force, rope))
        {
            LoseAnchor(anchor);
            storms.Log(entity, "rode snapped: the sea pulls {0:F0} N, the {1} holds {2:F0} N; anchor lost", force, anchor.Rope, rope);
            return;
        }

        var holding = anchor.Holding(Depth(anchor), storms.Config.Mooring.AnchorFullScope);
        if (state.WindSpeed <= 0 || rand.NextDouble() >= Waves.ChanceToExceed(force, holding)) return;

        var drag = storms.Config.Mooring.AnchorDragMetres / state.WindSpeed;
        Anchor = anchor.DraggedBy(state.WindX * drag, state.WindZ * drag);
        storms.Log(entity, "anchor dragged: the sea pulls {0:F0} N, it holds {1:F0} N at scope {2:F1}", force, holding, anchor.RodeMetres / Math.Max(1, Depth(anchor)));
    }

    private void Strain(ClothSystem stakeRope, double force)
    {
        var rope = RopeNewtons(storms.Config.Mooring.StakeRope);
        if (entity.World.Rand.NextDouble() >= Waves.ChanceToExceed(force, rope)) return;

        tieable?.Detach(stakeRope);
        cloths.UnregisterCloth(stakeRope.ClothId);
        Snapped();
        storms.Log(entity, "stake rope snapped: the sea pulls {0:F0} N, the rope holds {1:F0} N", force, rope);
    }

    private void LoseAnchor(Anchor anchor)
    {
        if (AnchorSlot() is { Itemstack: { } stack } slot)
        {
            var spareOut = anchor.RodeMetres - stack.Collectible.Attributes["rodeMetres"].AsDouble();
            if (spareOut > 0 && SpareRodeSlot() is { Itemstack: { } coil } spare)
            {
                spare.TakeOut((int)Math.Ceiling(spareOut / coil.Collectible.Attributes["rodeMetres"].AsDouble(1)));
            }
            slot.Itemstack = null;
            accessories!.storeInv();
            entity.MarkShapeModified();
        }
        Anchor = null;
        Snapped();
    }

    private void Snapped() => entity.World.PlaySoundAt(RopeRipSound, entity, null, true, 32);

    private ClothSystem? StakeRope() =>
        tieable?.ClothIds?.value
            .Select(cloths.GetClothSystem)
            .FirstOrDefault(rope => rope != null && (rope.FirstPoint.PinnedToBlockPos != null || rope.LastPoint.PinnedToBlockPos != null));

    private double RopeNewtons(string rope) => materials.BreakingKilonewtons(rope) * NewtonsPerKilonewton;

    private double Depth(Anchor anchor) => Math.Max(0, entity.Pos.Y - anchor.Y);
}
