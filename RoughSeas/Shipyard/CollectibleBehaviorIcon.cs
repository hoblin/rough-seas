using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorIcon(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:icon";

    private CompositeShape? shape;
    private MultiTextureMeshRef? mesh;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        shape = properties["shape"].AsObject<CompositeShape>(null, collObj.Code.Domain);
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        if (target != EnumItemRenderTarget.Gui || shape == null) return;

        if (mesh == null)
        {
            var location = shape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            capi.Tesselator.TesselateShape(collObj, capi.Assets.Get(location).ToObject<Shape>(), out var data);
            mesh = capi.Render.UploadMultiTextureMesh(data);
        }
        renderinfo.ModelRef = mesh;
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        mesh?.Dispose();
        mesh = null;
        base.OnUnloaded(api);
    }
}
