using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public sealed class SailRenderer : IRenderer
{
    private const string SailKey = "sailPosition";
    private const string Texture = "sail";
    private const int Reaches = 10;
    private const int Spans = 8;
    private const int TrimFramesAside = 90;
    private const double OriginMetres = 0.5;
    private static readonly (float U, float V)[] Tile = [(0, 0), (1, 0), (1, 1), (0, 1)];
    private static readonly (int Facing, int[] Order)[] Sides = [(1, [0, 1, 2, 0, 2, 3]), (-1, [0, 2, 1, 0, 3, 2])];

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly Sail sail;
    private readonly string trimAnimation;
    private readonly MeshData data = new(Reaches * Spans * 8, Reaches * Spans * 12, withNormals: false, withUv: true, withRgba: true, withFlags: true);
    private MeshRef? mesh;
    private (double Trim, double Fill) from;
    private (double Trim, double Fill) to;
    private long toAt;
    private long stepMilliseconds = 1;

    public SailRenderer(ICoreClientAPI capi, Entity entity, Sail sail, string trimAnimation)
    {
        this.capi = capi;
        this.entity = entity;
        this.sail = sail;
        this.trimAnimation = trimAnimation;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "roughseas-sail");
    }

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    public void Trimmed()
    {
        var now = capi.InWorldEllapsedMilliseconds;
        from = Drawn(now);
        stepMilliseconds = Math.Max(1, now - toAt);
        (to, toAt) = ((sail.Trim, sail.Fill), now);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (entity.WatchedAttributes.GetInt(SailKey) == 0 || !entity.Swimming)
        {
            if (entity.AnimManager.IsAnimationActive(trimAnimation)) entity.AnimManager.StopAnimation(trimAnimation);
            return;
        }
        if (!entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull) return;

        var (trim, fill) = Drawn(capi.InWorldEllapsedMilliseconds);
        Swing(trim);
        Weave(trim, fill);

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.Tex2D = capi.EntityTextureAtlas.AtlasTextures[Atlas().atlasNumber].TextureId;
        prog.ModelMatrix = hull.ModelMat;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        render.RenderMesh(mesh);
        prog.Stop();
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        mesh?.Dispose();
        mesh = null;
    }

    private (double Trim, double Fill) Drawn(long now)
    {
        var share = Math.Clamp((now - toAt) / (double)stepMilliseconds, 0, 1);
        return (from.Trim + (to.Trim - from.Trim) * share, from.Fill + (to.Fill - from.Fill) * share);
    }

    private void Swing(double trim)
    {
        var animations = entity.AnimManager;
        if (animations.Animator == null) return;
        if (!animations.IsAnimationActive(trimAnimation)) animations.StartAnimation(trimAnimation);
        if (animations.GetAnimationState(trimAnimation) is { } state) state.CurrentFrame = (float)(TrimFramesAside + trim * GameMath.RAD2DEG);
    }

    private TextureAtlasPosition Atlas() => capi.Tesselator.GetTextureSource(entity)[Texture];

    private void Weave(double trim, double fill)
    {
        var atlas = Atlas();
        var (width, height) = (atlas.x2 - atlas.x1, atlas.y2 - atlas.y1);
        data.Clear();
        var reef = sail.Reef;
        for (var i = 0; i < Reaches; i++)
        for (var j = 0; j < Spans; j++)
        {
            var corners = new[]
            {
                sail.Form.At(reef * i / Reaches, (double)j / Spans, trim, fill), sail.Form.At(reef * (i + 1) / Reaches, (double)j / Spans, trim, fill),
                sail.Form.At(reef * (i + 1) / Reaches, (double)(j + 1) / Spans, trim, fill), sail.Form.At(reef * i / Reaches, (double)(j + 1) / Spans, trim, fill)
            };
            var normal = Normal(corners);
            foreach (var (facing, order) in Sides)
            {
                var flags = VertexFlags.PackNormal(normal * facing);
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
        if (mesh == null) mesh = capi.Render.UploadMesh(data);
        else capi.Render.UpdateMesh(mesh, data);
    }

    private static Vec3d Normal(HullPoint[] corners)
    {
        var (a, b) = (corners[2] - corners[0], corners[3] - corners[1]);
        var normal = new Vec3d(a.Height * b.Across - a.Across * b.Height, a.Across * b.Along - a.Along * b.Across, a.Along * b.Height - a.Height * b.Along);
        var length = normal.Length();
        return length > 0 ? normal / length : new Vec3d(0, 0, 1);
    }
}
