using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace RoughSeas.SeaAndWeather;

public class CurrentDrift(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:currentdrift";

    private SeaWeatherSystem sea = null!;
    private EntityBehaviorPassivePhysics? physics;
    private IMountable? mountable;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        mountable = entity.GetInterface<IMountable>();
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        physics = entity.GetBehavior<EntityBehaviorPassivePhysics>();
    }

    public override void OnGameTick(float deltaTime)
    {
        if (physics == null || !entity.Swimming || mountable?.IsBeingControlled() == true) return;
        using var _ = sea.Budget.Measure();
        var pos = entity.Pos;
        var current = sea.CurrentAt(pos.X, pos.Z);
        // Added after each stretch of vanilla's water drag, so the drag pulls the motion towards the current, not to rest.
        var kept = WaterDrag.Kept(physics.WaterDragValue, deltaTime);
        pos.Motion.X += WaterDrag.Carried(current.X, current.X, kept);
        pos.Motion.Z += WaterDrag.Carried(current.Z, current.Z, kept);
    }
}
