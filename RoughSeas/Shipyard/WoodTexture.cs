using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

/// <summary>A part drawn in the wood it was split or hewn from: the debarked side of that wood's lying log.</summary>
public static class WoodTexture
{
    public const string Frame = "frame";
    public const string Float = "float";
    public const string FloatBark = "floatbark";
    public const string FloatRings = "floatrings";

    public const string BareSide = "inside-north";
    public const string Bark = "all";
    public const string Rings = "east";

    public static void Draw(ICoreClientAPI capi, ref Shape shape, ref bool shapeIsCloned, string part, string wood, IDictionary<string, CompositeTexture> textures, ITextureAtlasAPI atlas, string face = BareSide)
    {
        var code = $"{part}-{wood}";
        if (!textures.ContainsKey(code))
        {
            if (capi.World.GetBlock(Wood.Parse(wood).LyingLog)?.Textures?.TryGetValue(face, out var side) != true) return;

            var texture = side!.Clone();
            atlas.GetOrInsertTexture(texture, out int id, out _);
            texture.Baked.TextureSubId = id;
            textures[code] = texture;
        }
        ShapeFaces.Retexture(ref shape, ref shapeIsCloned, part, code);
    }
}
