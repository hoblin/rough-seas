using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Cargo;

/// <summary>
/// A stack as vanilla's ground storage lays it on one block, with as many models as the given share of a full pile.
/// </summary>
public sealed class GroundPile : ITexPositionSource
{
    private const string TransformCode = "groundStorageTransform";
    private const string CacheKey = "roughseas-cargo-piles";

    private readonly ICoreClientAPI capi;
    private CollectibleObject tesselating = null!;
    private Shape? shape;
    private Dictionary<string, AssetLocation>? stackingTextures;

    public GroundPile(ICoreClientAPI capi) => this.capi = capi;

    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            if (stackingTextures != null && stackingTextures.TryGetValue(textureCode, out var stacking)) return Position(stacking);

            var textures = tesselating is Item item ? item.Textures : ((Block)tesselating).Textures;
            AssetLocation? path = null;
            if (textures.TryGetValue(textureCode, out var texture) || textures.TryGetValue("all", out texture)) path = texture.Baked.BakedName;
            else if (shape?.Textures.TryGetValue(textureCode, out var fromShape) == true) path = fromShape;
            return Position(path ?? new AssetLocation(textureCode));
        }
    }

    public bool Draws(ItemStack stack) => Props(stack) != null;

    public MeshData? Of(ItemStack stack, double share)
    {
        if (Props(stack) is not { } props) return null;

        bool stacked = props.Layout == EnumGroundStorageLayout.Stacking;
        int most = stacked ? (int)Math.Ceiling(props.StackingCapacity / (float)props.ItemsPerModel) : Offsets(props.Layout).Length;
        int models = Math.Clamp((int)Math.Ceiling(share * most), 1, most);
        var cache = ObjectCacheUtil.GetOrCreate(capi, CacheKey, () => new Dictionary<string, MeshData?>());
        string key = $"{models}x{stack.Collectible.Code}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        tesselating = stack.Collectible;
        shape = null;
        stackingTextures = null;
        return cache[key] = stacked ? Stacked(props, models) : Laid(stack, props, models);
    }

    [ReliesOn("Vintagestory.GameContent.BlockEntityGroundStorage", "getOrCreateMesh", "a stacking pile draws the first CuboidsPerModel × models elements of its stacking model")]
    private MeshData? Stacked(GroundStorageProperties props, int models)
    {
        shape = Shape.TryGet(capi, props.StackingModel.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
        if (shape == null) return null;

        stackingTextures = props.StackingTextures;
        capi.Tesselator.TesselateShape("cargo pile", shape, out var mesh, this, null, 0, 0, 0, props.CuboidsPerModel * models);
        return mesh;
    }

    private MeshData Laid(ItemStack stack, GroundStorageProperties props, int count)
    {
        var one = One(stack);
        var offsets = Offsets(props.Layout);
        var pile = one.Clone().Clear();
        for (int i = 0; i < count; i++) pile.AddMeshData(one, offsets[i].X, offsets[i].Y, offsets[i].Z);
        return pile;
    }

    [ReliesOn("Vintagestory.GameContent.BlockEntityDisplay", "applyDefaultTranforms", "applies groundStorageTransform, then lays a voxelized item flat at a third")]
    private MeshData One(ItemStack stack)
    {
        MeshData mesh;
        if (stack.Class == EnumItemClass.Block)
        {
            mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
        }
        else
        {
            if (stack.Item.Shape?.Base != null) shape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
            capi.Tesselator.TesselateItem(stack.Item, out mesh, this);
            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
        }

        if (stack.Collectible.Attributes?[TransformCode].AsObject<ModelTransform>() is { } transform)
        {
            transform.EnsureDefaultValues();
            mesh.ModelTransform(transform);
        }

        if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
        {
            mesh.Rotate(GameMath.PIHALF, 0, 0);
            mesh.Scale(0.33f, 0.33f, 0.33f);
            mesh.Translate(0, -7.5f / 16f, 0f);
        }

        return mesh;
    }

    private static GroundStorageProperties? Props(ItemStack stack) => stack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;

    [ReliesOn("Vintagestory.GameContent.BlockEntityGroundStorage", "GetLayoutOffset", "each layout lays its items at these offsets")]
    private static Vec3f[] Offsets(EnumGroundStorageLayout layout) => layout switch
    {
        EnumGroundStorageLayout.Halves or EnumGroundStorageLayout.WallHalves => [new(-0.25f, 0, 0), new(0.25f, 0, 0)],
        EnumGroundStorageLayout.Quadrants => [new(-0.25f, 0, -0.25f), new(-0.25f, 0, 0.25f), new(0.25f, 0, -0.25f), new(0.25f, 0, 0.25f)],
        _ => [new(0, 0, 0)]
    };

    private TextureAtlasPosition Position(AssetLocation path)
    {
        if (capi.BlockTextureAtlas[path] is { } known) return known;
        return capi.BlockTextureAtlas.GetOrInsertTexture(path, out _, out var inserted) ? inserted : capi.BlockTextureAtlas.UnknownTexturePosition;
    }
}
