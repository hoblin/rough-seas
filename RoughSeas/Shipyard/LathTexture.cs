using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

/// <summary>A frame drawn in the wood its laths were split from: the debarked side of that wood's lying log.</summary>
public static class LathTexture
{
    private const string Frame = "frame";
    private const string BareSide = "inside-north";

    public static void Draw(ICoreClientAPI capi, ref Shape shape, ref bool shapeIsCloned, string wood, IDictionary<string, CompositeTexture> textures, ITextureAtlasAPI atlas)
    {
        var code = $"{Frame}-{wood}";
        if (!textures.ContainsKey(code))
        {
            if (capi.World.GetBlock(Wood.Parse(wood).LyingLog)?.Textures?.TryGetValue(BareSide, out var side) != true) return;

            var texture = side!.Clone();
            atlas.GetOrInsertTexture(texture, out int id, out _);
            texture.Baked.TextureSubId = id;
            textures[code] = texture;
        }
        ShapeFaces.Retexture(ref shape, ref shapeIsCloned, Frame, code);
    }
}
