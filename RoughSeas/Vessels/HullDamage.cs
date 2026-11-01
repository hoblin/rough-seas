using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public sealed class HullDamage
{
    public const int FirstWoundedStage = 1;
    public static readonly string[] WoundForms = ["Scratched", "Cracked", "Split", "StoveIn"];

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly HullDamageConfig config;
    private Shown shown = new(new Dictionary<string, TextureAtlasPosition>(), new Dictionary<HullPlace, int>());

    public HullDamage(ICoreClientAPI capi, Entity entity, HullDamageConfig config)
    {
        this.capi = capi;
        this.entity = entity;
        this.config = config;
    }

    public bool Overlaid => shown.Positions.Count > 0;

    public bool Shows(System.Func<HullPlace, int> stage) =>
        HullShape.Places.All(place => stage(place) == shown.Stages.GetValueOrDefault(place));

    public void Show(System.Func<HullPlace, int> stage)
    {
        // Inserting may grow the entity atlas, which only the main thread may do; tesselation can run elsewhere,
        // so it gets a finished snapshot, never one being filled.
        var stages = HullShape.Places.Where(place => stage(place) > 0).ToDictionary(place => place, stage);
        var positions = new Dictionary<string, TextureAtlasPosition>();
        foreach (var (place, placeStage) in stages)
        {
            var overlay = new BlendedOverlayTexture { Base = config.Overlay(placeStage) };
            foreach (var code in config.Textures)
            {
                if (!entity.Properties.Client.Textures.TryGetValue(code, out var texture)) continue;

                var damagedTexture = texture.Clone();
                damagedTexture.BlendedOverlays = [.. damagedTexture.BlendedOverlays ?? [], overlay];
                capi.EntityTextureAtlas.GetOrInsertTexture(damagedTexture, out _, out var position);
                positions[Placed(code, place)] = position;
            }
        }
        shown = new Shown(positions, stages);
    }

    public void Retexture(ref Shape shape, ref bool shapeIsCloned, HullShape hull)
    {
        var snapshot = shown;
        if (snapshot.Positions.Count == 0) return;

        if (!shapeIsCloned)
        {
            shape = shape.Clone();
            shapeIsCloned = true;
        }
        foreach (var (element, place) in Places(shape.Elements, hull, config.Textures))
        {
            if (snapshot.Stages.ContainsKey(place)) Mark(element, place);
        }
        foreach (var code in config.Textures)
        {
            if (!shape.TextureSizes.TryGetValue(code, out var size)) continue;
            foreach (var place in snapshot.Stages.Keys) shape.TextureSizes[Placed(code, place)] = size;
        }
    }

    public string[] Hidden(Shape shape)
    {
        var stages = shown.Stages;
        return Hidden(shape.Elements, place => stages.GetValueOrDefault(place)).ToArray();
    }

    public static IEnumerable<string> Hidden(ShapeElement[]? elements, System.Func<HullPlace, int> stage) =>
        Hidden(elements, HullShape.Places
            .SelectMany(place => WoundForms
                .Where((_, n) => FirstWoundedStage + n != stage(place))
                .Select(form => $"Wound{place}{form}"))
            .ToArray());

    private static IEnumerable<string> Hidden(ShapeElement[]? elements, string[] forms)
    {
        foreach (var element in elements ?? [])
        {
            if (element.Name is { } named && forms.Any(form => named.StartsWith(form, StringComparison.Ordinal))) yield return named;
            else foreach (var name in Hidden(element.Children, forms)) yield return name;
        }
    }

    public ITexPositionSource Over(ITexPositionSource defaultSource) => new DamagedSource(shown.Positions, defaultSource);

    public static IEnumerable<(ShapeElement Element, HullPlace Place)> Places(ShapeElement[]? elements, HullShape hull, string[] textures) =>
        Places(elements, Mat4f.Create(), hull, textures);

    private static IEnumerable<(ShapeElement Element, HullPlace Place)> Places(ShapeElement[]? elements, float[] parent, HullShape hull, string[] textures)
    {
        foreach (var element in elements ?? [])
        {
            var transform = Mat4f.Mul(Mat4f.Create(), parent, element.GetLocalTransformMatrix(0));
            if (element.FacesResolved?.Any(face => face != null && textures.Contains(face.Texture)) == true)
            {
                var size = element.To!.Zip(element.From!, (to, from) => (float)((to - from) / HullShape.VoxelsPerBlock / 2)).ToArray();
                var centre = Mat4f.MulWithVec4(transform, size[0], size[1], size[2], 1);
                yield return (element, hull.At(centre[0] - 0.5, centre[1], centre[2] - 0.5));
            }
            foreach (var child in Places(element.Children, transform, hull, textures)) yield return child;
        }
    }

    private void Mark(ShapeElement element, HullPlace place)
    {
        var faces = element.FacesResolved!;
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i] is not { } face || !config.Textures.Contains(face.Texture)) continue;

            faces[i] = new ShapeElementFace
            {
                Texture = Placed(face.Texture, place),
                Uv = face.Uv,
                ReflectiveMode = face.ReflectiveMode,
                WindMode = face.WindMode,
                WindData = face.WindData,
                Rotation = face.Rotation,
                Glow = face.Glow,
                Enabled = face.Enabled
            };
        }
    }

    private static string Placed(string code, HullPlace place) => $"{code}-damage-{place}";

    private sealed record Shown(IReadOnlyDictionary<string, TextureAtlasPosition> Positions, IReadOnlyDictionary<HullPlace, int> Stages);

    private sealed class DamagedSource(IReadOnlyDictionary<string, TextureAtlasPosition> damaged, ITexPositionSource defaultSource)
        : ITexPositionSource
    {
        public Size2i? AtlasSize => defaultSource.AtlasSize;

        public TextureAtlasPosition? this[string textureCode] =>
            damaged.TryGetValue(textureCode, out var position) ? position : defaultSource[textureCode];
    }
}
