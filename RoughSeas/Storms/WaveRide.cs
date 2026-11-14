using System;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
    private bool carried;
    private double carriedSeconds;
    private (double X, double Z) drifted;
    private (double X, double Z) carriedFrom;
    private double loggedSeconds;
    private double sway;
    private double shear;

    /// <summary>The pose she takes at <paramref name="seconds"/>, on a clock of the caller's: the one her model is drawn at, and the heave the water is read at.</summary>
    public SeaPose Follow(Entity boat, IDrawnSea drawn, double seconds) =>
        rider.Follow(drawn, boat.Pos.X, boat.Pos.Z, Yaw(boat), Hull(boat), seconds);

    /// <summary>She has left the water: she comes to rest and is next met afresh.</summary>
    public void Settle() => rider.Settle();

    /// <summary>The waves' water under her this tick, on the tick's clock, on the side that moves her; none elsewhere or out of the swell.</summary>
    public WaterOnHull Water(Entity boat, float dt)
    {
        var seconds = Math.Min(dt, LongestStepSeconds);
        if (!boat.Swimming || seconds <= 0) return Leave(boat, "out of the water");
        if (forces.Buoyant?.Afloat.Sunk == true) return Leave(boat, "sunk");
        var drawn = SeaFor(boat);
        if (drawn == null) return Leave(boat, boat.Api.Side == EnumAppSide.Client ? "not steered from here" : "steered from a client");
        var pos = boat.Pos;
        if (!drawn.SwellAt(pos.X, pos.Z)) return Leave(boat, "out of the swell");

        var hull = Hull(boat);
        var yaw = Yaw(boat);
        // the client's frames have followed her already; on the server these ticks are the only frames she has
        if (boat.Api.Side == EnumAppSide.Server) rider.Follow(drawn, pos.X, pos.Z, yaw, hull, boat.World.ElapsedMilliseconds / 1000.0);
        var rate = sea.Clock.SecondsIn(seconds, boat.World.Calendar.SpeedOfTime) / seconds;
        var (x, z) = OnTheClock(rider.Water(drawn, pos.X, pos.Z, yaw, hull), rate);
        var gradient = rider.Gradient(drawn, pos.X, pos.Z, yaw, hull).Scaled(rate);

        if (!carried)
        {
            carried = true;
            carriedFrom = (pos.X, pos.Z);
            storms.Log(boat, "moved by the drawn waves {0}, with their water at {1:F2} m/s: {2:F1} m by {3:F1} m, {4:F2} m draft, {5:F0} kg", Side(boat),
                Math.Sqrt(x * x + z * z), hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.Kg);
        }
        Report(boat, hull, x, z, gradient, seconds);
        return new WaterOnHull(0, 0, x, z, gradient);
    }

    /// <summary>
    /// A velocity in metres a second of the waves' clock as metres a second of the tick's, when the waves' clock runs
    /// <paramref name="wavesPerTickSecond"/> of its seconds to the tick's. One wave then moves a hull as far at any speed
    /// of time.
    /// </summary>
    internal static (double X, double Z) OnTheClock((double X, double Z) velocity, double wavesPerTickSecond) =>
        (velocity.X * wavesPerTickSecond, velocity.Z * wavesPerTickSecond);

    private IDrawnSea? SeaFor(Entity boat)
    {
        var mountable = boat.GetInterface<IMountable>();
        if (boat.Api is ICoreClientAPI capi)
        {
            return mountable?.Controller != null && mountable.Controller == capi.World.Player?.Entity ? storms.DrawnSea : null;
        }
        return mountable?.IsBeingControlled() == true ? null : storms.Swells?.At(boat.Pos.X, boat.Pos.Z);
    }

    private RiddenHull Hull(Entity boat)
    {
        double side = Math.Max(boat.CollisionBox.XSize, boat.CollisionBox.ZSize);
        return RiddenHull.Of(forces.LengthMetres ?? side, forces.BeamMetres > 0 ? forces.BeamMetres : side, forces.Buoyant);
    }

    private static double Yaw(Entity boat) => boat.Pos.Yaw + (boat.Properties.Client?.Shape?.rotateY ?? 0) * GameMath.DEG2RAD;

    private WaterOnHull Leave(Entity boat, string why)
    {
        if (boat.Api.Side == EnumAppSide.Server) rider.Settle();
        if (!carried) return default;
        carried = false;
        (carriedSeconds, drifted, loggedSeconds, sway, shear) = (0, default, 0, 0, 0);
        storms.Log(boat, "no longer moved by the drawn waves {0}: {1}", Side(boat), why);
        return default;
    }

    private void Report(Entity boat, RiddenHull hull, double x, double z, FlowGradient gradient, double seconds)
    {
        carriedSeconds += seconds;
        loggedSeconds += seconds;
        drifted = (drifted.X + x * seconds, drifted.Z + z * seconds);
        sway = Math.Max(sway, Math.Sqrt(x * x + z * z));
        shear = Math.Max(shear, Math.Max(Math.Abs(gradient.XX), Math.Abs(gradient.ZZ)));
        if (loggedSeconds < LogEverySeconds) return;

        var pos = boat.Pos;
        if (boat.World.NearestPlayer(pos.X, pos.Y, pos.Z)?.Entity?.Pos.DistanceTo(pos) <= LogWithinBlocks)
        {
            var (driftX, driftZ) = (drifted.X / carriedSeconds, drifted.Z / carriedSeconds);
            var (madeX, madeZ) = ((pos.X - carriedFrom.X) / carriedSeconds, (pos.Z - carriedFrom.Z) / carriedSeconds);
            storms.Log(boat, "waves {0}: the water under her sways up to {1:F2} m/s, changing up to {2:F3} m/s a metre, its mean {3:F3} {4:F3} m/s where she was, she made {5:F3} {6:F3} m/s over the ground over {7:F0} s; {8:F1} by {9:F1} m, {10:F2} m draft, lifted {11:F2} m",
                Side(boat), sway, shear, driftX, driftZ, madeX, madeZ, carriedSeconds, hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, rider.Lift);
        }
        (loggedSeconds, sway, shear) = (0, 0, 0);
    }

    private static string Side(Entity boat) => boat.Api.Side == EnumAppSide.Client ? "from this client, the one that steers her" : "on the server, nobody steering her";
}
