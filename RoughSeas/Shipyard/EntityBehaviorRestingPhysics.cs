using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public class EntityBehaviorRestingPhysics(Entity entity) : EntityBehaviorPassivePhysics(entity)
{
    public const string Code = "roughseas:restingphysics";

    protected override void applyCollision(EntityPos pos, float dtFactor)
    {
        if (entity.OnGround)
        {
            pos.Motion.X = 0;
            pos.Motion.Z = 0;
        }
        base.applyCollision(pos, dtFactor);
    }
}
