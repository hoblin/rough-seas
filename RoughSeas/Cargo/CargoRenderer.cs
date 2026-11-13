using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Cargo;

/// <summary>
/// The piles in a vessel's holds, drawn in her model's frame so they heave, roll and pitch with her.
/// </summary>
public sealed class CargoRenderer : IRenderer
{
    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly float scale;
    private readonly Matrixf model = new();
    private readonly List<(MultiTextureMeshRef Mesh, Vec3f At)> piles = [];

    public CargoRenderer(ICoreClientAPI capi, Entity entity, float scale)
    {
        this.capi = capi;
        this.entity = entity;
        this.scale = scale;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "roughseas-cargo");
    }

    public double RenderOrder => 0.5;

    public int RenderRange => 99;

    public void Show(IEnumerable<(MeshData Mesh, Vec3f AtMetres)> laid)
    {
        Clear();
        foreach (var (mesh, at) in laid) piles.Add((capi.Render.UploadMultiTextureMesh(mesh), at));
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (piles.Count == 0 || !entity.IsRendered || entity.Properties.Client.Renderer is not EntityShapeRenderer hull) return;

        var render = capi.Render;
        var prog = render.PreparedStandardShader((int)entity.Pos.X, (int)Math.Ceiling(entity.Pos.Y), (int)entity.Pos.Z);
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        foreach (var (mesh, at) in piles)
        {
            model.Set(hull.ModelMat).Translate(at.X, at.Y, at.Z).Scale(scale, scale, scale).Translate(-0.5f, 0, -0.5f);
            prog.ModelMatrix = model.Values;
            render.RenderMultiTextureMesh(mesh, "tex");
        }
        prog.Stop();
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        Clear();
    }

    private void Clear()
    {
        foreach (var (mesh, _) in piles) mesh.Dispose();
        piles.Clear();
    }
}
