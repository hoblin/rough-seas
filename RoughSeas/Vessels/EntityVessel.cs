using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Vanilla's boat, with the oars she carries following her rower's stroke. A speed multiplier of 0 leaves her dead
/// in the water: no paddling, no turning.
/// </summary>
public class EntityVessel : EntityBoat
{
    public const string ClassName = "roughseas:EntityVessel";

    private OwnOars? oars;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        if (api.Side == EnumAppSide.Client) oars = OwnOars.Of(this, MountAnimations);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        base.AfterInitialized(onFirstSpawn);
        if (Api.Side == EnumAppSide.Server) StrayCargo.Restow(this);
    }

    public override void OnRenderFrame(float dt, EnumRenderStage stage)
    {
        base.OnRenderFrame(dt, stage);
        oars?.Follow();
    }

    public override string GetName() =>
        Wood.Of(Properties.Variant) is { } wood ? Lang.GetMatching($"{Code.Domain}:item-creature-{Code.Path}", wood.Name) : base.GetName();

    protected override bool HasPaddle(Entity entity) => SpeedMultiplier > 0 && base.HasPaddle(entity);
}
