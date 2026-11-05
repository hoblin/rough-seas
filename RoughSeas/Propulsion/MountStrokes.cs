using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public sealed class MountStrokes(Dictionary<string, Dictionary<string, string>> strokes, VesselsModSystem vessels)
{
    private const string Ready = "ready";
    private const string Forwards = "forwards";
    private const string Change = "change";
    private const float FramesASecond = 30;
    private static readonly string[] Keys = [Ready, Forwards, "backwards"];

    private bool shown;
    private (string? Kind, bool Changing, bool Sweeping, Entity? Rower) showing;
    private (string Code, string? Tool)? playing;
    private int? finishingFrom;

    public bool Sided(Tool? tool) => tool != null && strokes.ContainsKey(OnSide(tool.Kind, Side.Port));

    public string? KindOf(Tool? tool, bool ownOars, Side side) =>
        tool != null && strokes.ContainsKey(OnSide(tool.Kind, side)) ? OnSide(tool.Kind, side)
        : tool != null && strokes.ContainsKey(tool.Kind) ? tool.Kind
        : ownOars && strokes.ContainsKey(RowerStation.OwnOars) ? RowerStation.OwnOars
        : null;

    /// <param name="sweeping">A sided paddle turning her where she lies: it sweeps forward on the side that turns her, whichever
    /// way vanilla's keys would row.</param>
    /// <param name="stance">Where the rower stands to work the tool, on his seat alone; none, and he works it seated.</param>
    /// <param name="tool">What the rower holds: a stroke is finished with the tool it was started with, never another.</param>
    public void Show(EntityBoat boat, Entity? rower, string? wanted, bool changing, bool sweeping, SeatPose? stance, string? tool)
    {
        var seats = boat.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        var poses = SeatPoses.Of(boat);
        foreach (var seat in seats) poses.Stand(seat, rower != null && seat.Passenger == rower ? stance : null);
        if (!shown || showing != (wanted, changing, sweeping, rower))
        {
            foreach (var key in Keys)
            {
                if (boat.MountAnimations.GetValueOrDefault(key) is { Length: > 0 } stopping && stopping != playing?.Code)
                {
                    foreach (var seat in seats) seat.Passenger?.AnimManager?.StopAnimation(stopping);
                }
                boat.MountAnimations[key] = wanted == null ? "" : strokes[wanted][changing && key != Ready ? Change : sweeping && key != Ready ? Forwards : key];
            }
            (shown, showing) = (true, (wanted, changing, sweeping, rower));
            vessels.Log(boat, wanted == null ? "strokes stowed" : changing ? "changes to the {0}" : stance == null ? "strokes of the {0}" : "strokes of the {0}, standing", wanted ?? "");
        }
        if (rower is EntityAgent agent && seats.FirstOrDefault(seat => seat.Passenger == rower) is EntityBoatSeat rowing)
        {
            Finish(boat, agent, rowing, wanted == null ? null : tool);
        }
    }

    /// <summary>
    /// A rower finishes the stroke he is in, or the change of sides, before he stops, rows the other way or starts the
    /// next. Vanilla picks the seat's animation from the keys each tick (<c>EntityBoat.SeatsToMotion</c>), so until the
    /// one playing has come round to its end, the seat is given it back and the ready pose vanilla started is stopped.
    /// </summary>
    private void Finish(EntityBoat boat, EntityAgent rower, EntityBoatSeat seat, string? tool)
    {
        if (playing is { } current && seat.actionAnim != current.Code && current.Tool == tool &&
            rower.AnimManager?.GetAnimationState(current.Code) is { Active: true } running)
        {
            if (finishingFrom == null)
            {
                finishingFrom = Begun(running) ? running.Iterations : -1;
                if (finishingFrom >= 0) vessels.Log(boat, "finishes the {0} from frame {1:F0} before the {2}", current.Code, running.CurrentFrame, seat.actionAnim ?? "ready pose");
            }
            if (running.Iterations == finishingFrom && running.CurrentFrame < running.Animation.QuantityFrames - 1)
            {
                seat.actionAnim = current.Code;
                if (boat.MountAnimations.GetValueOrDefault(Ready) is { Length: > 0 } ready && ready != current.Code) rower.AnimManager.StopAnimation(ready);
                return;
            }
        }
        finishingFrom = null;
        playing = seat.actionAnim == null ? null : (seat.actionAnim, tool);
    }

    /// <summary>
    /// A stroke that came round less than one blend ago counts as finished: the client's strokes run up to that far
    /// from the server's phase, which decides when the paddler changes sides.
    /// </summary>
    private static bool Begun(RunningAnimation running) =>
        running.CurrentFrame >= FramesASecond * running.meta.AnimationSpeed / running.meta.EaseInSpeed;

    private static string OnSide(string kind, Side side) => $"{kind}-{side.ToString().ToLowerInvariant()}";
}
