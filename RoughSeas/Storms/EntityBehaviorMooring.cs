using System;
using System.Collections.Generic;
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
    private static readonly AssetLocation RopeRipSound = new("game:sounds/effect/roperip");

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private EntityBehaviorSeaForces? forces;
    private EntityBehaviorAttachable? accessories;
    private IMountable? mountable;
    private float sinceUpdate;
    private double sinceWave;
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
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
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
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || forces == null) return;

        if (Anchor is { } leashed) Leash(leashed);

        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceWave += sinceUpdate;
        sinceUpdate = 0;
        FollowAnchor();
        var stake = Stake();
        var anchor = Anchor;
        Tree.SetDouble("lastSeenHours", sea.NowHours);
        Tree.SetDouble("reach", anchor is { } held ? held.Reach(Depth(held)) : stake != null ? storms.Config.Mooring.StakeReachMetres : -1);
        if (!entity.FeetInLiquid || !sea.DangerousOceans || (anchor == null && stake == null)) return;

        using var _ = sea.Budget.Measure();
        var state = sea.At(entity.Pos.X, entity.Pos.Z);
        if (state.WaveHeight < storms.Config.Waves.MinHeight || sinceWave < storms.Waves.Period(state.WaveHeight)) return;

        sinceWave = 0;
        var force = forces.Force(state);
        if (anchor is { } anchored) Strain(anchored, force, state);
        else Strain(stake!, force);
    }

    public override void OnEntityLoaded()
    {
        if (entity.World.Side != EnumAppSide.Server || forces == null || !sea.DangerousOceans) return;

        var lastSeen = Tree.GetDouble("lastSeenHours", -1);
        var config = storms.Config.Ashore;
        if (lastSeen < 0 || sea.NowHours - lastSeen < config.MinHours) return;

        var reach = Tree.GetDouble("reach", -1);
        if (reach < 0) return;

        var hoursPerSample = Math.Max(1, (sea.NowHours - lastSeen) / config.MaxSamples);
        var heights = Heights(lastSeen, hoursPerSample);
        if (heights.Count == 0) return;

        var gap = ShoreDistance(config.ShoreSearchBlocks) - reach;
        var chance = new ShoreHazard(config).Chance(heights, hoursPerSample, gap);
        var thrown = entity.World.Rand.NextDouble() < chance;
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (thrown) hull?.Strike(forces.BreakerEnergy(heights.Max()) * config.ImpactFactor);
        storms.Log(entity, "slept {0:F0} h moored, reach {1:F0} m, shore gap {2:F0}, highest waves {3:F1} m: ashore chance {4:F3}, thrown {5}, hull now {6:F2}",
            sea.NowHours - lastSeen, reach, gap, heights.Max(), chance, thrown, hull?.Integrity ?? 1);
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
        if (depth > aboard)
        {
            if (!hanging) storms.Log(entity, "anchor hangs: {0:F0} m of rode aboard, deeper water below", aboard);
            hanging = true;
            return;
        }

        hanging = false;
        var pos = entity.Pos;
        var anchor = new Anchor(pos.X, Math.Floor(pos.Y) - depth, pos.Z,
            Math.Min(aboard, depth * storms.Config.Mooring.AnchorFullScope), attributes["rope"].AsString(""), attributes["holdingNewtons"].AsDouble());
        Anchor = anchor;
        storms.Log(entity, "anchor down in {0:F0} m, paid out {1:F0} of {2:F0} m, holds {3:F0} N",
            depth, anchor.RodeMetres, aboard, anchor.Holding(depth, storms.Config.Mooring.AnchorFullScope));
    }

    private ItemSlot? AnchorSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemAnchor);

    private ItemSlot? SpareRodeSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is { } coil and not ItemAnchor && coil.Attributes?["rodeMetres"].Exists == true);

    private double SpareRodeMetres() =>
        SpareRodeSlot()?.Itemstack is { } coil ? coil.StackSize * coil.Collectible.Attributes["rodeMetres"].AsDouble() : 0;

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
            // The rider's client moves a steered vessel and the server cannot hold it back, so the anchor comes along.
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

    private void Strain(ClothSystem stake, double force)
    {
        var rope = RopeNewtons(storms.Config.Mooring.StakeRope);
        if (entity.World.Rand.NextDouble() >= Waves.ChanceToExceed(force, rope)) return;

        entity.GetBehavior<EntityBehaviorRopeTieable>()?.Detach(stake);
        entity.Api.ModLoader.GetModSystem<ClothManager>().UnregisterCloth(stake.ClothId);
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
                spare.MarkDirty();
            }
            slot.Itemstack = null;
            slot.MarkDirty();
            entity.MarkShapeModified();
        }
        Anchor = null;
        Snapped();
    }

    private void Snapped() => entity.World.PlaySoundAt(RopeRipSound, entity, null, true, 32);

    private ClothSystem? Stake()
    {
        var ids = entity.GetBehavior<EntityBehaviorRopeTieable>()?.ClothIds?.value;
        if (ids == null) return null;

        var cloths = entity.Api.ModLoader.GetModSystem<ClothManager>();
        return ids.Select(cloths.GetClothSystem)
            .FirstOrDefault(cloth => cloth != null && (cloth.FirstPoint.PinnedToBlockPos != null || cloth.LastPoint.PinnedToBlockPos != null));
    }

    private double RopeNewtons(string rope) =>
        entity.Api.ModLoader.GetModSystem<VesselsModSystem>().Materials.Strength(rope) * 1000;

    private double Depth(Anchor anchor) => Math.Max(0, entity.Pos.Y - anchor.Y);

    private List<double> Heights(double since, double hoursPerSample)
    {
        var heights = new List<double>();
        for (var hours = since; hours < sea.NowHours; hours += hoursPerSample)
        {
            heights.Add(sea.At(entity.Pos.X, entity.Pos.Z, hours).WaveHeight);
        }
        return heights;
    }

    private double ShoreDistance(int searchBlocks)
    {
        var accessor = entity.World.BlockAccessor;
        var centre = entity.Pos.AsBlockPos;
        var probe = new BlockPos(centre.dimension);
        for (var radius = 0; radius <= searchBlocks; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;

                    probe.Set(centre.X + dx, centre.Y, centre.Z + dz);
                    if (!accessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() && accessor.GetBlock(probe).SideSolid.Any) return radius;
                }
            }
        }
        return double.PositiveInfinity;
    }
}
