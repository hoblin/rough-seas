// The other end of RoughSeas/Surf/SurfField.cs and RoughSeas/Storms/SurfaceShader.cs: sand the swash wets, darkened while it drains.
uniform highp sampler2D seaBed;
uniform highp sampler2D seaBedAux;
uniform vec4 seaBedArea;
uniform vec4 seaWet;

float seaWetShade(vec4 at) {
	if (seaWet.w < 0.5) return 1.0;
	ivec2 texel = ivec2(floor(at.xz - seaBedArea.xy));
	if (texel.x < 0 || texel.y < 0 || texel.x >= int(seaBedArea.z) || texel.y >= int(seaBedArea.z)) return 1.0;
	vec4 bed = texelFetch(seaBed, texel, 0);
	if (bed.x > 0.0 || bed.z <= 0.0 || bed.w >= 100000.0) return 1.0;
	vec4 aux = texelFetch(seaBedAux, texel, 0);
	if (abs(at.y + seaBedArea.w - aux.w) > 0.05) return 1.0;
	float reach = bed.w / bed.z;
	float chance = exp(-log(50.0) * reach * reach);
	float drain = aux.x > 0.0 ? 2.0 * bed.z / seaWet.y / aux.x : 1000000.0;
	return mix(1.0, seaWet.z, clamp(drain * chance / seaWet.x, 0.0, 1.0));
}
