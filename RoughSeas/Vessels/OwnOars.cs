using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// The oars or paddle a vessel carries, in its rower's hands: a stroke named in the vessel's <c>mountAnimations</c>
/// that the vessel registers in its own <c>client/animations</c> plays on the vessel at the rower's frame. Builders fit
/// the two to each other (see docs/modeling.md).
/// </summary>
public class OwnOars
{
    private static readonly string[] Strokes = ["ready", "forwards", "backwards"];

    private readonly EntityBoat vessel;
    private readonly VesselsModSystem system;
    private readonly string[] codes;

    private OwnOars(EntityBoat vessel, string[] codes)
    {
        this.vessel = vessel;
        this.codes = codes;
        system = vessel.Api.ModLoader.GetModSystem<VesselsModSystem>();
    }

    public static OwnOars? Of(EntityBoat vessel, IReadOnlyDictionary<string, string> mountAnimations)
    {
        var own = vessel.Properties.Client.AnimationsByMetaCode;
        var codes = Strokes.Select(stroke => mountAnimations.GetValueOrDefault(stroke))
            .OfType<string>()
            .Where(own.ContainsKey)
            .ToArray();
        return codes.Length > 0 ? new OwnOars(vessel, codes) : null;
    }

    public void Follow()
    {
        if (vessel.AnimManager.Animator == null) return;
        var rower = vessel.GetBehavior<EntityBehaviorSeatable>()?.Controller as EntityAgent;
        foreach (var code in codes)
        {
            var stroke = rower?.AnimManager?.Animator == null ? null : rower.AnimManager.GetAnimationState(code);
            if (stroke?.Active == true) Match(code, stroke);
            else if (vessel.AnimManager.IsAnimationActive(code)) Stop(code);
        }
    }

    private void Match(string code, RunningAnimation stroke)
    {
        if (!vessel.AnimManager.IsAnimationActive(code))
        {
            vessel.AnimManager.StartAnimation(code);
            system.Log(vessel, "oars in the rower's hands: {0}", code);
        }
        var own = vessel.AnimManager.GetAnimationState(code);
        if (own != null) own.CurrentFrame = stroke.CurrentFrame;
    }

    private void Stop(string code)
    {
        vessel.AnimManager.StopAnimation(code);
        system.Log(vessel, "oars out of the rower's hands: {0}", code);
    }
}
