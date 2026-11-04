using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public readonly record struct SeatPose(Vec3f? Offset, string Animation, float? EyeHeight = null);

/// <summary>
/// The one writer of a vessel's seat configs: each seat shows a swamped hull's hold if it has one, else the stance its
/// rider's tool asks for, else the seat as built. The pose is the seat's own animation, which vanilla stops when its
/// rider leaves, so a rider who lets go of a swamped hull does not keep holding on.
/// </summary>
public sealed class SeatPoses
{
    private static readonly ConditionalWeakTable<EntityBoat, SeatPoses> OfVessel = new();

    private readonly EntityBoat boat;
    private readonly Dictionary<string, SeatPose> built = new();
    private readonly Dictionary<string, SeatPose> stances = new();
    private readonly Dictionary<string, SeatPose> holds = new();

    private SeatPoses(EntityBoat boat) => this.boat = boat;

    public static SeatPoses Of(EntityBoat boat) => OfVessel.GetValue(boat, vessel => new SeatPoses(vessel));

    public void Stand(IMountableSeat seat, SeatPose? stance) => Set(stances, seat, stance);

    public void Hold(IMountableSeat seat, SeatPose? hold) => Set(holds, seat, hold);

    private void Set(Dictionary<string, SeatPose> layer, IMountableSeat seat, SeatPose? pose)
    {
        var config = seat.Config;
        if (config == null) return;

        if (!built.TryGetValue(seat.SeatId, out var asBuilt))
        {
            built[seat.SeatId] = asBuilt = new SeatPose(config.RiderOffset, config.Animation ?? boat.MountAnimations["idle"], config.EyeHeight);
        }
        if (pose is { } given) layer[seat.SeatId] = given;
        else layer.Remove(seat.SeatId);
        var shown = holds.TryGetValue(seat.SeatId, out var hold) ? hold : stances.TryGetValue(seat.SeatId, out var stance) ? stance : asBuilt;
        Show(seat, shown, shown.EyeHeight ?? asBuilt.EyeHeight ?? config.EyeHeight);
    }

    private static void Show(IMountableSeat seat, SeatPose pose, float eyeHeight)
    {
        var config = seat.Config;
        var shown = config.Animation;
        config.RiderOffset = pose.Offset;
        config.Animation = pose.Animation;
        config.EyeHeight = eyeHeight;
        if (shown == pose.Animation || seat.Passenger?.AnimManager is not { } animations) return;

        if (shown != null) animations.StopAnimation(shown);
        animations.StartAnimation(pose.Animation);
    }
}
