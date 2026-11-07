using System;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// One vessel on the drawn sea: her size from her hull, her one heave, and the water that carries her on the side that
/// moves her: the client that steers her, through the sea it draws, or the server while nobody steers her, through the
/// same sea built there (<see cref="RegionSwells"/>). So a hull doesn't change physics when someone takes the helm.
/// </summary>
public sealed class WaveRide(StormsModSystem storms, SeaWeatherSystem sea, EntityBehaviorSeaForces forces)
{
    private const float LongestStepSeconds = 0.5f;
    private const double LogEverySeconds = 5;
    private const double LogWithinBlocks = 64;

    private readonly HullRider rider = new(storms.Config.Response);
    private (double X, double Z) applied;
    private bool carried;
    private double carriedSeconds;
    private (double X, double Z) drifted;
    private (double X, double Z) carriedFrom;
    private double loggedSeconds;
    private double sway;

    /// <summary>The pose she takes at <paramref name="seconds"/>, on a clock of the caller's: the one her model is drawn at, and the heave the water is read at.</summary>
    public SeaPose Follow(EntityBoat boat, IDrawnSea drawn, double seconds) =>
        rider.Follow(drawn, boat.Pos.X, boat.Pos.Z, Yaw(boat), Hull(boat), seconds);

    /// <summary>She has left the water: she comes to rest and is next met afresh.</summary>
    public void Settle() => rider.Settle();

    /// <summary>Adds what the waves move her by this step to her motion, after vanilla's steering has set it.</summary>
    public void Carry(EntityBoat boat, float dt)
    {
        using var _ = storms.Budget.Measure();
        var seconds = Math.Min(dt, LongestStepSeconds);
        if (!boat.Swimming || seconds <= 0)
        {
            Leave(boat, "out of the water");
            return;
        }
        var drawn = SeaFor(boat);
        if (drawn == null)
        {
            Leave(boat, boat.Api.Side == EnumAppSide.Client ? "not steered from here" : "steered from a client");
            return;
        }
        var pos = boat.Pos;
        if (!drawn.SwellAt(pos.X, pos.Z))
        {
            Leave(boat, "out of the swell");
            return;
        }

        var hull = Hull(boat);
        var yaw = Yaw(boat);
        // the client's frames have followed her already; on the server these ticks are the only frames she has
        if (boat.Api.Side == EnumAppSide.Server) rider.Follow(drawn, pos.X, pos.Z, yaw, hull, boat.World.ElapsedMilliseconds / 1000.0);
        var rate = sea.Clock.SecondsIn(seconds, boat.World.Calendar.SpeedOfTime) / seconds;
        var (x, z) = WaveCarry.OnTheClock(rider.Water(drawn, pos.X, pos.Z, yaw, hull), rate);

        // under way vanilla rewrites the motion each step from the speed through the water, so nothing of the last is kept;
        // lying still it leaves it to the drag, all of it while a rower takes that drag off
        var physics = boat.ForwardSpeed == 0 ? boat.GetBehavior<EntityBehaviorPassivePhysics>() : null;
        if (physics != null && forces.Driven)
        {
            // no way on and the drag taken off: nothing would ever take off what her motion holds, so it is written outright, as vanilla writes her way
            pos.Motion.X = x / WaterDrag.TicksPerSecond;
            pos.Motion.Z = z / WaterDrag.TicksPerSecond;
        }
        else
        {
            double? kept = physics != null ? WaterDrag.Kept(physics.WaterDragValue, seconds) : null;
            // vanilla's swimming physics divides her whole motion when it is fast, the waves' part with it
            var swimDrag = physics != null ? 1 : VesselDraught.SwimDrag(boat.ForwardSpeed, GlobalConstants.PhysicsFrameTime * WaterDrag.TicksPerSecond);
            pos.Motion.X += WaterDrag.Carried(x, applied.X, kept ?? 0) * swimDrag;
            pos.Motion.Z += WaterDrag.Carried(z, applied.Z, kept ?? 0) * swimDrag;
        }
        applied = (x, z);

        if (!carried)
        {
            carried = true;
            carriedFrom = (pos.X, pos.Z);
            storms.Log(boat, "carried by the drawn waves {0}: {1:F1} m by {2:F1} m, {3:F2} m draft, {4:F0} kg", Side(boat),
                hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.Kg);
        }
        Report(boat, hull, x, z, seconds, physics != null);
    }

    private IDrawnSea? SeaFor(EntityBoat boat)
    {
        var mountable = boat.GetInterface<IMountable>();
        if (boat.Api is ICoreClientAPI capi)
        {
            return mountable?.Controller != null && mountable.Controller == capi.World.Player?.Entity ? storms.DrawnSea : null;
        }
        return mountable?.IsBeingControlled() == true ? null : storms.Swells?.At(boat.Pos.X, boat.Pos.Z);
    }

    private RiddenHull Hull(EntityBoat boat)
    {
        double side = Math.Max(boat.CollisionBox.XSize, boat.CollisionBox.ZSize);
        return RiddenHull.Of(forces.LengthMetres ?? side, forces.BeamMetres > 0 ? forces.BeamMetres : side, forces.Buoyant);
    }

    private static double Yaw(EntityBoat boat) => boat.Pos.Yaw + (boat.Properties.Client?.Shape?.rotateY ?? 0) * GameMath.DEG2RAD;

    private void Leave(EntityBoat boat, string why)
    {
        applied = default;
        if (!carried) return;
        carried = false;
        (carriedSeconds, drifted, loggedSeconds, sway) = (0, default, 0, 0);
        storms.Log(boat, "no longer carried by the drawn waves {0}: {1}", Side(boat), why);
    }

    private void Report(EntityBoat boat, RiddenHull hull, double x, double z, double seconds, bool lyingStill)
    {
        carriedSeconds += seconds;
        loggedSeconds += seconds;
        drifted = (drifted.X + x * seconds, drifted.Z + z * seconds);
        sway = Math.Max(sway, Math.Sqrt(x * x + z * z));
        if (loggedSeconds < LogEverySeconds) return;

        var pos = boat.Pos;
        if (boat.World.NearestPlayer(pos.X, pos.Y, pos.Z)?.Entity?.Pos.DistanceTo(pos) <= LogWithinBlocks)
        {
            var (driftX, driftZ) = (drifted.X / carriedSeconds, drifted.Z / carriedSeconds);
            var (madeX, madeZ) = ((pos.X - carriedFrom.X) / carriedSeconds, (pos.Z - carriedFrom.Z) / carriedSeconds);
            storms.Log(boat, "waves {0}: she sways up to {1:F2} m/s, the waves' drift {2:F3} {3:F3} m/s, over the ground {4:F3} {5:F3} m/s over {6:F0} s; {7:F1} by {8:F1} m, {9:F2} m draft, lifted {10:F2} m; {11}",
                Side(boat), sway, driftX, driftZ, madeX, madeZ, carriedSeconds, hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, rider.Lift,
                lyingStill ? "against the drag, lying still" : "on top of her way");
        }
        (loggedSeconds, sway) = (0, 0);
    }

    private static string Side(EntityBoat boat) => boat.Api.Side == EnumAppSide.Client ? "from this client, the one that steers her" : "on the server, nobody steering her";
}
