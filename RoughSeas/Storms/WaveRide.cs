using System;
using RoughSeas.SeaAndWeather;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// One vessel carried by the waves on the side that moves her: the client that steers her, through the sea it draws,
/// or the server while nobody steers her, through the same waves drawn around her (<see cref="LocalSea"/>). Either
/// side answers with her own <see cref="HullDrive"/>, so a hull doesn't change physics when someone takes the helm.
/// </summary>
public sealed class WaveRide(StormsModSystem storms, SeaWeatherSystem sea, EntityBehaviorSeaForces forces)
{
    private const float LongestStepSeconds = 0.5f;
    private const double LogEverySeconds = 5;

    private readonly HullDrive drive = new(storms.Config.Drive, storms.Config.Response, storms.Config.Waves.WaterDensity);
    private LocalSea? local;
    private (double X, double Z) applied;
    private bool carried;
    private double carriedSeconds;
    private (double X, double Z) drifted;
    private double loggedSeconds;
    private double sway;
    private double water;

    public void Carry(EntityBoat boat, float dt)
    {
        var seconds = Math.Min(dt, LongestStepSeconds);
        if (!boat.Swimming || seconds <= 0)
        {
            Leave(boat, "out of the water");
            return;
        }
        var sea = SeaFor(boat, seconds);
        if (sea is not { Swell: true })
        {
            Leave(boat, sea != null ? "out of the swell" : boat.Api.Side == EnumAppSide.Client ? "not steered from here" : "steered from a client");
            return;
        }

        var pos = boat.Pos;
        var hull = Hull(boat);
        var yaw = pos.Yaw + (boat.Properties.Client?.Shape?.rotateY ?? 0) * GameMath.DEG2RAD;
        var (x, z) = drive.Step(sea, pos.X, pos.Z, yaw, hull, seconds);
        // under way vanilla rewrites the motion each step from the speed through the water; lying still it leaves it to the drag
        double? kept = boat.ForwardSpeed != 0 || boat.GetBehavior<EntityBehaviorPassivePhysics>() is not { } physics
            ? null
            : WaveCarry.Kept(physics.WaterDragValue, seconds);
        pos.Motion.X += WaveCarry.Carried(x, applied.X, kept);
        pos.Motion.Z += WaveCarry.Carried(z, applied.Z, kept);
        applied = (x, z);

        if (!carried)
        {
            carried = true;
            storms.Log(boat, "carried by the drawn waves {0}: {1:F1} m by {2:F1} m, {3:F2} m draft, {4:F0} kg", Side(boat),
                hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.MassKg);
        }
        Report(boat, hull, seconds, kept == null);
    }

    private IDrawnSea? SeaFor(EntityBoat boat, float seconds)
    {
        var controlled = boat.GetInterface<IMountable>();
        if (boat.Api is ICoreClientAPI capi)
        {
            return controlled?.Controller != null && controlled.Controller == capi.World.Player?.Entity ? storms.DrawnSea : null;
        }
        if (controlled?.IsBeingControlled() == true) return null;

        local ??= new LocalSea(storms, sea);
        local.Advance(boat.Pos.X, boat.Pos.Z, seconds, boat.World.Calendar.SpeedOfTime, boat.World.BlockAccessor.RegionSize);
        return local.Drawn;
    }

    private DrivenHull Hull(EntityBoat boat)
    {
        // the hull's own boxes, unturned, as both sides load them from the entity type
        var (alongX, alongZ) = Extent(boat);
        var length = Math.Max(alongX, alongZ);
        var beam = forces.BeamMetres > 0 ? forces.BeamMetres : Math.Min(alongX, alongZ);
        var buoyant = forces.Buoyant;
        return buoyant is { HasDraft: true }
            ? new DrivenHull(length, beam, buoyant.Afloat.DraftMetres, buoyant.DepthMetres, buoyant.Aboard.Total)
            : new DrivenHull(length, beam, 0, 0, boat.Properties.Weight);
    }

    private static readonly AccessTools.FieldRef<EntityBehaviorPassivePhysicsMultiBox, Cuboidf[]> Boxes =
        AccessTools.FieldRefAccess<EntityBehaviorPassivePhysicsMultiBox, Cuboidf[]>("OrigCollisionBoxes");

    private static (double X, double Z) Extent(EntityBoat boat)
    {
        if (boat.GetBehavior<EntityBehaviorPassivePhysicsMultiBox>() is not { } multibox || Boxes(multibox) is not { Length: > 0 } boxes)
        {
            return (boat.SelectionBox.XSize, boat.SelectionBox.ZSize);
        }
        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var box in boxes)
        {
            (minX, maxX) = (Math.Min(minX, box.X1), Math.Max(maxX, box.X2));
            (minZ, maxZ) = (Math.Min(minZ, box.Z1), Math.Max(maxZ, box.Z2));
        }
        return (maxX - minX, maxZ - minZ);
    }

    private void Leave(EntityBoat boat, string why)
    {
        drive.Settle();
        applied = default;
        if (!carried) return;
        carried = false;
        (carriedSeconds, drifted, loggedSeconds, sway, water) = (0, default, 0, 0, 0);
        storms.Log(boat, "no longer carried by the drawn waves {0}: {1}", Side(boat), why);
    }

    private void Report(EntityBoat boat, DrivenHull hull, double seconds, bool underWay)
    {
        carriedSeconds += seconds;
        loggedSeconds += seconds;
        var velocity = drive.Velocity;
        drifted = (drifted.X + velocity.X * seconds, drifted.Z + velocity.Z * seconds);
        sway = Math.Max(sway, Math.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z));
        water = Math.Max(water, Math.Sqrt(drive.Water.X * drive.Water.X + drive.Water.Z * drive.Water.Z));
        if (loggedSeconds < LogEverySeconds) return;

        var (driftX, driftZ) = (drifted.X / carriedSeconds, drifted.Z / carriedSeconds);
        storms.Log(boat, "waves {0}: water over her up to {1:F2} m/s, she sways up to {2:F2} m/s; drift {3:F3} {4:F3} m/s ({5:F3}) over {6:F0} s; "
            + "{7:F1} by {8:F1} m, {9:F2} m draft, {10:F0} kg, added mass {11:F2} along and {12:F2} across; {13}",
            Side(boat), water, sway, driftX, driftZ, Math.Sqrt(driftX * driftX + driftZ * driftZ), carriedSeconds,
            hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.MassKg, drive.Added.Surge, drive.Added.Sway,
            underWay ? "on top of her way" : "against the drag, lying still");
        (loggedSeconds, sway, water) = (0, 0, 0);
    }

    private static string Side(EntityBoat boat) => boat.Api.Side == EnumAppSide.Client ? "from this client, the one that steers her" : "on the server, nobody steering her";
}
