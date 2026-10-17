using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// The oars or paddle a vessel carries, in its rower's hands: a stroke of the rower's <c>mountAnimations</c> that the
/// vessel registers in its own <c>client/animations</c> plays on the vessel at the rower's frame. Builders fit the
/// two to each other (see docs/modeling.md).
/// </summary>
public class OwnOars
{
    private static readonly string[] Strokes = ["ready", "forwards", "backwards"];

    private readonly EntityBoat vessel;
    private readonly VesselsModSystem system;
    private readonly string[] codes;

    public OwnOars(EntityBoat vessel)
    {
        this.vessel = vessel;
        system = vessel.Api.ModLoader.GetModSystem<VesselsModSystem>();
        var own = vessel.Properties.Client.AnimationsByMetaCode;
        codes = Strokes.Select(stroke => vessel.MountAnimations.GetValueOrDefault(stroke))
            .OfType<string>()
            .Where(own.ContainsKey)
            .ToArray();
    }

    public void Follow()
    {
        var rower = vessel.GetBehavior<EntityBehaviorSeatable>()?.Controller as EntityAgent;
        foreach (var code in codes)
        {
            var stroke = rower?.AnimManager?.GetAnimationState(code);
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
        if (own == null) return;
        own.CurrentFrame = stroke.CurrentFrame;
        own.EasingFactor = stroke.EasingFactor;
        own.BlendedWeight = stroke.BlendedWeight;
    }

    private void Stop(string code)
    {
        vessel.AnimManager.StopAnimation(code);
        system.Log(vessel, "oars out of the rower's hands: {0}", code);
    }
}
