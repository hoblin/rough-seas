using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Vanilla's boat, whose <c>speedMultiplier</c> scales turning as much as speed; here <c>turnSpeed</c> sets turning
/// on its own, against the raft's.
/// </summary>
public class EntityVessel : EntityBoat
{
    public const string ClassName = "roughseas:EntityVessel";

    private double turnSpeed = 1;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        turnSpeed = properties.Attributes["turnSpeed"].AsDouble(1);
    }

    public override Vec2d SeatsToMotion(float dt)
    {
        var motion = base.SeatsToMotion(dt);
        return new Vec2d(motion.X, motion.Y * turnSpeed / SpeedMultiplier);
    }
}
