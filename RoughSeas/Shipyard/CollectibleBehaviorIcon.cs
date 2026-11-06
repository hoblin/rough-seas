using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorIcon(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:icon";

    private const string CacheKey = "roughseas:iconMeshes";

    private CompositeShape? shape;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        shape = properties["shape"].AsObject<CompositeShape>(null, collObj.Code.Domain);
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        if (target != EnumItemRenderTarget.Gui || shape == null) return;

        var meshes = ObjectCacheUtil.GetOrCreate(capi, CacheKey, () => new Dictionary<string, MultiTextureMeshRef>());
        var key = collObj.Code.ToShortString();
        if (!meshes.TryGetValue(key, out var mesh))
        {
            var location = shape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            capi.Tesselator.TesselateShape(collObj, capi.Assets.Get(location).ToObject<Shape>(), out var data);
            mesh = meshes[key] = capi.Render.UploadMultiTextureMesh(data);
        }
        renderinfo.ModelRef = mesh;
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        if (ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, CacheKey) is { } meshes)
        {
            foreach (var mesh in meshes.Values) mesh.Dispose();
        }
        base.OnUnloaded(api);
    }
}
