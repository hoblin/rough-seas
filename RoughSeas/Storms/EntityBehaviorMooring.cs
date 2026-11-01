using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
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
    private const double TautMetres = 0.5;
    private const double PullLogShare = 0.1;
    private const double StillMps = 0.01;
    private static readonly AssetLocation RopeRipSound = new("game:sounds/effect/roperip");

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private MaterialTable materials = null!;
    private ClothManager cloths = null!;
    private EntityBehaviorSeaForces? forces;
    private EntityBehaviorAttachable? accessories;
    private EntityBehaviorRopeTieable? tieable;
    private EntityBehaviorCurrentDrift? drift;
    private EntityBehaviorPassivePhysicsMultiBox? physics;
    private double swingRate;
    private float sinceSwingLog;
    private double loggedPull;
    private long loggedPullAt;
    private IMountable? mountable;
    private AshoreRoll? ashore;
    private float ashoreIn;
    private float sinceUpdate;
    private bool hanging;
    private bool taut;

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
        drift = entity.GetBehavior<EntityBehaviorCurrentDrift>();
        physics = entity.GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();
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
        if (lastSeen >= 0 && reach >= 0 && sea.Clock.Hours - lastSeen >= storms.Config.Ashore.MinHours)
        {
            ashore = new AshoreRoll(storms, sea.Model, entity, Tree.GetDouble("pointX"), Tree.GetDouble("pointZ"), lastSeen, reach);
            ashoreIn = (float)storms.Config.Ashore.RollDelaySeconds;
        }
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || forces == null || !entity.Alive) return;

        using var _ = storms.Budget.Measure();
        var held = false;
        if (Anchor is { } leashed) held = Leash(leashed);
        else if (StakeRope() is { } rope && Stake(rope) is { } stake)
        {
            // vanilla's cloth spring would drag a hull with no water drag on her to the stake: the rope holds her here,
            // and only when taut
            if (drift != null) rope.CanPull = false;
            held = Hold(stake.X + 0.5, stake.Z + 0.5, Reach(rope));
        }
        taut = held && mountable?.IsBeingControlled() != true;
        if (taut) Swing(deltaTime);

        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        FollowAnchor();
        StrainSteady();
        if (ashore != null)
        {
            ashoreIn -= sinceUpdate;
            sinceUpdate = 0;
            if (ashoreIn > 0) return;

            if (sea.DangerousOceans) ashore.Roll(entity.GetBehavior<EntityBehaviorVesselIntegrity>());
            ashore = null;
        }
        sinceUpdate = 0;
        Tree.SetDouble("lastSeenHours", sea.Clock.Hours);
        RecordMooring();
    }

    private void RecordMooring()
    {
        var (x, z, reach) = Anchor is { } anchor
            ? (anchor.X, anchor.Z, anchor.Reach(Depth(anchor)))
            : StakeRope() is { } rope && Stake(rope) is { } stake
                ? (stake.X + 0.5, stake.Z + 0.5, Reach(rope))
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

    /// <summary>
    /// The current's pull on her mooring, newtons in world axes: the drag of her underwater body on the water running
    /// past her as the rope holds her taut. Nothing while she swings free within its reach or someone steers her.
    /// </summary>
    private (double X, double Z) CurrentPull()
    {
        if (!taut || !entity.Swimming || drift?.Body is not { } body) return (0, 0);

        var (vx, vz) = drift.Velocity;
        var (wx, wz) = drift.Water;
        return body.Pull(vx, vz, wx, wz, entity.Pos.Yaw);
    }

    /// <summary>A steady pull past what the rope or the anchor holds parts or drags it, whatever the waves do.</summary>
    private void StrainSteady()
    {
        var (x, z) = CurrentPull();
        var pull = Math.Sqrt(x * x + z * z);
        if (Anchor is { } anchor) StrainSteady(anchor, pull, x, z);
        else if (StakeRope() is { } rope) StrainSteady(rope, pull);
        else loggedPull = 0;
    }

    private void StrainSteady(Anchor anchor, double pull, double x, double z)
    {
        var rope = RopeNewtons(anchor.Rope);
        var holding = anchor.Holding(Depth(anchor), storms.Config.Mooring.AnchorFullScope);
        if (Changed(pull))
        {
            storms.Log(entity, "the current pulls {0:F0} N on her rode; the anchor holds {1:F0} N at scope {2:F1}, the {3} {4:F0} N",
                pull, holding, anchor.RodeMetres / Math.Max(1, Depth(anchor)), anchor.Rope, rope);
        }
        if (!sea.DangerousOceans || pull <= 0) return;

        if (pull >= rope)
        {
            LoseAnchor(anchor);
            storms.Log(entity, "rode snapped: the current pulls {0:F0} N, the {1} holds {2:F0} N; anchor lost", pull, anchor.Rope, rope);
            return;
        }
        if (pull < holding) return;

        var drag = storms.Config.Mooring.AnchorDragMetres / pull;
        Anchor = anchor.DraggedBy(x * drag, z * drag);
        storms.Log(entity, "anchor dragged: the current pulls {0:F0} N, it holds {1:F0} N", pull, holding);
    }

    private void StrainSteady(ClothSystem stakeRope, double pull)
    {
        var rope = RopeNewtons(storms.Config.Mooring.StakeRope);
        if (Changed(pull)) storms.Log(entity, "the current pulls {0:F0} N on her stake rope, which holds {1:F0} N", pull, rope);
        if (!sea.DangerousOceans || pull < rope) return;

        PartStake(stakeRope);
        storms.Log(entity, "stake rope snapped: the current pulls {0:F0} N, the rope holds {1:F0} N", pull, rope);
    }

    /// <summary>Whether the pull has changed by a tenth since it was last logged, a log interval ago or more.</summary>
    private bool Changed(double pull)
    {
        var now = entity.World.ElapsedMilliseconds;
        if (now - loggedPullAt < storms.Config.Drift.LogSeconds * 1000) return false;
        if (Math.Abs(pull - loggedPull) <= PullLogShare * Math.Max(pull, loggedPull)) return false;

        (loggedPull, loggedPullAt) = (pull, now);
        return true;
    }

    /// <summary>
    /// Held at her bow, she turns until the water's drag on her side balances about it: stern downstream, lying along
    /// the stream.
    /// </summary>
    private void Swing(float deltaTime)
    {
        if (!entity.Swimming || drift?.Body is not { } body || physics == null) return;

        var pos = entity.Pos;
        var (vx, vz) = drift.Velocity;
        var (wx, wz) = drift.Water;
        var (along, across) = HullShape.ToHull(wx - vx, wz - vz, pos.Yaw);
        (swingRate, var turned) = body.Swing(swingRate, across, deltaTime);
        // turning counterclockwise in her own frame lowers her yaw
        var yaw = (float)(pos.Yaw - turned);
        if (physics.AdjustCollisionBoxesToYaw(deltaTime, true, yaw)) pos.Yaw = yaw;
        else swingRate = 0;

        sinceSwingLog += deltaTime;
        if (sinceSwingLog < storms.Config.Drift.LogSeconds || Math.Sqrt(along * along + across * across) < StillMps) return;

        sinceSwingLog = 0;
        var (x, z) = body.Pull(vx, vz, wx, wz, pos.Yaw);
        storms.Log(entity, "swings at {0:F1} deg/s, {1:F0} deg off the stream with her stern {2}, the current pulling {3:F0} N",
            -swingRate * GameMath.RAD2DEG, Math.Atan2(Math.Abs(across), along) * GameMath.RAD2DEG, along >= 0 ? "downstream" : "upstream", Math.Sqrt(x * x + z * z));
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

    /// <summary>
    /// Holds the vessel at the anchor's reach, whether the rode is taut. A steered vessel drags its anchor along
    /// instead: the rider's client moves it, and the server cannot hold it back.
    /// </summary>
    private bool Leash(Anchor anchor)
    {
        var reach = anchor.Reach(Depth(anchor));
        if (mountable?.IsBeingControlled() != true) return Hold(anchor.X, anchor.Z, reach);

        var pos = entity.Pos;
        var (dx, dz) = (pos.X - anchor.X, pos.Z - anchor.Z);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        if (distance <= reach) return false;

        var over = (distance - reach) / distance;
        Anchor = anchor.DraggedBy(dx * over, dz * over);
        return false;
    }

    /// <summary>Takes away the vessel's motion out past the point's reach; whether the rope is taut.</summary>
    private bool Hold(double x, double z, double reach)
    {
        var pos = entity.Pos;
        var (dx, dz) = (pos.X - x, pos.Z - z);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        if (distance < reach - TautMetres) return false;
        if (distance <= reach) return true;

        var outward = (pos.Motion.X * dx + pos.Motion.Z * dz) / distance;
        var pull = Math.Max(0, outward) + Math.Min(distance - reach, ReelInBlocksPerTick);
        pos.Motion.X -= pull * dx / distance;
        pos.Motion.Z -= pull * dz / distance;
        return true;
    }

    /// <summary>As far as the rope reaches: its own length, which a tied rope takes from where it was tied, up to vanilla's longest.</summary>
    private double Reach(ClothSystem rope) =>
        Math.Min(storms.Config.Mooring.StakeReachMetres, RopeConstraints(rope).Sum(constraint => constraint.SpringLength));

    private static readonly AccessTools.FieldRef<ClothSystem, List<ClothConstraint>> RopeConstraints =
        AccessTools.FieldRefAccess<ClothSystem, List<ClothConstraint>>("Constraints");

    private static BlockPos? Stake(ClothSystem rope) => rope.FirstPoint.PinnedToBlockPos ?? rope.LastPoint.PinnedToBlockPos;

    private void Strain(Anchor anchor, double force, SeaState state)
    {
        var rand = entity.World.Rand;
        var rope = RopeNewtons(anchor.Rope);
        var (cx, cz) = CurrentPull();
        var current = Math.Sqrt(cx * cx + cz * cz);
        if (rand.NextDouble() < Waves.ChanceToExceed(force, rope, current))
        {
            LoseAnchor(anchor);
            storms.Log(entity, "rode snapped: the sea pulls {0:F0} N on the current's {1:F0} N, the {2} holds {3:F0} N; anchor lost", force, current, anchor.Rope, rope);
            return;
        }

        var holding = anchor.Holding(Depth(anchor), storms.Config.Mooring.AnchorFullScope);
        if (rand.NextDouble() >= Waves.ChanceToExceed(force, holding, current)) return;

        // dragged the way the wind, the waves and the current pull together
        var wind = Math.Max(state.WindSpeed, 1e-9);
        var (x, z) = (state.WindX / wind * force + cx, state.WindZ / wind * force + cz);
        var pull = Math.Sqrt(x * x + z * z);
        if (pull <= 0) return;

        var drag = storms.Config.Mooring.AnchorDragMetres / pull;
        Anchor = anchor.DraggedBy(x * drag, z * drag);
        storms.Log(entity, "anchor dragged: the sea pulls {0:F0} N on the current's {1:F0} N, it holds {2:F0} N at scope {3:F1}",
            force, current, holding, anchor.RodeMetres / Math.Max(1, Depth(anchor)));
    }

    private void Strain(ClothSystem stakeRope, double force)
    {
        var rope = RopeNewtons(storms.Config.Mooring.StakeRope);
        var (cx, cz) = CurrentPull();
        var current = Math.Sqrt(cx * cx + cz * cz);
        if (entity.World.Rand.NextDouble() >= Waves.ChanceToExceed(force, rope, current)) return;

        PartStake(stakeRope);
        storms.Log(entity, "stake rope snapped: the sea pulls {0:F0} N on the current's {1:F0} N, the rope holds {2:F0} N", force, current, rope);
    }

    private void PartStake(ClothSystem stakeRope)
    {
        tieable?.Detach(stakeRope);
        cloths.UnregisterCloth(stakeRope.ClothId);
        Snapped();
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
