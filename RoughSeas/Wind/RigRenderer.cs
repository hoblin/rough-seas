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
    private const string Stowed = "roll";
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
    private Shape? tesselated;
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
        var opaque = stage == EnumRenderStage.Opaque;
        if (!(opaque ? entity.IsRendered : entity.IsShadowRendered) || entity.Properties.Client.Renderer is not EntityShapeRenderer hull || !windage.Rigged) return;

        model = hull.ModelMat;
        if (!opaque)
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
            render.GlEnableCullFace();
            render.RenderMesh(woven);
            render.GlDisableCullFace();
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

    public void Shaped(Shape shape)
    {
        tesselated = shape;
        placedParts.Clear();
        foreach (var mesh in spars.Values) mesh?.Dispose();
        spars.Clear();
        lastDrawn = null;
    }

    private MultiTextureMeshRef? Spar(string part)
    {
        if (spars.TryGetValue(part, out var mesh)) return mesh;
        if (!parts.TryGetValue(part, out var path)) return spars[part] = null;

        var shape = part == Stowed ? entity.Properties.Client.LoadedShapeForEntity : tesselated ?? entity.Properties.Client.LoadedShapeForEntity;
        if (shape == null) return spars[part] = null;

        var meta = new TesselationMetaData { SelectiveElements = [path], TexSource = entity.GetTextureSource(), TypeForLogging = "entity" };
        capi.Tesselator.TesselateShape(meta, shape, out var meshData);
        return spars[part] = capi.Render.UploadMultiTextureMesh(meshData);
    }

    private static float[] Turned((HullPoint From, HullPoint To) rest, (HullPoint From, HullPoint To) at, double girth)
    {
        float[] Of(HullPoint p) => [(float)p.Along, (float)p.Height, (float)p.Across];
        float[] Rotation(float[] a, float[] b) => Mat4f.FromQuat(Mat4f.Create(), Quaternionf.RotationTo(Quaternionf.Create(), a, b));
        var (u, v, x) = (Of((rest.To - rest.From).Unit), Of((at.To - at.From).Unit), new float[] { 1, 0, 0 });
        var (from, to) = (rest.From + Origin, at.From + Origin);
        return new Matrixf().Translate(to.Along, to.Height, to.Across).Mul(Rotation(u, v)).Mul(Rotation(x, u)).Scale(1, (float)girth, (float)girth)
            .Mul(Rotation(u, x)).Translate(-from.Along, -from.Height, -from.Across).Values;
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

    private TextureAtlasPosition ClothAtlas => clothAtlas ??= capi.Tesselator.GetTextureSource(entity, returnNullWhenMissing: true)[cloth]
        ?? throw new InvalidOperationException($"{entity.Code} has no texture {cloth} for her sail");
}
