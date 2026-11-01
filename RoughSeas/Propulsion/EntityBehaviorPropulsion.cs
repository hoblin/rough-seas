using System;
using System.Collections.Generic;
using System.Linq;
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
    private const double JoulesPerKilojoule = 1000;
    private const long LogEveryMilliseconds = 5000;
    private const double SpentLogSeconds = 10;
    private static readonly string[] StrokeKeys = ["ready", "forwards", "backwards"];

    private PropulsionModSystem propulsion = null!;
    private VesselsModSystem vessels = null!;
    private JsonObject form = null!;
    private string? paddleStroke;
    private OarsConfig? oars;
    private double sweepMetres;
    private double handsAboveWaterMetres;
    private Dictionary<string, Dictionary<string, string>> strokes = [];
    private IHull? hull;
    private EntityBehaviorSeaForces? sea;
    private EntityBehaviorPassivePhysics? physics;
    private double vanillaWaterDrag;
    private string? shownKind;
    private string? logged;
    private long loggedAt;
    private HullForm? loggedForm;
    private double spentSeconds;
    private double spentSatiety;

    /// <summary>What her rower's body gives while he strokes, in watts: the effort a stamina reads.</summary>
    public double EffortWatts { get; private set; }

    public Stroke? Stroke { get; private set; }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        propulsion = entity.Api.ModLoader.GetModSystem<PropulsionModSystem>();
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        form = attributes;
        paddleStroke = attributes["paddle"].AsString();
        oars = attributes["oars"].AsObject<OarsConfig?>(null);
        sweepMetres = attributes["sweepMetres"].AsDouble();
        handsAboveWaterMetres = attributes["handsAboveWaterMetres"].AsDouble();
        strokes = attributes["strokes"].AsObject<Dictionary<string, Dictionary<string, string>>>([]);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        sea = entity.GetBehavior<EntityBehaviorSeaForces>();
        physics = entity.GetBehavior<EntityBehaviorPassivePhysics>();
        vanillaWaterDrag = physics?.WaterDragValue ?? 0;
    }

    public Vec2d Drive(EntityBoat boat, Vec2d vanilla)
    {
        if (boat.GetBehavior<EntityBehaviorSeatable>()?.Controller is not EntityAgent rower || hull == null) return Idle(vanilla);

        var sailing = boat.WatchedAttributes.GetInt(SailKey) > 0;
        Show(boat, sailing ? null : KindIn(rower));
        if (sailing || boat.SpeedMultiplier <= 0) return Idle(vanilla);
        if (physics != null) physics.WaterDragValue = 1;

        var propulsor = PropulsorFor(rower, out var refusal);
        var ahead = Math.Sign(vanilla.X);
        var turn = Math.Sign(vanilla.Y);
        var shape = HullForm.From(form, vessels.Drag, hull.Shape);
        if (shape != loggedForm)
        {
            loggedForm = shape;
            vessels.Log(boat, "her drag on a hull {0:F2} by {1:F2} m, bow {2:F2}, side {3:F2}, fin {4:F2} m²",
                shape.LengthMetres, shape.BeamMetres, shape.BowDrag, shape.SideDrag, shape.FinSquareMetres);
        }
        var drag = new HullDrag(shape, hull.Afloat.DraftMetres, hull.Aboard.Total, propulsion.WaterDensity, vessels.Drag);
        var speed = VanillaSwim.Realized(boat.ForwardSpeed);
        var spin = boat.AngularVelocity * RadiansPerAngularVelocity;
        var stroking = propulsor != null && (ahead != 0 || turn != 0);
        var metabolic = propulsion.Config.Rower.MetabolicWatts;
        var push = stroking ? propulsor!.Push(ahead, turn, speed, spin, metabolic * propulsor.Stroke.GrossEfficiency) : default;
        EffortWatts = stroking ? metabolic : 0;
        Stroke = stroking ? propulsor!.Stroke : null;
        Note(boat, rower, propulsor, refusal, push, drag, speed, spin, ahead, turn);

        var gaining = (push.Newtons + drag.Along(speed)) / drag.AlongKg;
        var turning = (push.NewtonMetres + drag.Turning(spin)) / drag.TurningKgSquareMetres;
        return new Vec2d(VanillaSwim.Commanded(speed + gaining) / boat.SpeedMultiplier,
            (boat.AngularVelocity + turning / RadiansPerAngularVelocity) / boat.SpeedMultiplier);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || EffortWatts <= 0 || Stroke == null) return;
        if (entity.GetBehavior<EntityBehaviorSeatable>()?.Controller is not EntityPlayer rower) return;

        var calendar = entity.World.Calendar;
        var kilojoules = EffortWatts * deltaTime / JoulesPerKilojoule;
        var pace = calendar.SpeedOfTime * calendar.CalendarSpeedMul / propulsion.Config.Rower.VanillaPace;
        var satiety = kilojoules * propulsion.Config.Rower.SatietyPerKilojoule * pace;
        rower.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation((float)satiety);
        (spentSeconds, spentSatiety) = (spentSeconds + deltaTime, spentSatiety + satiety);
        if (spentSeconds < SpentLogSeconds) return;
        vessels.Log(entity, "{0} spent {1:F1} satiety on {2:F0} s of {3}", rower.GetName(), spentSatiety, spentSeconds, Stroke.Code);
        (spentSeconds, spentSatiety) = (0, 0);
    }

    private Vec2d Idle(Vec2d vanilla)
    {
        if (physics != null) physics.WaterDragValue = vanillaWaterDrag;
        EffortWatts = 0;
        Stroke = null;
        return vanilla;
    }

    private Propulsor? PropulsorFor(EntityAgent rower, out string? refusal)
    {
        refusal = null;
        var tool = Tool.Of(rower.RightHandItemSlot?.Itemstack, propulsion.Config);
        switch (tool)
        {
            case Pole pole:
                var reach = pole.Reach(handsAboveWaterMetres);
                var depth = sea?.WaterDepth((int)Math.Ceiling(reach) + 1) ?? 0;
                if (depth > reach)
                {
                    refusal = $"a {pole.Metres:F0} m pole reaches {reach:F1} m, the water is deeper";
                    return null;
                }
                return new HeldTool(new PoleStroke(Pole.Code, propulsion.Config.Strokes[Pole.Code], pole.Kilograms, depth, handsAboveWaterMetres), sweepMetres);
            case Paddle paddle when paddleStroke != null:
                return new HeldTool(new BladeStroke(paddleStroke, propulsion.Config.Strokes[paddleStroke], paddle.BladeSquareMetres, propulsion.WaterDensity), sweepMetres);
            case Paddle:
                refusal = "a paddle doesn't serve her";
                return null;
        }
        if (oars != null) return new TholeOars(new BladeStroke(oars.Stroke, propulsion.Config.Strokes[oars.Stroke], oars.BladeSquareMetres, propulsion.WaterDensity), oars.SpanMetres);
        refusal = "nothing in hand that moves her";
        return null;
    }

    private string? KindIn(EntityAgent rower) =>
        Tool.Of(rower.RightHandItemSlot?.Itemstack, propulsion.Config) is { Kind: var kind } && strokes.ContainsKey(kind) ? kind
        : oars != null ? TholeOar.Code
        : null;

    private void Show(EntityBoat boat, string? kind)
    {
        if (kind == shownKind || (kind != null && !strokes.ContainsKey(kind))) return;

        var seats = boat.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        foreach (var key in StrokeKeys)
        {
            if (boat.MountAnimations.GetValueOrDefault(key) is { Length: > 0 } shown)
            {
                foreach (var seat in seats) seat.Passenger?.AnimManager?.StopAnimation(shown);
            }
            boat.MountAnimations[key] = kind == null ? "" : strokes[kind][key];
        }
        shownKind = kind;
        vessels.Log(boat, kind == null ? "strokes stowed" : "strokes of the {0}", kind ?? "");
    }

    private void Note(EntityBoat boat, EntityAgent rower, Propulsor? propulsor, string? refusal, Push push, HullDrag drag, double speed, double spin, int ahead, int turn)
    {
        var state = propulsor == null ? refusal : ahead == 0 && turn == 0 ? "resting" : $"{propulsor.Stroke.Code} {ahead} {turn}";
        var now = boat.World.ElapsedMilliseconds;
        if (state == logged && now - loggedAt < LogEveryMilliseconds) return;
        (logged, loggedAt) = (state, now);
        if (propulsor == null) vessels.Log(boat, "{0} makes no headway: {1}", rower.GetName(), refusal ?? "");
        else if (ahead == 0 && turn == 0) vessels.Log(boat, "{0} rests the {1} at {2:F2} m/s", rower.GetName(), propulsor.Stroke.Code, speed);
        else
        {
            vessels.Log(boat, "{0} {1} at {2:F0} W, ahead {3} turn {4}: {5:F0} N and {6:F0} N·m at {7:F2} m/s, {8:F2} rad/s, against {9:F0} N of drag on {10:F0} kg at {11:F3} m draft",
                rower.GetName(), propulsor.Stroke.Code, EffortWatts * propulsor.Stroke.GrossEfficiency, ahead, turn, push.Newtons, push.NewtonMetres, speed, spin,
                -drag.Along(speed), hull!.Aboard.Total, hull.Afloat.DraftMetres);
        }
    }
}

public class OarsConfig
{
    public string Stroke = "";
    public double BladeSquareMetres;
    public double SpanMetres;
}
