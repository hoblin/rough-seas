using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class EntityBehaviorMooring(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:mooring";

    private const string TreeKey = "roughseas:mooring";
    private const string AnchorKey = "roughseas:anchor";
    private const string LoweredKey = "lowered";
    private const int DataVersion = 2;
    private const double Unmoored = -1;
    private const double NewtonsPerKilonewton = 1000;
    private const double ReelInBlocksPerTick = 0.01;
    private const double SlackMetres = 0.1;
    private const double TensionLogShare = 0.1;
    private const double FollowedLogMetres = 0.1;
    private const double RepinRadians = 5 * GameMath.DEG2RAD;
    private static readonly AssetLocation RopeRipSound = new("game:sounds/effect/roperip");

    /// <summary>The anchor's line key, beside the ids of vanilla's ropes, which start at 1.</summary>
    private const int AnchorLine = 0;

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private MaterialTable materials = null!;
    private ClothManager cloths = null!;
    private EntityBehaviorSeaForces? forces;
    private EntityBehaviorAttachable? accessories;
    private EntityBehaviorCurrentDrift? drift;
    private IMountable? mountable;
    private IHull? hull;
    private AshoreRoll? ashore;
    private float ashoreIn;
    private float sinceUpdate;
    private bool hanging;
    private double followedMetres;
    private volatile Hold[] holds = [];
    private volatile bool here;

    private sealed record Tether(int Key, double X, double Z, double ReachMetres, MooringLine Line)
    {
        public bool Anchored => Key == AnchorLine;

        public string Name => Anchored ? "the rode" : "the stake rope";

        /// <summary>The cosine of the line's slope: its reach over its length.</summary>
        public double Level => Line.RestMetres > 0 ? ReachMetres / Line.RestMetres : 0;
    }

    /// <summary>One line on her. The physics thread writes whether it is taut and when it comes taut; the tick reads them.</summary>
    private sealed class Hold(Tether tether)
    {
        private int arrived;
        private double slideDeceleration;

        public Tether Tether { get; set; } = tether;

        public volatile bool Taut;
        public volatile bool Slack;
        public volatile bool Slipping;
        public bool WasTaut;
        public double PinnedYaw = double.NaN;
        public bool Reported;
        public double LoggedTension;
        public long LoggedAt;

        public double SlideDeceleration
        {
            get => Volatile.Read(ref slideDeceleration);
            set => Volatile.Write(ref slideDeceleration, value);
        }

        public void Arrive() => Interlocked.Exchange(ref arrived, 1);

        public bool TakeArrival() => Interlocked.Exchange(ref arrived, 0) == 1;
    }

    public VesselRope Rope { get; private set; } = null!;

    public Anchor? Anchor
    {
        get
        {
            var tree = entity.WatchedAttributes.GetTreeAttribute(AnchorKey);
            return tree == null || !tree.HasAttribute("kilograms")
                ? null
                : new Anchor(tree.GetDouble("x"), tree.GetDouble("y"), tree.GetDouble("z"), tree.GetDouble("rodeMetres"),
                    tree.GetString("rope"), tree.GetDouble("kilograms"), tree.GetDouble("bedHold"));
        }
        private set
        {
            var watched = entity.WatchedAttributes;
            if (value is not { } anchor)
            {
                watched.RemoveAttribute(AnchorKey);
                return;
            }

            var tree = watched.GetOrAddTreeAttribute(AnchorKey);
            tree.SetDouble("x", anchor.X);
            tree.SetDouble("y", anchor.Y);
            tree.SetDouble("z", anchor.Z);
            tree.SetDouble("rodeMetres", anchor.RodeMetres);
            tree.SetString("rope", anchor.Rope);
            tree.SetDouble("kilograms", anchor.Kilograms);
            tree.SetDouble("bedHold", anchor.BedHold);
            watched.MarkPathDirty(AnchorKey);
        }
    }

    public double Exposure => Anchor != null ? storms.Config.Mooring.AnchoredExposure : 1;

    private ITreeAttribute Tree => entity.Attributes.GetTreeAttribute(TreeKey);

    private MooringConfig Config => storms.Config.Mooring;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var mods = entity.Api.ModLoader;
        storms = mods.GetModSystem<StormsModSystem>();
        sea = mods.GetModSystem<SeaWeatherSystem>();
        materials = mods.GetModSystem<VesselsModSystem>().Materials;
        cloths = mods.GetModSystem<ClothManager>();
        mountable = entity.GetInterface<IMountable>();
        if (entity.World.Side != EnumAppSide.Server) return;

        var tree = Tree;
        if (tree == null || tree.GetInt("version") < DataVersion)
        {
            tree ??= new TreeAttribute();
            tree.RemoveAttribute("anchor");
            tree.SetInt("version", DataVersion);
            entity.Attributes.SetAttribute(TreeKey, tree);
        }
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        forces = entity.GetBehavior<EntityBehaviorSeaForces>();
        accessories = entity.GetBehavior<EntityBehaviorAttachable>();
        drift = entity.GetBehavior<EntityBehaviorCurrentDrift>();
        Rope = new VesselRope(entity, cloths);
        hull = IHull.Of(entity);
    }

    public override void OnEntityLoaded()
    {
        if (entity.World.Side != EnumAppSide.Server || !Mechanic.MooringStrain.On(entity.World)) return;

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
        if (forces == null || !entity.Alive) return;

        using var _ = storms.Budget.Measure();
        var server = entity.World.Side == EnumAppSide.Server;
        var steered = mountable?.IsBeingControlled() == true;
        here = entity.Api is ICoreClientAPI client ? mountable?.Controller == client.World.Player?.Entity : !steered;
        Tie(server);
        if (here) Strain();
        else
        {
            foreach (var hold in holds) (hold.WasTaut, hold.Slack, hold.Slipping) = (false, false, false);
        }
        if (!server) return;

        FollowDragged(steered);
        drift?.HeldAtBow(here && holds.Any(hold => hold.Taut), deltaTime);
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        FollowAnchor();
        Rope.Prune();
        LogFollowed();
        if (ashore != null)
        {
            ashoreIn -= sinceUpdate;
            sinceUpdate = 0;
            if (ashoreIn > 0) return;

            if (Mechanic.MooringStrain.On(entity.World)) ashore.Roll(entity.GetBehavior<EntityBehaviorVesselIntegrity>());
            ashore = null;
        }
        sinceUpdate = 0;
        Tree.SetDouble("lastSeenHours", sea.Clock.Hours);
        RecordMooring();
    }

    /// <summary>
    /// In the move vanilla's physics is about to make: each taut line stops her bow going out past its reach, where her
    /// physics runs, and an anchor sliding along the bed pulls her back by its friction. Called from the physics thread.
    /// </summary>
    internal void Restrain(Vec3d motion, float seconds)
    {
        foreach (var hold in holds)
        {
            if (!here) hold.Taut = false;
            else if (hold.Slipping) Slide(hold, motion, seconds);
            else Restrain(hold, motion);
        }
    }

    private void Restrain(Hold hold, Vec3d motion)
    {
        var tied = hold.Tether;
        var (outX, outZ, distance) = Outward(tied);
        if (distance < tied.ReachMetres - SlackMetres)
        {
            (hold.Taut, hold.Slack) = (false, true);
            return;
        }
        if (distance < tied.ReachMetres) return;

        var (pivotX, pivotZ) = drift?.PivotMotion ?? default;
        var outward = (motion.X - pivotX) * outX + (motion.Z - pivotZ) * outZ;
        var pull = Math.Max(0, outward) + Math.Min(distance - tied.ReachMetres, ReelInBlocksPerTick);
        motion.X -= pull * outX;
        motion.Z -= pull * outZ;
        drift?.Hold(outX, outZ);
        if (hold.Slack) hold.Arrive();
        (hold.Taut, hold.Slack) = (true, false);
    }

    private void Slide(Hold hold, Vec3d motion, float seconds)
    {
        hold.Taut = true;
        var (outX, outZ, _) = Outward(hold.Tether);
        var outward = motion.X * outX + motion.Z * outZ;
        var cut = Math.Min(Math.Max(0, outward), hold.SlideDeceleration * seconds / WaterDrag.TicksPerSecond);
        motion.X -= cut * outX;
        motion.Z -= cut * outZ;
        drift?.Slow(outX, outZ, cut * WaterDrag.TicksPerSecond);
    }

    /// <summary>Server only: a line her controller's client found parted on the vessel it steers.</summary>
    public void Reported(IServerPlayer player, MooringReport report)
    {
        if (mountable?.Controller != player.Entity)
        {
            storms.Log(entity, "ignored a parted mooring reported by {0}, who does not steer her", player.PlayerName);
            return;
        }
        if (holds.FirstOrDefault(hold => hold.Tether.Key == report.Line) is { } parted) Part(parted, report.Newtons);
    }

    /// <summary>The lines that hold her now, where her physics runs: her anchor's rode and the rope that ties her to a post.</summary>
    private void Tie(bool server)
    {
        var before = holds;
        if (!server && !here)
        {
            holds = [];
            return;
        }

        var tethers = new List<Tether>();
        if (Anchor is { } anchor) tethers.Add(new Tether(AnchorLine, anchor.X, anchor.Z, anchor.Reach(Depth(anchor)), Line(anchor.Rope, anchor.RodeMetres)));
        var rope = Rope.Tied;
        if (rope != null && VesselRope.Post(rope) is { } post)
        {
            var length = VesselRope.RestMetres(rope);
            var rise = post.Y - entity.Pos.Y;
            tethers.Add(new Tether(rope.ClothId, post.X + 0.5, post.Z + 0.5, Math.Sqrt(Math.Max(0, length * length - rise * rise)), Line(Config.StakeRope, length)));
        }
        if (tethers.Count == 0 && before.Length == 0) return;

        var now = tethers.Select(tied => before.FirstOrDefault(hold => hold.Tether.Key == tied.Key) is { } kept ? Kept(kept, tied) : new Hold(tied)).ToArray();
        foreach (var gone in before.Where(hold => !hold.Tether.Anchored && now.All(kept => kept.Tether.Key != hold.Tether.Key)))
        {
            if (cloths.GetClothSystem(gone.Tether.Key) is { } loosed) VesselRope.GiveBack(loosed);
        }
        if (rope != null && now.FirstOrDefault(hold => hold.Tether.Key == rope.ClothId) is { } staked)
        {
            var yaw = entity.Pos.Yaw;
            var turned = double.IsNaN(staked.PinnedYaw) || Math.Abs(GameMath.AngleRadDistance((float)staked.PinnedYaw, yaw)) > RepinRadians;
            Rope.TakeOver(rope, server && turned ? hull?.Shape.Bow(yaw) ?? default : null);
            if (server && turned) staked.PinnedYaw = yaw;
        }
        if (!now.Select(hold => hold.Tether.Key).SequenceEqual(before.Select(hold => hold.Tether.Key)))
        {
            storms.Log(entity, now.Length == 0 ? "moored no longer" : "moored by {0}, her physics {1}",
                string.Join(", ", now.Select(hold => $"{hold.Tether.Name} {hold.Tether.ReachMetres:F1} m from {hold.Tether.X:F1} {hold.Tether.Z:F1}")), here ? "here" : "elsewhere");
        }
        holds = now;
    }

    private static Hold Kept(Hold hold, Tether tied)
    {
        hold.Tether = tied;
        return hold;
    }

    private MooringLine Line(string rope, double metres) =>
        new(metres, materials.BreakingKilonewtons(rope) * NewtonsPerKilonewton, materials.StretchAtBreak(rope));

    /// <summary>
    /// The pull on each taut line: the water's steady pull shared between them, and the jerk when she comes to a line's
    /// end, along the line, which lies at its slope.
    /// </summary>
    private void Strain()
    {
        var engaged = holds.Where(hold => hold.Taut || hold.Slipping).ToArray();
        foreach (var hold in holds.Where(hold => !hold.Taut && !hold.Slipping))
        {
            if (hold.WasTaut) storms.Log(entity, "{0} goes slack", hold.Tether.Name);
            (hold.WasTaut, hold.LoggedTension) = (false, 0);
        }
        if (engaged.Length == 0) return;

        var outward = engaged.Select(hold => Outward(hold.Tether)).ToArray();
        var (pullX, pullZ) = drift?.Pull() ?? default;
        var (waterX, waterZ) = forces!.Ride.WaterAcceleration;
        var accelerated = KilogramsAlong(waterX, waterZ);
        var shares = MooringLoad.Share(pullX + accelerated * waterX, pullZ + accelerated * waterZ, outward.Select(line => (line.X, line.Z)).ToArray());
        var (velocityX, velocityZ) = hull?.Velocity ?? default;
        for (var i = 0; i < engaged.Length; i++)
        {
            var hold = engaged[i];
            var level = hold.Tether.Level;
            if (level <= 0) continue;

            var (outX, outZ, _) = outward[i];
            var kilograms = KilogramsAlong(outX, outZ);
            var steady = shares[i] / level;
            var speed = (velocityX * outX + velocityZ * outZ) * level;
            var snatch = hold.TakeArrival() && !hold.Slipping;
            var tension = hold.Slipping ? steady : hold.Tether.Line.Tension(steady, speed, kilograms, snatch);
            hold.WasTaut = true;
            LogTension(hold, tension, steady, speed, kilograms, snatch);
            if (!Mechanic.MooringStrain.On(entity.World))
            {
                hold.Slipping = false;
                continue;
            }

            if (hold.Tether.Line.Parts(tension)) Part(hold, tension);
            else if (hold.Tether.Anchored && Anchor is { } anchor) Slip(hold, anchor, tension, kilograms);
        }
    }

    private void Slip(Hold hold, Anchor anchor, double tension, double kilograms)
    {
        var holding = Holding(anchor);
        hold.SlideDeceleration = kilograms > 0 ? holding * hold.Tether.Level / kilograms : 0;
        var slides = tension > holding;
        if (slides == hold.Slipping) return;

        hold.Slipping = slides;
        storms.Log(entity, slides ? "the anchor drags: the rode pulls {0:F0} N, it holds {1:F0} N on the bed" : "the anchor bites again: the rode pulls {0:F0} N, it holds {1:F0} N", tension, holding);
    }

    private void LogTension(Hold hold, double tension, double steady, double speed, double kilograms, bool snatch)
    {
        var line = hold.Tether.Line;
        if (snatch)
        {
            storms.Log(entity, "{0} comes taut at {1:F2} m/s with {2:F0} kg: {3:F0} N of the {4:F0} N it holds ({5:F1} m, {6:F0} N/m)",
                hold.Tether.Name, speed, kilograms, tension, line.BreakingNewtons, line.RestMetres, line.NewtonsPerMetre);
            return;
        }

        var now = entity.World.ElapsedMilliseconds;
        if (now - hold.LoggedAt < storms.Config.Drift.LogSeconds * 1000 || Math.Abs(tension - hold.LoggedTension) <= TensionLogShare * Math.Max(tension, hold.LoggedTension)) return;

        (hold.LoggedTension, hold.LoggedAt) = (tension, now);
        storms.Log(entity, "{0} holds her at {1:F0} N of the {2:F0} N it holds: its share of the water's pull, {3:F0} N along it", hold.Tether.Name, tension, line.BreakingNewtons, steady);
    }

    private double KilogramsAlong(double x, double z)
    {
        var length = Math.Sqrt(x * x + z * z);
        if (drift?.Drag is not { } drag) return hull?.Aboard.Total ?? 0;
        if (length <= 0) return drag.AlongKg;

        var (along, across) = HullShape.ToHull(x / length, z / length, entity.Pos.Yaw);
        return drag.AlongKg * along * along + drag.AcrossKg * across * across;
    }

    private void Part(Hold hold, double tension)
    {
        var tied = hold.Tether;
        (hold.WasTaut, hold.Slipping, hold.Taut) = (false, false, false);
        if (entity.Api is ICoreClientAPI)
        {
            if (!hold.Reported) storms.Report(new MooringReport { EntityId = entity.EntityId, Line = tied.Key, Newtons = tension });
            hold.Reported = true;
            return;
        }

        if (tied.Anchored && Anchor is { } anchor)
        {
            LoseAnchor(anchor);
            storms.Log(entity, "rode parted at {0:F0} N, the {1} holds {2:F0} N; anchor lost", tension, anchor.Rope, tied.Line.BreakingNewtons);
        }
        else if (cloths.GetClothSystem(tied.Key) is { } rope)
        {
            Rope.Part(rope);
            Snapped();
            storms.Log(entity, "stake rope parted at {0:F0} N, it holds {1:F0} N; the rope lies by the post", tension, tied.Line.BreakingNewtons);
        }
    }

    /// <summary>The anchor follows a vessel that pulls it along the bed: one it is sliding under, and one her rider's client moves past it.</summary>
    private void FollowDragged(bool steered)
    {
        if (Anchor is not { } anchor || holds.FirstOrDefault(hold => hold.Tether.Anchored) is not { } rode || !(steered || rode.Slipping)) return;

        var (outX, outZ, distance) = Outward(rode.Tether);
        var over = distance - rode.Tether.ReachMetres;
        if (over <= (rode.Slipping ? 0 : SlackMetres)) return;

        Anchor = anchor.DraggedBy(outX * over, outZ * over);
        followedMetres += over;
    }

    private void LogFollowed()
    {
        if (followedMetres < FollowedLogMetres) return;

        storms.Log(entity, "anchor dragged {0:F1} m along the bed", followedMetres);
        followedMetres = 0;
    }

    private (double X, double Z, double Distance) Outward(Tether tied)
    {
        var pos = entity.Pos;
        var (bowX, bowZ) = hull?.Shape.Bow(pos.Yaw) ?? default;
        var (dx, dz) = (pos.X + bowX - tied.X, pos.Z + bowZ - tied.Z);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        return distance > 0 ? (dx / distance, dz / distance, distance) : (0, 0, 0);
    }

    private void RecordMooring()
    {
        var tied = holds.Select(hold => hold.Tether).OrderByDescending(tether => tether.ReachMetres).FirstOrDefault();
        var (x, z, reach) = tied is { } widest ? (widest.X, widest.Z, widest.ReachMetres) : (0, 0, Unmoored);
        Tree.SetDouble("pointX", x);
        Tree.SetDouble("pointZ", z);
        Tree.SetDouble("reach", reach);
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

        var reachBlocks = (int)Math.Ceiling(aboard) + 1;
        var depth = forces!.WaterDepth(reachBlocks);
        var bed = forces.Seabed(reachBlocks);
        var pos = entity.Pos;
        var anchor = Storms.Anchor.LetGo(pos.X, Math.Floor(pos.Y) - depth, pos.Z, depth, aboard, Config.AnchorFullScope,
            attributes["rope"].AsString(""), attributes["kilograms"].AsDouble(), BedHold(bed));
        if (anchor == null)
        {
            if (!hanging) storms.Log(entity, "anchor hangs: {0:F0} m of rode aboard, deeper water below", aboard);
            hanging = true;
            return;
        }

        hanging = false;
        Anchor = anchor;
        storms.Log(entity, "anchor down in {0:F0} m on {1}, paid out {2:F0} of {3:F0} m, holds {4:F0} N",
            depth, bed.Code, anchor.Value.RodeMetres, aboard, Holding(anchor.Value));
    }

    private double BedHold(Block bed) =>
        bed.BlockMaterial is EnumBlockMaterial.Stone or EnumBlockMaterial.Ore or EnumBlockMaterial.Mantle ? Config.RockHold : Config.SandHold;

    private double Holding(Anchor anchor) => anchor.Holding(Depth(anchor), Config.AnchorStoneDensity, storms.Config.Waves.WaterDensity);

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
        storms.Log(entity, "rode taken in to {0:F0} m, holds {1:F0} N", aboard, Holding(Anchor.Value));
    }

    private ItemSlot? AnchorSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemAnchor);

    private ItemSlot? SpareRodeSlot() =>
        accessories?.Inventory?.FirstOrDefault(slot => slot.Itemstack?.Collectible is { } coil and not ItemAnchor && coil.Attributes?["rodeMetres"].Exists == true);

    private double SpareRodeMetres() =>
        SpareRodeSlot()?.Itemstack is { } coil ? coil.StackSize * coil.Collectible.Attributes["rodeMetres"].AsDouble() : 0;

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

    private double Depth(Anchor anchor) => Math.Max(0, entity.Pos.Y - anchor.Y);
}
