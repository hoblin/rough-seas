using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class ShaderNeedsTests
{
    private const string Opaque = """
        in vec3 normal;
        in vec4 worldPos;
        layout(location = 0) out vec4 outColor;
        vec4 applyReflectiveEffect(vec4 texColor, vec3 normal, vec4 worldPos) { return texColor; }
        void main()
        {
            vec4 texColor = vec4(1.0);
            outColor = applyFogAndShadowFromBrightness(texColor, 0.0, 1.0, worldPos.xyz);
        }
        """;

    private static readonly (string, string)[] WetSand = [("vec3", "normal"), ("vec4", "worldPos"), ("vec4", "texColor")];

    [Fact]
    public void VanillasOpaqueShaderHoldsEverythingWetSandReads()
    {
        Assert.Empty(ShaderNeeds.Missing(Opaque, WetSand, "void main()", "outColor = applyFogAndShadowFromBrightness(texColor,"));
    }

    [Fact]
    public void AMainDeclaredWithVoidIsMissingTheAnchor()
    {
        Assert.Equal(["'void main()'"], ShaderNeeds.Missing(Opaque.Replace("void main()", "void main(void)"), WetSand, "void main()"));
    }

    [Fact]
    public void ADeclarationOfAnotherTypeIsMissing()
    {
        Assert.Equal(["vec4 worldPos"], ShaderNeeds.Missing(Opaque.Replace("in vec4 worldPos;", "in vec3 worldPos;"), WetSand));
    }

    [Fact]
    public void AnIncludedFunctionsParameterIsNoDeclaration()
    {
        Assert.Equal(["vec3 normal"], ShaderNeeds.Missing(Opaque.Replace("in vec3 normal;", ""), WetSand));
    }

    [Fact]
    public void AnAttributeDeclaredByLayoutCounts()
    {
        Assert.Empty(ShaderNeeds.Missing("layout(location = 6) in int waterFlagsIn;\nvoid main(void) {}", [("int", "waterFlagsIn")], "void main(void)"));
    }
}
