using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public sealed class RigRenderer : IRenderer
{
    private const string Cloth = "sail";
    private const string Wood = "frame";
    private const string Cord = "lashing";
    private const int Reaches = 10;
    private const int Spans = 8;
    private const double OriginMetres = 0.5;
    private const double ShuntMilliseconds = 1000;
    private static readonly (float U, float V)[] Tile = [(0, 0), (1, 0), (1, 1), (0, 1)];
    private static readonly (int Facing, int[] Order)[] Sides = [(1, [0, 1, 2, 0, 2, 3]), (-1, [0, 2, 1, 0, 3, 2])];

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly EntityBehaviorWindage windage;
    private readonly Dictionary<string, Layer> meshes = new();
    private readonly Dictionary<string, Layer> blended = new();
    private Dictionary<string, Layer> target;
    private int drawnTack = -1;
    private (RigEnd End, (double Yard, double Boom, double Fill) Trim)? shunted;
    private long shuntedAt;
    private (double Yard, double Boom, double Fill) from;
    private (double Yard, double Boom, double Fill) to;
    private long toAt;
    private long stepMilliseconds = 1;

    public RigRenderer(ICoreClientAPI capi, Entity entity, EntityBehaviorWindage windage)
    {
        this.capi = capi;
        this.entity = entity;
        this.windage = windage;
        foreach (var texture in new[] { Cloth, Wood, Cord }) (meshes[texture], blended[texture]) = (new Layer(), new Layer());
        target = meshes;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "roughseas-rig");
    }

    private Sail Sail => windage.Sail!;

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    public void Trimmed()
    {
        var now = capi.InWorldEllapsedMilliseconds;
        from = Drawn(now);
        stepMilliseconds = Math.Max(1, now - toAt);
        (to, toAt) = ((Sail.State.Yard, Sail.State.Boom, Sail.Depth), now);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull || !windage.Rigged) return;

        windage.Hoist();
        var now = capi.InWorldEllapsedMilliseconds;
        var rig = Sail.Rig;
        if (drawnTack >= 0 && rig.Tacked != drawnTack)
        {
            (shunted, shuntedAt) = ((rig.EndAt(drawnTack), Drawn(now)), now);
            from = to = (Sail.State.Yard, Sail.State.Boom, Sail.Depth);
        }
        drawnTack = rig.Tacked;
        Draw(meshes, rig.End, Drawn(now));
        var share = (now - shuntedAt) / ShuntMilliseconds;
        if (shunted is var (before, trim) && share < 1)
        {
            Draw(blended, before, trim);
            foreach (var (texture, layer) in meshes) Blend(blended[texture].Data, layer.Data, share);
        }

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.ModelMatrix = hull.ModelMat;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        foreach (var (texture, layer) in meshes)
        {
            if (layer.Data.VerticesCount == 0) continue;
            if (layer.Mesh == null || layer.Data.VerticesCount > layer.Uploaded)
            {
                layer.Mesh?.Dispose();
                (layer.Mesh, layer.Uploaded) = (render.UploadMesh(layer.Data), layer.Data.VerticesCount);
            }
            else render.UpdateMesh(layer.Mesh, layer.Data);
            prog.Tex2D = capi.EntityTextureAtlas.AtlasTextures[Atlas(texture).atlasNumber].TextureId;
            render.RenderMesh(layer.Mesh);
        }
        prog.Stop();
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        foreach (var layer in meshes.Values) layer.Mesh?.Dispose();
        meshes.Clear();
    }

    private sealed class Layer
    {
        public readonly MeshData Data = new(64, 96, withNormals: false, withUv: true, withRgba: true, withFlags: true);
        public MeshRef? Mesh;
        public int Uploaded;
    }

    private void Draw(Dictionary<string, Layer> into, RigEnd end, (double Yard, double Boom, double Fill) trim)
    {
        target = into;
        foreach (var layer in into.Values) layer.Data.Clear();
        if (Sail.SquareMetres > 0) Hoisted(end, trim);
        else Lowered(end);
        target = meshes;
    }

    private static void Blend(MeshData before, MeshData after, double share)
    {
        if (before.VerticesCount != after.VerticesCount) return;
        for (var k = 0; k < after.VerticesCount * 3; k++) after.xyz[k] = (float)(before.xyz[k] + (after.xyz[k] - before.xyz[k]) * share);
    }

    private (double Yard, double Boom, double Fill) Drawn(long now)
    {
        var share = Math.Clamp((now - toAt) / (double)stepMilliseconds, 0, 1);
        double Between(double a, double b) => a + (b - a) * share;
        return (Between(from.Yard, to.Yard), Between(from.Boom, to.Boom), Between(from.Fill, to.Fill));
    }

    private void Hoisted(RigEnd end, (double Yard, double Boom, double Fill) trim)
    {
        var state = end.Pose(trim.Yard, trim.Boom);
        var pose = end.Hoisted;
        var set = Sail.Set;
        Weave(state.Form, trim.Fill, set);
        var tack = pose.Tack;
        var foot = (state.Form.At(1, 0, 0, set) - tack).Unit;
        var clew = tack + (pose.Clew - tack).Length * foot;
        var bridle = tack + (pose.Bridle - tack).Length * foot;
        var widths = end.Widths;
        Standing(pose, widths);
        Tube(Wood, tack, state.Peak, widths.Spar);
        Tube(Wood, tack, clew, widths.Spar);
        if (set.Span < 1) Tube(Cloth, tack, clew, widths.Roll * Math.Sqrt(1 - set.Span));
        Tube(Cord, pose.Masthead, state.Bend, widths.Line);
        Tube(Cord, bridle, pose.Lead, widths.Line);
    }

    private void Lowered(RigEnd end)
    {
        var (pose, widths) = (end.Lowered, end.Widths);
        Standing(pose, widths);
        Tube(Wood, pose.Tack, pose.Peak, widths.Spar);
        Tube(Wood, pose.Tack, pose.Clew, widths.Spar);
        Tube(Cloth, pose.Tack, pose.Clew, widths.Roll);
        Tube(Cord, pose.Masthead, pose.HalyardBend, widths.Line);
        Tube(Cord, pose.Bridle, pose.Lead, widths.Line);
    }

    private void Standing(RigPose pose, RigWidths widths)
    {
        Tube(Wood, pose.Heel, pose.Masthead, widths.Pole);
        Tube(Wood, pose.ShoreFoot, pose.ShoreTop, widths.Shore);
        Tube(Cord, pose.Masthead, pose.Tack, widths.Line);
        Tube(Cord, pose.Masthead, pose.SternPin, widths.Line);
        Tube(Cord, pose.Masthead, pose.ShroudFoot, widths.Line);
    }

    private void Weave(SailForm form, double fill, (double Reach, double Span) set)
    {
        HullPoint At(int i, int j) => form.At((double)i / Reaches, (double)j / Spans, fill, set);
        for (var i = 0; i < Reaches; i++)
        for (var j = 0; j < Spans; j++) Quad(Cloth, [At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1)]);
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
        var data = target[texture].Data;
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

    private TextureAtlasPosition Atlas(string texture) => capi.Tesselator.GetTextureSource(entity)[texture];
}
