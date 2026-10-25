using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// The water aboard, one level of the shape's <c>Bilge</c> group: it follows the hull's position and heading but not
/// its roll and pitch, so it stays level with the sea.
/// </summary>
public sealed class BilgeRenderer : IRenderer
{
    private const string LevelsElement = "Bilge";
    private static readonly Vec4f Tint = new(0.7f, 0.82f, 0.82f, 0.8f);

    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly Matrixf model = new();
    private readonly Vec4f middle = new();
    private readonly Vec4f pivot = new();
    private MultiTextureMeshRef? mesh;
    private int? levels;

    public BilgeRenderer(ICoreClientAPI capi, Entity entity)
    {
        this.capi = capi;
        this.entity = entity;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "roughseas-bilge");
    }

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    public int Levels => (levels ??= entity.Properties.Client.LoadedShapeForEntity?.GetElementByName(LevelsElement)?.Children?.Length) ?? 0;

    public int Level { get; private set; }

    public double HeightOf(int level) =>
        entity.Properties.Client.LoadedShapeForEntity?.GetElementByName(LevelsElement)?.Children?[level - 1].Children?[0].From?[1] / HullShape.VoxelsPerBlock ?? 0;

    public void Show(int level)
    {
        if (level == Level && (mesh != null || level == 0)) return;

        Level = level;
        mesh?.Dispose();
        mesh = null;
        if (level == 0 || entity.Properties.Client.LoadedShapeForEntity is not { } shape) return;

        var meta = new TesselationMetaData
        {
            SelectiveElements = [$"{LevelsElement}/{LevelsElement}{level}/*"],
            TexSource = capi.Tesselator.GetTextureSource(entity),
            TypeForLogging = "entity"
        };
        capi.Tesselator.TesselateShape(meta, shape, out var meshData);
        mesh = capi.Render.UploadMultiTextureMesh(meshData);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (mesh == null || !entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull) return;

        float half = entity.SelectionBox.Y2 / 2;
        middle.Set(0.5f, half, 0.5f, 1);
        Mat4f.MulWithVec4(hull.ModelMat, middle, pivot);
        float yaw = entity.Pos.Yaw + ((entity.Properties.Client.Shape?.rotateY ?? 0) + 90) * GameMath.DEG2RAD + hull.yangle;
        model.Identity().Translate(pivot.X, pivot.Y, pivot.Z).RotateY(yaw).Translate(-0.5f, -half, -0.5f);

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.ModelMatrix = model.Values;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        prog.RgbaTint = Tint;
        render.GlToggleBlend(true);
        render.GlDisableCullFace();
        render.RenderMultiTextureMesh(mesh, "tex");
        render.GlEnableCullFace();
        prog.Stop();
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        mesh?.Dispose();
        mesh = null;
    }
}
