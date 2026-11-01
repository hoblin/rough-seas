using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public sealed class MountStrokes(Dictionary<string, Dictionary<string, string>> strokes, VesselsModSystem vessels)
{
    private static readonly string[] Keys = ["ready", "forwards", "backwards"];

    private bool shown;
    private string? kind;

    public string? KindOf(Tool? tool, bool ownOars) =>
        tool != null && strokes.ContainsKey(tool.Kind) ? tool.Kind
        : ownOars && strokes.ContainsKey(RowerStation.OwnOars) ? RowerStation.OwnOars
        : null;

    public void Show(EntityBoat boat, string? wanted)
    {
        if (shown && wanted == kind) return;

        var seats = boat.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        foreach (var key in Keys)
        {
            if (boat.MountAnimations.GetValueOrDefault(key) is { Length: > 0 } playing)
            {
                foreach (var seat in seats) seat.Passenger?.AnimManager?.StopAnimation(playing);
            }
            boat.MountAnimations[key] = wanted == null ? "" : strokes[wanted][key];
        }
        (shown, kind) = (true, wanted);
        vessels.Log(boat, wanted == null ? "strokes stowed" : "strokes of the {0}", wanted ?? "");
    }
}
