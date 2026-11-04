using System;
using System.Collections.Generic;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public class EntityBehaviorPropulsion(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:propulsion";

    private const string SailKey = "sailPosition";
    private const string PaddleSideKey = "roughseas:paddleSide";
    private const string PaddleChangingKey = "roughseas:paddleChanging";
    private const double RadiansPerAngularVelocity = 30;
    private const long LogEveryMilliseconds = 5000;

    private PropulsionModSystem propulsion = null!;
    private VesselsModSystem vessels = null!;
    private JsonObject attributes = null!;
    private RowerStation station = null!;
    private MountStrokes strokes = null!;
    private Fatigue fatigue = null!;
    private Paddler paddler = null!;
    private IHull? hull;
    private EntityBehaviorPassivePhysics? physics;
    private double vanillaWaterDrag;
    private (HullForm Form, double Draft, double Kg) dragFor;
    private HullDrag? drag;
    private string? logged;
    private long loggedAt;

    /// <summary>What her rower's body gives while he strokes, in watts: the effort a stamina reads.</summary>
    public double EffortWatts { get; private set; }

    public Stroke? Stroke { get; private set; }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        propulsion = entity.Api.ModLoader.GetModSystem<PropulsionModSystem>();
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        this.attributes = attributes;
        strokes = new MountStrokes(attributes["strokes"].AsObject<Dictionary<string, Dictionary<string, string>>>([]), vessels);
        fatigue = new Fatigue(propulsion.Config.Rower, vessels);
        paddler = new Paddler(propulsion.Config.Strokes.GetValueOrDefault(attributes["paddle"].AsString() ?? "")?.VeerDegrees ?? 0);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        physics = entity.GetBehavior<EntityBehaviorPassivePhysics>();
        vanillaWaterDrag = physics?.WaterDragValue ?? 0;
        var sea = entity.GetBehavior<EntityBehaviorSeaForces>();
        station = new RowerStation(attributes, propulsion.Config, propulsion.WaterDensity, blocks => sea?.WaterDepth(blocks) ?? 0);
    }

    public Vec2d Drive(EntityBoat boat, Vec2d vanilla)
    {
        if (boat.GetBehavior<EntityBehaviorSeatable>()?.Controller is not EntityAgent rower || hull == null) return Idle(vanilla);

        using var _ = vessels.Budget.Measure();
        var tool = Tool.Of(rower.RightHandItemSlot?.Itemstack, propulsion.Config, vessels.Materials.Density);
        var sailing = boat.WatchedAttributes.GetInt(SailKey) > 0;
        string? refusal = null;
        var propulsor = sailing || boat.SpeedMultiplier <= 0 ? null : station.Propulsor(tool, out refusal);
        var ahead = Math.Sign(vanilla.X);
        var turn = Math.Sign(vanilla.Y);
        var trying = propulsor != null && (ahead != 0 || turn != 0);
        var stroking = trying && boat.Swimming;
        var sided = strokes.Sided(tool);
        if (boat.World.Side == EnumAppSide.Server)
        {
            if (!trying || !sided) paddler.Rest();
            else if (stroking) Paddle(boat, rower, ahead, turn, propulsor!.Stroke);
            Publish();
        }
        var side = (Side)boat.WatchedAttributes.GetInt(PaddleSideKey, (int)Side.Starboard);
        var changing = sided && boat.WatchedAttributes.GetBool(PaddleChangingKey);
        strokes.Show(boat, sailing ? null : strokes.KindOf(tool, station.HasOars, side), changing, sided && ahead == 0 && turn != 0);
        if (sailing || boat.SpeedMultiplier <= 0) return Idle(vanilla);
        if (physics != null) physics.WaterDragValue = 1;

        var water = Drag(boat);
        var speed = VanillaSwim.Realized(boat.ForwardSpeed);
        var spin = boat.AngularVelocity * RadiansPerAngularVelocity;
        var metabolic = propulsion.Config.Rower.MetabolicWatts;
        var push = stroking && !changing ? propulsor!.Push(ahead, turn, side, speed, spin, metabolic * propulsor.Stroke.GrossEfficiency) : default;
        EffortWatts = stroking ? metabolic : 0;
        Stroke = stroking ? propulsor!.Stroke : null;
        Note(boat, rower, propulsor, refusal, push, water, speed, spin, ahead, turn, sided ? side : null);

        // Vanilla eases ForwardSpeed toward this target times SpeedMultiplier by a second's share each frame
        // (EntityBoat.updateBoatAngleAndMotion), so a target of speed plus acceleration steps her by acceleration × dt.
        var target = speed + (push.Newtons + water.Along(speed)) / water.AlongKg;
        var turning = (push.NewtonMetres + water.Turning(spin, speed)) / water.TurningKgSquareMetres;
        return new Vec2d(VanillaSwim.Commanded(target) / boat.SpeedMultiplier,
            (boat.AngularVelocity + turning / RadiansPerAngularVelocity) / boat.SpeedMultiplier);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server) return;

        using var _ = vessels.Budget.Measure();
        paddler.Lap(deltaTime);
        Publish();
        var rower = entity.GetBehavior<EntityBehaviorSeatable>()?.Controller as EntityPlayer;
        if (EffortWatts > 0 && Stroke != null && rower != null) fatigue.Spend(entity, rower, EffortWatts, deltaTime, Stroke.Code);
        else fatigue.Seated(entity, rower);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (entity.World.Side == EnumAppSide.Server) fatigue.Settle(entity);
    }

    private void Publish()
    {
        var watched = entity.WatchedAttributes;
        if (watched.GetInt(PaddleSideKey, (int)Side.Starboard) != (int)paddler.Side) watched.SetInt(PaddleSideKey, (int)paddler.Side);
        if (watched.GetBool(PaddleChangingKey) != paddler.Changing) watched.SetBool(PaddleChangingKey, paddler.Changing);
    }

    private void Paddle(EntityBoat boat, EntityAgent rower, int ahead, int turn, Stroke stroke)
    {
        var onThisSide = paddler.Strokes(stroke.StrokesPerMinute);
        if (!paddler.Paddle(ahead, turn, boat.Pos.Yaw, 60 / stroke.StrokesPerMinute)) return;
        vessels.Log(boat, "{0} changes his paddle to {1} after {2:F0} strokes, {3:F1}° off her course",
            rower.GetName(), paddler.Side.ToString().ToLowerInvariant(), onThisSide, paddler.OffCourseDegrees(boat.Pos.Yaw));
    }

    private Vec2d Idle(Vec2d vanilla)
    {
        paddler.Rest();
        if (physics != null) physics.WaterDragValue = vanillaWaterDrag;
        EffortWatts = 0;
        Stroke = null;
        return vanilla;
    }

    private HullDrag Drag(EntityBoat boat)
    {
        var key = (HullForm.From(attributes, vessels.Drag, hull!.Shape), hull.Afloat.DraftMetres, hull.Aboard.Total);
        if (drag != null && key == dragFor) return drag;

        if (key.Item1 != dragFor.Form)
        {
            vessels.Log(boat, "her drag on a hull {0:F2} by {1:F2} m, bow {2:F2}, side {3:F2}, fin {4:F2} m²",
                key.Item1.LengthMetres, key.Item1.BeamMetres, key.Item1.BowDrag, key.Item1.SideDrag, key.Item1.FinSquareMetres);
        }
        dragFor = key;
        return drag = new HullDrag(key.Item1, key.DraftMetres, key.Total, propulsion.WaterDensity, vessels.Drag);
    }

    private void Note(EntityBoat boat, EntityAgent rower, Propulsor? propulsor, string? refusal, Push push, HullDrag water, double speed, double spin, int ahead, int turn, Side? side)
    {
        var resting = ahead == 0 && turn == 0;
        var state = propulsor == null ? refusal : resting ? "resting" : $"{propulsor.Stroke.Code} {ahead} {turn}";
        var now = boat.World.ElapsedMilliseconds;
        if (state == logged && (propulsor == null || resting || now - loggedAt < LogEveryMilliseconds)) return;
        (logged, loggedAt) = (state, now);
        if (propulsor == null) vessels.Log(boat, "{0} makes no headway: {1}", rower.GetName(), refusal ?? "");
        else if (resting) vessels.Log(boat, "{0} rests the {1} at {2:F2} m/s", rower.GetName(), propulsor.Stroke.Code, speed);
        else
        {
            vessels.Log(boat, "{0} {1} at {2:F0} W, ahead {3} turn {4}: {5:F0} N and {6:F0} N·m at {7:F2} m/s, {8:F2} rad/s, against {9:F0} N of drag on {10:F0} kg at {11:F3} m draft{12}",
                rower.GetName(), propulsor.Stroke.Code, EffortWatts * propulsor.Stroke.GrossEfficiency, ahead, turn, push.Newtons, push.NewtonMetres, speed, spin,
                -water.Along(speed), hull!.Aboard.Total, hull.Afloat.DraftMetres,
                side == null ? "" : $", on her {side.ToString()!.ToLowerInvariant()} side");
        }
    }
}
