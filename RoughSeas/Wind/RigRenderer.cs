using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public sealed class RigRenderer : IRenderer, IRigCanvas
{
    private const int PanelsPerMetre = 2;
    private const double OriginMetres = 0.5;
    private static readonly HullPoint Origin = new(OriginMetres, 0, OriginMetres);
    private static readonly (int Facing, int[] Order)[] Sides = [(1, [0, 1, 2, 0, 2, 3]), (-1, [0, 2, 1, 0, 3, 2])];
    private static readonly EnumRenderStage[] Stages = [EnumRenderStage.Opaque, EnumRenderStage.ShadowFar, EnumRenderStage.ShadowNear];

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly EntityBehaviorWindage windage;
    private readonly string cloth;
    private readonly IReadOnlyDictionary<string, string> parts;
    private readonly Dictionary<string, MultiTextureMeshRef?> spars = new();
    private readonly List<(MultiTextureMeshRef Mesh, float[] Placed)> placedParts = [];
    private readonly MeshData weave = new(64, 96, withNormals: false, withUv: true, withRgba: true, withFlags: true);
    private readonly RigRopes ropes;
    private readonly float[] placed = new float[4];
    private readonly float[] shown = Mat4f.Create();
    private MeshRef? woven;
    private int uploaded;
    private TextureAtlasPosition? clothAtlas;
    private float[] model = Mat4f.Create();
    private Vec3d camera = new();
    private (RigState State, double Fill, (double Reach, double Span) Shown, bool Rolled)? lastDrawn;

    public RigRenderer(ICoreClientAPI capi, Entity entity, EntityBehaviorWindage windage, string cloth, IReadOnlyDictionary<string, string> parts)
    {
        (this.capi, this.entity, this.windage, this.cloth, this.parts) = (capi, entity, windage, cloth, parts);
        ropes = new RigRopes(capi, entity);
        foreach (var stage in Stages) capi.Event.RegisterRenderer(this, stage, "roughseas-rig");
    }

    private Sail Sail => windage.Sail!;

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    [ReliesOn("Vintagestory.GameContent.EntityShapeRenderer", "DoRender3DOpaque", "sets the hull's ModelMat for this frame, in the shadow passes too, in an earlier render order than this renderer's, so the rig draws with her heave, roll and pitch of the same frame")]
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull || !windage.Rigged) return;

        model = hull.ModelMat;
        if (stage != EnumRenderStage.Opaque)
        {
            Shadowed();
            return;
        }

        camera = capi.World.Player.Entity.CameraPos;
        var drawing = (Sail.State, Sail.Fill, Sail.Shown, Sail.Rolled);
        if (drawing != lastDrawn) Rebuild(Sail.Rig.End, drawing);
        lastDrawn = drawing;
        ropes.Pin(World);

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        if (woven != null && weave.VerticesCount > 0)
        {
            prog.ModelMatrix = model;
            prog.Tex2D = capi.EntityTextureAtlas.AtlasTextures[ClothAtlas.atlasNumber].TextureId;
            render.RenderMesh(woven);
        }
        foreach (var (mesh, at) in placedParts)
        {
            prog.ModelMatrix = Mat4f.Mul(shown, model, at);
            render.RenderMultiTextureMesh(mesh, "tex");
        }
        prog.Stop();
    }

    [ReliesOn("Vintagestory.GameContent.EntityShapeRenderer", "RenderItem", "draws a mesh into the shadow passes with the active shader's mvpMatrix and origin, its texture as tex2d")]
    private void Shadowed()
    {
        var render = capi.Render;
        if (render.CurrentActiveShader is not { } prog) return;

        float[] Mvp(float[] placing)
        {
            var mvp = Mat4f.Mul(Mat4f.Create(), render.CurrentModelviewMatrix, placing);
            return Mat4f.Mul(mvp, render.CurrentProjectionMatrix, mvp);
        }

        prog.Uniform("origin", 0f, 0f, 0f);
        if (woven != null && weave.VerticesCount > 0)
        {
            prog.UniformMatrix("mvpMatrix", Mvp(model));
            prog.BindTexture2D("tex2d", capi.EntityTextureAtlas.AtlasTextures[ClothAtlas.atlasNumber].TextureId, 0);
            render.RenderMesh(woven);
        }
        foreach (var (mesh, at) in placedParts)
        {
            prog.UniformMatrix("mvpMatrix", Mvp(Mat4f.Mul(Mat4f.Create(), model, at)));
            render.RenderMultiTextureMesh(mesh, "tex2d");
        }
    }

    private void Rebuild(Rigging end, (RigState State, double Fill, (double Reach, double Span) Shown, bool Rolled) drawing)
    {
        weave.Clear();
        placedParts.Clear();
        ropes.Begin();
        if (drawing.Rolled) end.DrawFurled(this, drawing.State);
        else end.Draw(this, drawing.State, drawing.Fill);
        ropes.End();

        if (weave.VerticesCount == 0) return;
        if (woven == null || weave.VerticesCount > uploaded)
        {
            woven?.Dispose();
            (woven, uploaded) = (capi.Render.UploadMesh(weave), weave.VerticesCount);
        }
        else capi.Render.UpdateMesh(woven, weave);
    }

    public void Dispose()
    {
        foreach (var stage in Stages) capi.Event.UnregisterRenderer(this, stage);
        woven?.Dispose();
        foreach (var mesh in spars.Values) mesh?.Dispose();
        spars.Clear();
        ropes.Dispose();
    }

    public void Part(string part, (HullPoint From, HullPoint To) rest, (HullPoint From, HullPoint To) at, double girth = 1)
    {
        if (Spar(part) is not { } mesh) return;

        placedParts.Add((mesh, Turned(rest, at, girth)));
    }

    public void Line(HullPoint from, HullPoint to, double width, double metres) => ropes.Lay(from, to, metres, width, World);

    public void Cloth(SailForm form, double fill, (double Reach, double Span) set)
    {
        var (reach, span) = ((form.At(1, 0.5, 0) - form.At(0, 0.5, 0)).Length, (form.At(0.5, 1, 0) - form.At(0.5, 0, 0)).Length);
        var (reaches, spans) = (Math.Max(1, (int)Math.Ceiling(reach)) * PanelsPerMetre, Math.Max(1, (int)Math.Ceiling(span)) * PanelsPerMetre);
        HullPoint At(int i, int j) => form.At((double)i / reaches, (double)j / spans, fill, set);
        for (var i = 0; i < reaches; i++)
        for (var j = 0; j < spans; j++)
            Quad([At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1)], (double)(i % PanelsPerMetre) / PanelsPerMetre, (double)(j % PanelsPerMetre) / PanelsPerMetre);
    }

    private MultiTextureMeshRef? Spar(string part)
    {
        if (spars.TryGetValue(part, out var mesh)) return mesh;
        if (!parts.TryGetValue(part, out var path) || entity.Properties.Client.LoadedShapeForEntity is not { } shape) return spars[part] = null;

        var meta = new TesselationMetaData { SelectiveElements = [path], TexSource = capi.Tesselator.GetTextureSource(entity), TypeForLogging = "entity" };
        capi.Tesselator.TesselateShape(meta, shape, out var meshData);
        return spars[part] = capi.Render.UploadMultiTextureMesh(meshData);
    }

    private static float[] Turned((HullPoint From, HullPoint To) rest, (HullPoint From, HullPoint To) at, double girth)
    {
        var (u, v) = ((rest.To - rest.From).Unit, (at.To - at.From).Unit);
        var k = u.Cross(v);
        var (sin, cos) = (k.Length, u.Dot(v));
        double[,] turn = sin < 1e-9
            ? cos > 0 ? Identity() : HalfTurn(u)
            : Rodrigues(k, sin, cos);
        var thick = new double[3, 3];
        double[] axis = [u.Along, u.Height, u.Across];
        for (var r = 0; r < 3; r++)
        for (var c = 0; c < 3; c++) thick[r, c] = (r == c ? girth : 0) + (1 - girth) * axis[r] * axis[c];
        var a = Times(turn, thick);
        var (from, to) = (rest.From + Origin, at.From + Origin);
        double[] f = [from.Along, from.Height, from.Across];
        double[] t = [to.Along, to.Height, to.Across];
        var m = Mat4f.Create();
        for (var c = 0; c < 3; c++)
        for (var r = 0; r < 3; r++) m[c * 4 + r] = (float)a[r, c];
        for (var r = 0; r < 3; r++) m[12 + r] = (float)(t[r] - (a[r, 0] * f[0] + a[r, 1] * f[1] + a[r, 2] * f[2]));
        return m;
    }

    private static double[,] Identity() => new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

    private static double[,] HalfTurn(HullPoint u)
    {
        var p = (Math.Abs(u.Height) < 0.9 ? u.Cross(new HullPoint(0, 1, 0)) : u.Cross(new HullPoint(1, 0, 0))).Unit;
        double[] q = [p.Along, p.Height, p.Across];
        var m = new double[3, 3];
        for (var r = 0; r < 3; r++)
        for (var c = 0; c < 3; c++) m[r, c] = 2 * q[r] * q[c] - (r == c ? 1 : 0);
        return m;
    }

    private static double[,] Rodrigues(HullPoint k, double sin, double cos)
    {
        double[,] x = { { 0, -k.Across, k.Height }, { k.Across, 0, -k.Along }, { -k.Height, k.Along, 0 } };
        var xx = Times(x, x);
        var m = Identity();
        for (var r = 0; r < 3; r++)
        for (var c = 0; c < 3; c++) m[r, c] += x[r, c] + xx[r, c] * (1 - cos) / (sin * sin);
        return m;
    }

    private static double[,] Times(double[,] a, double[,] b)
    {
        var m = new double[3, 3];
        for (var r = 0; r < 3; r++)
        for (var c = 0; c < 3; c++) m[r, c] = a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
        return m;
    }

    private Vec3d World(HullPoint point)
    {
        Mat4f.MulWithVec4(model, new float[] { (float)(point.Along + OriginMetres), (float)point.Height, (float)(point.Across + OriginMetres), 1 }, placed);
        return new Vec3d(camera.X + placed[0], camera.Y + placed[1], camera.Z + placed[2]);
    }

    private void Quad(HullPoint[] corners, double u, double v)
    {
        var atlas = ClothAtlas;
        var (width, height) = (atlas.x2 - atlas.x1, atlas.y2 - atlas.y1);
        var step = 1.0 / PanelsPerMetre;
        (double U, double V)[] tile = [(u, v), (u + step, v), (u + step, v + step), (u, v + step)];
        var normal = (corners[2] - corners[0]).Cross(corners[3] - corners[1]);
        var unit = normal.Length > 0 ? normal.Unit : new HullPoint(0, 1, 0);
        foreach (var (facing, order) in Sides)
        {
            var flags = VertexFlags.PackNormal(facing * unit.Along, facing * unit.Height, facing * unit.Across);
            var first = weave.VerticesCount;
            for (var k = 0; k < 4; k++)
            {
                var c = corners[k];
                weave.AddVertexWithFlags((float)(c.Along + OriginMetres), (float)c.Height, (float)(c.Across + OriginMetres),
                    (float)(atlas.x1 + tile[k].U * width), (float)(atlas.y1 + tile[k].V * height), ColorUtil.WhiteArgb, flags);
            }
            foreach (var index in order) weave.AddIndex(first + index);
        }
    }

    private TextureAtlasPosition ClothAtlas => clothAtlas ??= capi.Tesselator.GetTextureSource(entity)[cloth]
        ?? throw new InvalidOperationException($"{entity.Code} has no texture {cloth} for her sail");
}
