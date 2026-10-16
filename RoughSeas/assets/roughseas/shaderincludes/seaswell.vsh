uniform vec4 seaSwell[8];
uniform float seaSwellSteepness;

vec3 seaSwellAt(vec2 relativeXZ) {
	vec3 offset = vec3(0.0);
	for (int i = 0; i < 8; i++) {
		vec4 wave = seaSwell[i];
		float number = length(wave.xy);
		if (number == 0.0 || wave.z == 0.0) continue;
		float theta = dot(wave.xy, relativeXZ) - wave.w;
		offset.y += wave.z * sin(theta);
		offset.xz += seaSwellSteepness * wave.z * cos(theta) * wave.xy / number;
	}
	return offset;
}

vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div) {
	vec4 warped = applyVanillaLiquidWarping(windAffected, worldPos, div);
	#if WAVINGSTUFF == 1
	float openSea = clamp((5.0 / div - 1.0) / 4.0, 0.0, 1.0);
	if (windAffected && openSea > 0.0) warped.xyz += openSea * seaSwellAt(worldPos.xz);
	#endif
	return warped;
}

