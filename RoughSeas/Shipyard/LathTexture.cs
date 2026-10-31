using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

/// <summary>A part drawn in the wood it was split or hewn from: the debarked side of that wood's lying log.</summary>
public static class LathTexture
{
    public const string Frame = "frame";
    public const string Float = "float";

    private const string BareSide = "inside-north";

    public static void Draw(ICoreClientAPI capi, ref Shape shape, ref bool shapeIsCloned, string part, string wood, IDictionary<string, CompositeTexture> textures, ITextureAtlasAPI atlas)
    {
        var code = $"{part}-{wood}";
        if (!textures.ContainsKey(code))
        {
            if (capi.World.GetBlock(Wood.Parse(wood).LyingLog)?.Textures?.TryGetValue(BareSide, out var side) != true) return;

            var texture = side!.Clone();
            atlas.GetOrInsertTexture(texture, out int id, out _);
            texture.Baked.TextureSubId = id;
            textures[code] = texture;
        }
        ShapeFaces.Retexture(ref shape, ref shapeIsCloned, part, code);
    }
}
