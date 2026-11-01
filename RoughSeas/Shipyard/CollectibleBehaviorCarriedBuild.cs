using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>A vessel carried before she is finished: her item shows her as far as she is built, and says so.</summary>
public class CollectibleBehaviorCarriedBuild(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:carriedbuild";

    private Dictionary<string, MultiTextureMeshRef> meshes = [];

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        if (Framing(capi, itemstack) is not { } framing) return;

        var key = framing.ShapeKey;
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = Tesselate(capi, framing);
        if (mesh != null) renderinfo.ModelRef = mesh;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack is { } stack && Framing(world.Api, stack)?.Progress(world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours) is { } progress)
        {
            dsc.AppendLine(progress);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        foreach (var mesh in meshes.Values) mesh?.Dispose();
        meshes = [];
    }

    private Framing? Framing(ICoreAPI api, ItemStack stack)
    {
        if (stack.Attributes.GetTreeAttribute(EntityBehaviorBuild.TreeKey) is not { } tree) return null;

        var stages = ObjectCacheUtil.GetOrCreate(api, $"{Code}/stages/{collectible.Code}", () =>
        {
            var config = api.World.GetEntityType(collectible.Code) is { } type ? FramingConfig.Of(type) ?? CoveringConfig.Of(type) : null;
            config?.Measure(collectible is Item { Shape.Base: { } shape } ? FramingConfig.ShapeAt(api, shape) : null);
            return config?.Stages;
        });
        return stages == null ? null : new Framing(() => tree, () => { }, stages);
    }

    private Shape? BaseShape(ICoreAPI api) =>
        collectible is Item { Shape.Base: { } shape } ? Shape.TryGet(api, shape.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json")) : null;

    private MultiTextureMeshRef? Tesselate(ICoreClientAPI capi, Framing framing)
    {
        if (collectible is not Item item || BaseShape(capi) is not { } shape) return null;

        bool cloned = true;
        var drawnByBuild = framing.Stages.SelectMany(stage => new[] { stage.Replaces, stage.Bends }).OfType<string>().Append(Shipyard.Framing.SheetGroup);
        var ignored = item.Shape.IgnoreElements?.Except(drawnByBuild) ?? [];
        shape.RemoveElements(framing.Hidden().Concat(ignored).ToArray());
        if (framing.Wood is { } wood) WoodTexture.Draw(capi, ref shape, ref cloned, WoodTexture.Frame, wood, item.Textures, capi.ItemTextureAtlas);
        capi.Tesselator.TesselateShape(item, shape, out var mesh);
        return capi.Render.UploadMultiTextureMesh(mesh);
    }
}
