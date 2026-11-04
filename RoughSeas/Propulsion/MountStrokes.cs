using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public sealed class MountStrokes(Dictionary<string, Dictionary<string, string>> strokes, VesselsModSystem vessels)
{
    private static readonly string[] Keys = ["ready", "forwards", "backwards"];

    private bool shown;
    private string? kind;
    private Entity? stroking;

    public string? KindOf(Tool? tool, bool ownOars) =>
        tool != null && strokes.ContainsKey(tool.Kind) ? tool.Kind
        : ownOars && strokes.ContainsKey(RowerStation.OwnOars) ? RowerStation.OwnOars
        : null;

    public void Show(EntityBoat boat, Entity? rower, string? wanted, SeatPose? stance)
    {
        if (shown && wanted == kind && rower == stroking) return;

        var seats = boat.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        foreach (var key in Keys)
        {
            if (boat.MountAnimations.GetValueOrDefault(key) is { Length: > 0 } playing)
            {
                foreach (var seat in seats) seat.Passenger?.AnimManager?.StopAnimation(playing);
            }
            boat.MountAnimations[key] = wanted == null ? "" : strokes[wanted][key];
        }
        var poses = SeatPoses.Of(boat);
        foreach (var seat in seats) poses.Stand(seat, rower != null && seat.Passenger == rower ? stance : null);
        (shown, kind, stroking) = (true, wanted, rower);
        vessels.Log(boat, wanted == null ? "strokes stowed" : stance == null ? "strokes of the {0}" : "strokes of the {0}, standing", wanted ?? "");
    }
}
