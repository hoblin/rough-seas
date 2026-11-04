using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public sealed class MountStrokes(Dictionary<string, Dictionary<string, string>> strokes, VesselsModSystem vessels)
{
    private const string Ready = "ready";
    private const string Forwards = "forwards";
    private const string Change = "change";
    private static readonly string[] Keys = [Ready, Forwards, "backwards"];

    private bool shown;
    private (string? Kind, bool Changing, bool Sweeping) showing;

    public bool Sided(Tool? tool) => tool != null && strokes.ContainsKey(OnSide(tool.Kind, Side.Port));

    public string? KindOf(Tool? tool, bool ownOars, Side side) =>
        tool != null && strokes.ContainsKey(OnSide(tool.Kind, side)) ? OnSide(tool.Kind, side)
        : tool != null && strokes.ContainsKey(tool.Kind) ? tool.Kind
        : ownOars && strokes.ContainsKey(RowerStation.OwnOars) ? RowerStation.OwnOars
        : null;

    /// <param name="sweeping">A sided paddle turning her where she lies: it sweeps forward on the side that turns her, whichever
    /// way vanilla's keys would row.</param>
    public void Show(EntityBoat boat, string? wanted, bool changing, bool sweeping)
    {
        if (shown && showing == (wanted, changing, sweeping)) return;

        var seats = boat.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        foreach (var key in Keys)
        {
            if (boat.MountAnimations.GetValueOrDefault(key) is { Length: > 0 } playing)
            {
                foreach (var seat in seats) seat.Passenger?.AnimManager?.StopAnimation(playing);
            }
            boat.MountAnimations[key] = wanted == null ? "" : strokes[wanted][changing && key != Ready ? Change : sweeping && key != Ready ? Forwards : key];
        }
        (shown, showing) = (true, (wanted, changing, sweeping));
        vessels.Log(boat, wanted == null ? "strokes stowed" : changing ? "changes to the {0}" : "strokes of the {0}", wanted ?? "");
    }

    private static string OnSide(string kind, Side side) => $"{kind}-{side.ToString().ToLowerInvariant()}";
}
