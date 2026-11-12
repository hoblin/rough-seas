using System;
using System.Collections.Generic;
using System.Globalization;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

[ReliesOn("Vintagestory.GameContent.EntityBoat", "updateBoatAngleAndMotion", "ForwardSpeed eases by (motion·SpeedMultiplier − ForwardSpeed)·dt and the yaw turns by AngularVelocity·dt·30")]
[ReliesOn("Vintagestory.GameContent.EntityBoat", "SeatsToMotion", "reverses a paddled boat when its rider looks astern, so what the rower asks is read from the seat's controls")]
public class EntityBehaviorPropulsion(Entity entity) : EntityBehavior(entity), IHullWay
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
    private readonly PoleWarning warning = new();
    private IHull? hull;
    private LiveDrag? drag;
    private string? logged;
    private long loggedAt;

    /// <summary>What her rower's body gives while he strokes, in watts: the effort a stamina reads.</summary>
    public double EffortWatts { get; private set; }

    public Stroke? Stroke { get; private set; }

    public bool Driving { get; private set; }

    public HullDrag? WaterDrag => drag?.Now;

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
        if (hull != null) drag = new LiveDrag(entity, hull, attributes, vessels);
        var sea = entity.GetBehavior<EntityBehaviorSeaForces>();
        station = new RowerStation(attributes, propulsion.Config, propulsion.WaterDensity, (along, across, blocks) => sea?.WaterDepth(along, across, blocks) ?? 0,
            () => Mechanic.PoleDepth.On(entity.World));
    }

    /// <summary>
    /// Ahead and turning to port, each -1, 0 or 1, from the rower's keys, whichever way he looks.
    /// </summary>
    public static (int Ahead, int Turn) Asked(EntityControls? controls) => controls == null
        ? (0, 0)
        : (controls.Forward ? 1 : controls.Backward ? -1 : 0, controls.Left ? 1 : controls.Right ? -1 : 0);

    public double? PoleSlide(Entity rower, double poleMetres, double downward) =>
        Driving && entity.GetBehavior<EntityBehaviorSeatable>()?.Controller == rower ? station.Slide(poleMetres, downward, strokes.DrawnShare(rower)) : null;

    public static int Way(int ahead, int turn) => ahead < 0 || (ahead == 0 && turn < 0) ? -1 : 1;

    [ReliesOn("Vintagestory.GameContent.EntityBehaviorPlayerPhysics", "OnRenderFrame", "a mount's controller's client moves it in physics ticks of 1/60 s, where the swim drag clamp(10·|motion|·dtFactor − 0.02, 1, 1.25) never passes 1, so her ForwardSpeed is her way")]
    [ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "OnPhysicsTick", "splits a tick into 10 moves past 0.1 blocks of motion, which keeps the swim drag at 1 at 1/60 s")]
    [ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "MotionAndCollision", "the swim drag is clamp(10·|motion|·dtFactor − 0.02, 1, 1.25)")]
    public Vec2d Drive(EntityBoat boat, Vec2d vanilla)
    {
        if (boat.GetBehavior<EntityBehaviorSeatable>()?.Controller is not EntityAgent rower || hull == null)
        {
            strokes.Show(boat, null, null, false, false, null, null);
            return Idle(vanilla);
        }

        using var _ = vessels.Budget.Measure();
        var tool = Tool.Of(rower.RightHandItemSlot?.Itemstack, propulsion.Config, vessels.Materials.Density);
        var idle = boat.WatchedAttributes.GetInt(SailKey) > 0 || boat.SpeedMultiplier <= 0;
        string? refusal = null;
        var (ahead, turn) = Asked(rower.MountedOn?.Controls);
        var propulsor = idle ? null : station.Propulsor(tool, Way(ahead, turn), out refusal);
        var asking = ahead != 0 || turn != 0;
        warning.Tell(rower, propulsor == null ? tool as Pole : null, asking && !idle);
        var trying = propulsor != null && asking;
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
        strokes.Show(boat, rower, idle ? null : strokes.KindOf(tool, station.HasOars, side), changing, sided && ahead == 0 && turn != 0,
            idle ? null : station.Stance(tool?.Kind), tool?.Kind);
        if (idle || drag?.Now is not { } water) return Idle(vanilla);
        Driving = true;

        var speed = boat.ForwardSpeed * SeaAndWeather.WaterDrag.TicksPerSecond;
        var spin = boat.AngularVelocity * RadiansPerAngularVelocity;
        var metabolic = propulsion.Config.Rower.MetabolicWatts;
        var push = stroking && !changing
            ? propulsor!.Push(ahead, turn, side, speed, spin, metabolic * propulsor.Stroke.GrossEfficiency, strokes.DrawnShare(rower))
            : default;
        strokes.Pace(rower, stroking ? propulsor!.Stroke.Pace(propulsor.StrokeSpeed(ahead, turn, speed, spin), strokes.DrawnShare(rower)) : 1);
        EffortWatts = stroking ? metabolic : 0;
        Stroke = stroking ? propulsor!.Stroke : null;
        Note(boat, rower, propulsor, refusal, push, water, speed, spin, ahead, turn, sided ? side : null);

        // Vanilla eases ForwardSpeed toward this target times SpeedMultiplier by a second's share each frame
        // (EntityBoat.updateBoatAngleAndMotion), so a target of speed plus acceleration steps her by acceleration × dt.
        var target = speed + (push.Newtons + water.Along(speed)) / water.AlongKg;
        var turning = (push.NewtonMetres + water.Turning(spin, speed)) / water.TurningKgSquareMetres;
        return new Vec2d(target / SeaAndWeather.WaterDrag.TicksPerSecond / boat.SpeedMultiplier,
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
        vessels.Log(boat, "{0} changes his paddle to {1} after {2:F1} strokes, {3:F1}° off her course, once the stroke he is in is done",
            rower.GetName(), (paddler.Coming ?? paddler.Side).ToString().ToLowerInvariant(), onThisSide, paddler.OffCourseDegrees(boat.Pos.Yaw));
    }

    private Vec2d Idle(Vec2d vanilla)
    {
        paddler.Rest();
        Driving = false;
        EffortWatts = 0;
        Stroke = null;
        return vanilla;
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
            vessels.Log(boat, "{0} {1} at {2:F0} W, ahead {3} turn {4}: {5:F0} N and {6:F0} N·m at {7:F2} m/s, {8:F2} rad/s, against {9:F0} N of drag on {10:F0} kg at {11:F3} m draft{12}{13}",
                rower.GetName(), propulsor.Stroke.Code, EffortWatts * propulsor.Stroke.GrossEfficiency, ahead, turn, push.Newtons, push.NewtonMetres, speed, spin,
                -water.Along(speed), hull!.Aboard.Total, hull.Afloat.DraftMetres,
                side == null ? "" : $", on her {side.ToString()!.ToLowerInvariant()} side",
                station.BedMetres is { } bed ? string.Format(CultureInfo.InvariantCulture, ", the pole on a bed {0:F2} m down", bed) : "");
        }
    }
}
