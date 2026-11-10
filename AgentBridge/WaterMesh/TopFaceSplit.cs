using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AgentBridge.WaterMesh;

internal static class TopFaceSplit
{
    private const int TopFace = 4;
    private const int FullDistance = 255;
    private const int OceanityMask = 0xff << 2;

    private static readonly AccessTools.FieldRef<LiquidTesselator, int[]> ShouldWave = AccessTools.FieldRefAccess<LiquidTesselator, int[]>("shouldWave");
    private static readonly AccessTools.FieldRef<LiquidTesselator, int> LavaFlag = AccessTools.FieldRefAccess<LiquidTesselator, int>("lavaFlag");
    private static readonly AccessTools.FieldRef<LiquidTesselator, int> ExtraFlags = AccessTools.FieldRefAccess<LiquidTesselator, int>("extraFlags");

    public static int Cells { get; set; } = 1;

    public static int SampledLight { get; private set; }

    public static TextureAtlasPosition? SampledTile { get; private set; }

    public static void Attach(Harmony harmony) =>
        harmony.Patch(AccessTools.Method(typeof(LiquidTesselator), "DrawLiquidBlockFace"), new HarmonyMethod(typeof(TopFaceSplit), nameof(Draw)));

    private static bool Draw(LiquidTesselator __instance, TCTCache vars, int tileSide, float northSouthLevel, float southWestLevel, float northEastLevel, float southEastLevel,
        FastVec3f[] quadOffsets, float[] flowVectors, int shouldWaveOffset, float texHeightLeftRel, float texHeightRightRel, MeshData[] meshPools)
    {
        if (tileSide == TopFace && ExtraFlags(__instance) != 0 && vars.block.Code.Path.StartsWith("saltwater"))
        {
            SampledLight = vars.CurrentLightRGBByCorner[0];
            SampledTile = vars.textureAtlasPositionsByTextureSubId[vars.fastBlockTextureSubidsByFace[tileSide]];
        }
        if (Cells <= 1 || tileSide != TopFace || vars.RenderPass != EnumChunkRenderPass.Liquid) return true;

        var tile = vars.textureAtlasPositionsByTextureSubId[vars.fastBlockTextureSubidsByFace[tileSide]];
        var mesh = meshPools[tile.atlasNumber];
        var flags = vars.VertexFlags | BlockFacing.AllVertexFlagsNormals[tileSide] | ExtraFlags(__instance);
        var waves = ShouldWave(__instance);
        var lava = LavaFlag(__instance);
        var colormap = vars.ColorMapData.Value;
        var height = tile.y2 - tile.y1;
        var push = vars.block.PushSpeed / 0.00075f;
        var light = vars.CurrentLightRGBByCorner;
        var (a, b, c, d) = (quadOffsets[4], quadOffsets[5], quadOffsets[6], quadOffsets[7]);
        var first = mesh.VerticesCount;

        for (var row = 0; row <= Cells; row++)
        {
            for (var column = 0; column <= Cells; column++)
            {
                var s = column / (float)Cells;
                var t = row / (float)Cells;
                mesh.AddVertexWithFlags(
                    vars.lx + Blend(a.X, b.X, c.X, d.X, s, t),
                    vars.ly + Blend(a.Y * northSouthLevel, b.Y * southWestLevel, c.Y * northEastLevel, d.Y * southEastLevel, s, t),
                    vars.lz + Blend(a.Z, b.Z, c.Z, d.Z, s, t),
                    Blend(tile.x1, tile.x2, tile.x1, tile.x2, s, t),
                    Blend(tile.y1, tile.y1, tile.y1 + height * texHeightRightRel, tile.y1 + height * texHeightLeftRel, s, t),
                    BlendBytes(light[0], light[1], light[2], light[3], s, t),
                    flags);
                mesh.CustomInts.Add(colormap);
                var nearest = t < 0.5f ? (s < 0.5f ? 1 : 0) : (s < 0.5f ? 3 : 2);
                var oceanity = (int)Blend(vars.OceanityFlagBR & OceanityMask, vars.OceanityFlagBL & OceanityMask, vars.OceanityFlagTR & OceanityMask, vars.OceanityFlagTL & OceanityMask, s, t) & OceanityMask;
                var across = (int)(s * FullDistance) << 10;
                var along = (int)(t * texHeightLeftRel * FullDistance) << 18;
                mesh.CustomInts.Add(waves[shouldWaveOffset + nearest] | across | along | lava | oceanity);
                mesh.CustomFloats.Add(Blend(flowVectors[4], flowVectors[2], flowVectors[6], flowVectors[0], s, t) * push);
                mesh.CustomFloats.Add(Blend(flowVectors[5], flowVectors[3], flowVectors[7], flowVectors[1], s, t) * push);
            }
        }

        for (var row = 0; row < Cells; row++)
        {
            for (var column = 0; column < Cells; column++)
            {
                var near = first + row * (Cells + 1) + column;
                var far = near + Cells + 1;
                mesh.AddIndex(far + 1);
                mesh.AddIndex(near + 1);
                mesh.AddIndex(near);
                mesh.AddIndex(far + 1);
                mesh.AddIndex(near);
                mesh.AddIndex(far);
            }
        }

        vars.UpdateChunkMinMax(vars.lx, vars.ly, vars.lz);
        vars.UpdateChunkMinMax(vars.lx + 1f, vars.ly + 1f, vars.lz + 1f);
        return false;
    }

    private static float Blend(float a, float b, float c, float d, float s, float t) =>
        (a * (1 - s) + b * s) * (1 - t) + (c * (1 - s) + d * s) * t;

    private static int BlendBytes(int a, int b, int c, int d, float s, float t)
    {
        var blended = 0;
        for (var shift = 0; shift < 32; shift += 8)
        {
            blended |= ((int)Blend((a >> shift) & 0xff, (b >> shift) & 0xff, (c >> shift) & 0xff, (d >> shift) & 0xff, s, t) & 0xff) << shift;
        }
        return blended;
    }
}
