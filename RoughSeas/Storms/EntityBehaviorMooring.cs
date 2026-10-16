using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
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
            ashore = new AshoreRoll(storms, sea, entity, lastSeen, reach);
        }
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || forces == null) return;

        using var _ = storms.Budget.Measure();
        if (Anchor is { } leashed) Leash(leashed);

        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceUpdate = 0;
        FollowAnchor();
        if (ashore != null)
        {
            if (!ashore.AreaLoaded()) return;

            ashore.Roll(forces, entity.GetBehavior<EntityBehaviorVesselIntegrity>());
            ashore = null;
        }
        Tree.SetDouble("lastSeenHours", sea.NowHours);
        Tree.SetDouble("reach", Anchor is { } anchor ? anchor.Reach(Depth(anchor)) : StakeRope() != null ? storms.Config.Mooring.StakeReachMetres : Unmoored);
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
        if (Anchor != null || !entity.FeetInLiquid) return;

        var attributes = stack.Collectible.Attributes;
        var aboard = attributes["rodeMetres"].AsDouble() + SpareRodeMetres();
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

    private ItemSlot? AnchorSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemAnchor);

    private ItemSlot? SpareRodeSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is { } coil and not ItemAnchor && coil.Attributes?["rodeMetres"].Exists == true);

    private double SpareRodeMetres() =>
        SpareRodeSlot()?.Itemstack is { } coil ? coil.StackSize * coil.Collectible.Attributes["rodeMetres"].AsDouble() : 0;

    /// <summary>A steered vessel drags its anchor along: the rider's client moves it, and the server cannot hold it back.</summary>
    private void Leash(Anchor anchor)
    {
        var pos = entity.Pos;
        var dx = pos.X - anchor.X;
        var dz = pos.Z - anchor.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var reach = anchor.Reach(Depth(anchor));
        if (distance <= reach) return;

        if (mountable?.IsBeingControlled() == true)
        {
            var over = (distance - reach) / distance;
            Anchor = anchor.DraggedBy(dx * over, dz * over);
            return;
        }

        var outward = (pos.Motion.X * dx + pos.Motion.Z * dz) / distance;
        if (outward <= 0) return;

        pos.Motion.X -= outward * dx / distance;
        pos.Motion.Z -= outward * dz / distance;
    }

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
