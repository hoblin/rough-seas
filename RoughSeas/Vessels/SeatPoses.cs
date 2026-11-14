using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public readonly record struct SeatPose(Vec3f? Offset, string Animation, float? EyeHeight = null, Vec3f? Rotation = null);

/// <summary>
/// The one writer of a vessel's seat configs: each seat shows a swamped hull's hold if it has one, else the stance its
/// rider's tool asks for, else the seat as built. The pose is the seat's own animation, which vanilla stops when its
/// rider leaves, so a rider who lets go of a swamped hull does not keep holding on.
/// </summary>
public sealed class SeatPoses
{
    private static readonly ConditionalWeakTable<Entity, SeatPoses> OfVessel = new();

    private readonly Entity boat;
    private readonly Dictionary<string, SeatPose> built = new();
    private readonly Dictionary<string, SeatPose> stances = new();
    private readonly Dictionary<string, SeatPose> holds = new();

    private SeatPoses(Entity boat) => this.boat = boat;

    public static SeatPoses Of(Entity boat) => OfVessel.GetValue(boat, vessel => new SeatPoses(vessel));

    public void Stand(IMountableSeat seat, SeatPose? stance) => Set(stances, seat, stance);

    public void Hold(IMountableSeat seat, SeatPose? hold) => Set(holds, seat, hold);

    private void Set(Dictionary<string, SeatPose> layer, IMountableSeat seat, SeatPose? pose)
    {
        var config = seat.Config;
        if (config == null) return;

        if (!built.TryGetValue(seat.SeatId, out var asBuilt))
        {
            built[seat.SeatId] = asBuilt = new SeatPose(config.RiderOffset, config.Animation ?? Boats.Of(boat)!.MountAnimations(boat)["idle"], config.EyeHeight, config.MountRotation);
        }
        if (pose is { } given) layer[seat.SeatId] = given;
        else layer.Remove(seat.SeatId);
        var shown = holds.TryGetValue(seat.SeatId, out var hold) ? hold : stances.TryGetValue(seat.SeatId, out var stance) ? stance : asBuilt;
        Show(seat, shown, shown.EyeHeight ?? asBuilt.EyeHeight ?? config.EyeHeight, shown.Rotation ?? asBuilt.Rotation!, config.Animation ?? asBuilt.Animation);
    }

    private static void Show(IMountableSeat seat, SeatPose pose, float eyeHeight, Vec3f rotation, string shown)
    {
        var config = seat.Config;
        var turned = config.MountRotation.Y != rotation.Y;
        config.RiderOffset = pose.Offset;
        config.Animation = pose.Animation;
        config.EyeHeight = eyeHeight;
        config.MountRotation = rotation;
        if (turned && seat.Passenger?.Api is ICoreClientAPI capi && capi.World.Player.Entity == seat.Passenger)
            capi.Input.MouseYaw = seat.SeatPosition.Yaw;
        if (shown == pose.Animation || seat.Passenger?.AnimManager is not { } animations) return;

        animations.StopAnimation(shown);
        animations.StartAnimation(pose.Animation);
    }
}
