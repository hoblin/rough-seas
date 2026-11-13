using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Cargo;

public class CargoModSystem : ModSystem
{
    public override void Start(ICoreAPI api) =>
        api.RegisterEntityBehaviorClass(EntityBehaviorCargo.Code, typeof(EntityBehaviorCargo));

    public void Log(Entity vessel, string format, params object[] args) =>
        Mod.Logger.Debug("[cargo] {0} {1}: {2}", vessel.Code.ToShortString(), vessel.EntityId, string.Format(format, args));
}
