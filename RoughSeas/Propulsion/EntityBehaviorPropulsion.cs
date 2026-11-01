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
    private const double RadiansPerAngularVelocity = 30;
    private const long LogEveryMilliseconds = 5000;

    private PropulsionModSystem propulsion = null!;
    private VesselsModSystem vessels = null!;
    private JsonObject attributes = null!;
    private RowerStation station = null!;
    private MountStrokes strokes = null!;
    private Fatigue fatigue = null!;
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
        var tool = Tool.Of(rower.RightHandItemSlot?.Itemstack, propulsion.Config);
        var sailing = boat.WatchedAttributes.GetInt(SailKey) > 0;
        strokes.Show(boat, sailing ? null : strokes.KindOf(tool, station.HasOars));
        if (sailing || boat.SpeedMultiplier <= 0) return Idle(vanilla);
        if (physics != null) physics.WaterDragValue = 1;

        var propulsor = station.Propulsor(tool, out var refusal);
        var ahead = Math.Sign(vanilla.X);
        var turn = Math.Sign(vanilla.Y);
        var water = Drag(boat);
        var speed = VanillaSwim.Realized(boat.ForwardSpeed);
        var spin = boat.AngularVelocity * RadiansPerAngularVelocity;
        var stroking = propulsor != null && (ahead != 0 || turn != 0) && boat.Swimming;
        var metabolic = propulsion.Config.Rower.MetabolicWatts;
        var push = stroking ? propulsor!.Push(ahead, turn, speed, spin, metabolic * propulsor.Stroke.GrossEfficiency) : default;
        EffortWatts = stroking ? metabolic : 0;
        Stroke = stroking ? propulsor!.Stroke : null;
        Note(boat, rower, propulsor, refusal, push, water, speed, spin, ahead, turn);

        // Vanilla eases ForwardSpeed toward this target times SpeedMultiplier by a second's share each frame
        // (EntityBoat.updateBoatAngleAndMotion), so a target of speed plus acceleration steps her by acceleration × dt.
        var target = speed + (push.Newtons + water.Along(speed)) / water.AlongKg;
        var turning = (push.NewtonMetres + water.Turning(spin)) / water.TurningKgSquareMetres;
        return new Vec2d(VanillaSwim.Commanded(target) / boat.SpeedMultiplier,
            (boat.AngularVelocity + turning / RadiansPerAngularVelocity) / boat.SpeedMultiplier);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || EffortWatts <= 0 || Stroke == null) return;
        if (entity.GetBehavior<EntityBehaviorSeatable>()?.Controller is not EntityPlayer rower) return;

        using var _ = vessels.Budget.Measure();
        fatigue.Spend(entity, rower, EffortWatts, deltaTime, Stroke.Code);
    }

    private Vec2d Idle(Vec2d vanilla)
    {
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

    private void Note(EntityBoat boat, EntityAgent rower, Propulsor? propulsor, string? refusal, Push push, HullDrag water, double speed, double spin, int ahead, int turn)
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
            vessels.Log(boat, "{0} {1} at {2:F0} W, ahead {3} turn {4}: {5:F0} N and {6:F0} N·m at {7:F2} m/s, {8:F2} rad/s, against {9:F0} N of drag on {10:F0} kg at {11:F3} m draft",
                rower.GetName(), propulsor.Stroke.Code, EffortWatts * propulsor.Stroke.GrossEfficiency, ahead, turn, push.Newtons, push.NewtonMetres, speed, spin,
                -water.Along(speed), hull!.Aboard.Total, hull.Afloat.DraftMetres);
        }
    }
}
