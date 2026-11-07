using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

public static class ShapeFaces
{
    /// <summary>
    /// Draws every face of <paramref name="from"/> with <paramref name="to"/> instead. A shape cloned for this keeps
    /// sharing its faces with the cached one, so each changed face is replaced rather than edited.
    /// </summary>
    public static void Retexture(ref Shape shape, ref bool shapeIsCloned, string from, string to)
    {
        if (!shapeIsCloned)
        {
            shape = shape.Clone();
            shapeIsCloned = true;
        }
        foreach (var element in shape.Elements) Retexture(element, from, to);
    }

    private static void Retexture(ShapeElement element, string from, string to)
    {
        var faces = element.FacesResolved ?? [];
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i] is not { } face || face.Texture != from) continue;

            faces[i] = new ShapeElementFace
            {
                Texture = to,
                Uv = face.Uv,
                ReflectiveMode = face.ReflectiveMode,
                WindMode = face.WindMode,
                WindData = face.WindData,
                Rotation = face.Rotation,
                Glow = face.Glow,
                Enabled = face.Enabled
            };
        }
        foreach (var child in element.Children ?? []) Retexture(child, from, to);
    }
}
