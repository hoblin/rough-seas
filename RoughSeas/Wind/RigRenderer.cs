using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public sealed record RigTextures(string Cloth, string Wood, string Cord);

public sealed class RigRenderer : IRenderer, IRigCanvas
{
    private const int Reaches = 10;
    private const int Spans = 8;
    private const double OriginMetres = 0.5;
    private static readonly (float U, float V)[] Tile = [(0, 0), (1, 0), (1, 1), (0, 1)];
    private static readonly (int Facing, int[] Order)[] Sides = [(1, [0, 1, 2, 0, 2, 3]), (-1, [0, 2, 1, 0, 3, 2])];

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly EntityBehaviorWindage windage;
    private readonly RigTextures textures;
    private readonly Dictionary<string, Layer> meshes = new();
    private readonly Dictionary<string, TextureAtlasPosition> atlases = new();
    private readonly RigRopes ropes;
    private readonly float[] placed = new float[4];
    private float[] model = Mat4f.Create();
    private Vec3d camera = new();
    private (RigState State, double Fill, (double Reach, double Span) Shown, bool Rolled)? lastDrawn;

    public RigRenderer(ICoreClientAPI capi, Entity entity, EntityBehaviorWindage windage, RigTextures textures)
    {
        this.capi = capi;
        this.entity = entity;
        this.windage = windage;
        this.textures = textures;
        foreach (var texture in new[] { textures.Cloth, textures.Wood }) meshes[texture] = new Layer();
        ropes = new RigRopes(capi, entity);
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "roughseas-rig");
    }

    private Sail Sail => windage.Sail!;

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    [ReliesOn("Vintagestory.GameContent.EntityShapeRenderer", "DoRender3DOpaque", "sets the hull's ModelMat for this frame in an earlier render order than this renderer's, so the rig draws with her heave, roll and pitch of the same frame")]
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull || !windage.Rigged) return;

        (model, camera) = (hull.ModelMat, capi.World.Player.Entity.CameraPos);
        var drawing = (Sail.State, Sail.Fill, Sail.Shown, Sail.Rolled);
        if (drawing != lastDrawn) Rebuild(Sail.Rig.End, drawing);
        lastDrawn = drawing;
        ropes.Pin(World);

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.ModelMatrix = hull.ModelMat;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        foreach (var (texture, layer) in meshes)
        {
            if (layer.Mesh == null || layer.Data.VerticesCount == 0) continue;
            prog.Tex2D = capi.EntityTextureAtlas.AtlasTextures[Atlas(texture).atlasNumber].TextureId;
            render.RenderMesh(layer.Mesh);
        }
        prog.Stop();
    }

    private void Rebuild(Rigging end, (RigState State, double Fill, (double Reach, double Span) Shown, bool Rolled) drawing)
    {
        foreach (var layer in meshes.Values) layer.Data.Clear();
        ropes.Begin();
        if (drawing.Rolled) end.DrawFurled(this, drawing.State);
        else end.Draw(this, drawing.State, drawing.Fill);
        ropes.End();

        var render = capi.Render;
        foreach (var layer in meshes.Values)
        {
            if (layer.Data.VerticesCount == 0) continue;
            if (layer.Mesh == null || layer.Data.VerticesCount > layer.Uploaded)
            {
                layer.Mesh?.Dispose();
                (layer.Mesh, layer.Uploaded) = (render.UploadMesh(layer.Data), layer.Data.VerticesCount);
            }
            else render.UpdateMesh(layer.Mesh, layer.Data);
        }
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        foreach (var layer in meshes.Values) layer.Mesh?.Dispose();
        meshes.Clear();
        ropes.Dispose();
    }

    private Vec3d World(HullPoint point)
    {
        Mat4f.MulWithVec4(model, new float[] { (float)(point.Along + OriginMetres), (float)point.Height, (float)(point.Across + OriginMetres), 1 }, placed);
        return new Vec3d(camera.X + placed[0], camera.Y + placed[1], camera.Z + placed[2]);
    }

    private sealed class Layer
    {
        public readonly MeshData Data = new(64, 96, withNormals: false, withUv: true, withRgba: true, withFlags: true);
        public MeshRef? Mesh;
        public int Uploaded;
    }

    public void Spar(HullPoint from, HullPoint to, double width) => Tube(textures.Wood, from, to, width);

    public void Line(HullPoint from, HullPoint to, double width, double metres) => ropes.Lay(from, to, metres, World);

    public void Roll(HullPoint from, HullPoint to, double width) => Tube(textures.Cloth, from, to, width);

    public void Cloth(SailForm form, double fill, (double Reach, double Span) set) => Weave(form, fill, set);

    private void Weave(SailForm form, double fill, (double Reach, double Span) set)
    {
        HullPoint At(int i, int j) => form.At((double)i / Reaches, (double)j / Spans, fill, set);
        for (var i = 0; i < Reaches; i++)
        for (var j = 0; j < Spans; j++) Quad(textures.Cloth, [At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1)]);
    }

    private void Tube(string texture, HullPoint start, HullPoint end, double width)
    {
        var along = (end - start).Unit;
        var side = along.Cross(Math.Abs(along.Height) < 0.9 ? new HullPoint(0, 1, 0) : new HullPoint(1, 0, 0)).Unit;
        var up = side.Cross(along);
        var (a, b) = (width / 2 * side, width / 2 * up);
        HullPoint[] ring = [a + b, a - b, -a - b, -a + b];
        for (var k = 0; k < 4; k++)
        {
            var (p, q) = (ring[k], ring[(k + 1) % 4]);
            Quad(texture, [start + p, end + p, end + q, start + q]);
        }
    }

    private void Quad(string texture, HullPoint[] corners)
    {
        var atlas = Atlas(texture);
        var (width, height) = (atlas.x2 - atlas.x1, atlas.y2 - atlas.y1);
        var data = meshes[texture].Data;
        var normal = (corners[2] - corners[0]).Cross(corners[3] - corners[1]);
        var unit = normal.Length > 0 ? normal.Unit : new HullPoint(0, 1, 0);
        foreach (var (facing, order) in Sides)
        {
            var flags = VertexFlags.PackNormal(facing * unit.Along, facing * unit.Height, facing * unit.Across);
            var first = data.VerticesCount;
            for (var k = 0; k < 4; k++)
            {
                var c = corners[k];
                data.AddVertexWithFlags((float)(c.Along + OriginMetres), (float)c.Height, (float)(c.Across + OriginMetres),
                    atlas.x1 + Tile[k].U * width, atlas.y1 + Tile[k].V * height, ColorUtil.WhiteArgb, flags);
            }
            foreach (var index in order) data.AddIndex(first + index);
        }
    }

    private TextureAtlasPosition Atlas(string texture)
    {
        if (atlases.TryGetValue(texture, out var found)) return found;
        return atlases[texture] = capi.Tesselator.GetTextureSource(entity)[texture]
            ?? throw new InvalidOperationException($"{entity.Code} has no texture {texture} for her rig");
    }
}
