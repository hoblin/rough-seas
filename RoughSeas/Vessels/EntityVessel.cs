using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Vanilla's boat, whose <c>speedMultiplier</c> scales turning as much as speed; here <c>turnSpeed</c> sets turning
/// on its own, against the raft's. A multiplier of 0 leaves it dead in the water: no paddling, no turning.
/// </summary>
public class EntityVessel : EntityBoat
{
    public const string ClassName = "roughseas:EntityVessel";

    private static readonly string[] Strokes = ["ready", "forwards", "backwards"];

    private double turnSpeed = 1;
    private Dictionary<string, string> strokes = [];
    private OwnOars? oars;
    private bool? underSail;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        turnSpeed = properties.Attributes["turnSpeed"].AsDouble(1);
        strokes = new Dictionary<string, string>(MountAnimations);
        if (properties.Attributes["unfurlSails"].AsBool())
        {
            WatchedAttributes.RegisterModifiedListener("sailPosition", StrokeUnderSail);
            StrokeUnderSail();
        }
        if (api.Side == EnumAppSide.Client) oars = OwnOars.Of(this, strokes);
    }

    public override void OnRenderFrame(float dt, EnumRenderStage stage)
    {
        base.OnRenderFrame(dt, stage);
        oars?.Follow();
    }

    public override Vec2d SeatsToMotion(float dt)
    {
        var motion = base.SeatsToMotion(dt);
        return new Vec2d(motion.X, SpeedMultiplier > 0 ? motion.Y * turnSpeed / SpeedMultiplier : 0);
    }

    protected override bool HasPaddle(Entity entity) => SpeedMultiplier > 0 && base.HasPaddle(entity);

    private void StrokeUnderSail()
    {
        var sailing = sailPosition > 0;
        if (sailing == underSail) return;
        underSail = sailing;
        foreach (var stroke in Strokes)
        {
            var code = strokes[stroke];
            if (sailing)
            {
                foreach (var seat in GetBehavior<EntityBehaviorSeatable>()?.Seats ?? []) seat.Passenger?.AnimManager?.StopAnimation(code);
            }
            MountAnimations[stroke] = sailing ? "" : code;
        }
        Api.ModLoader.GetModSystem<VesselsModSystem>().Log(this, sailing ? "sail set: paddle stowed" : "sail furled: paddle to hand");
    }
}
