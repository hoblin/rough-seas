using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class EntityVessel : EntityBoat
{
    public const string ClassName = "roughseas:EntityVessel";

    private double forwardSpeed = 1;
    private double turnSpeed = 1;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        forwardSpeed = properties.Attributes["forwardSpeed"].AsDouble(1);
        turnSpeed = properties.Attributes["turnSpeed"].AsDouble(1);
    }

    public override Vec2d SeatsToMotion(float dt)
    {
        var motion = base.SeatsToMotion(dt);
        return new Vec2d(motion.X * forwardSpeed, motion.Y * turnSpeed);
    }
}
