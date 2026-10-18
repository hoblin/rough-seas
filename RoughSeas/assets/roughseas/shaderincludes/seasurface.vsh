// The other end of RoughSeas/Storms/SeaSurface.cs and SurfaceField.cs: DisplacementAt, FoldAt and Factor compute the same surface.
uniform vec4 seaWaves[SEA_SLOTS];
uniform float seaSteepness;
uniform sampler2D seaHeightMap;
uniform vec4 seaHeightArea;

out float seaFoamCrest;
out float seaFoamFold;
out float seaFoamShare;
out vec2 seaFoamWorld;

vec3 seaNormal = vec3(0.0, 1.0, 0.0);
float seaCrest = 0.0;
float seaFold = 0.0;
float seaShare = 0.0;

float seaFolding(vec3 squeeze) {
	return 1.0 - (1.0 - squeeze.x) * (1.0 - squeeze.z) + squeeze.y * squeeze.y;
}

vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div) {
	vec4 warped = applyVanillaLiquidWarping(windAffected, worldPos, div);
	float share = windAffected ? textureLod(seaHeightMap, (worldPos.xz - seaHeightArea.xy) / seaHeightArea.zw, 0.0).r * 2.0 : 0.0;
	seaShare = share;
	if (share <= 0.0) return warped;

	vec3 offset = vec3(0.0);
	vec3 normal = vec3(0.0, 1.0, 0.0);
	vec3 squeeze = vec3(0.0);
	vec3 squeezeBehind = vec3(0.0);
	vec3 squeezeFarBehind = vec3(0.0);
	float energy = 0.0;
	float steepest = 0.0;
	for (int i = 0; i < SEA_SLOTS; i++) {
		vec4 wave = seaWaves[i];
		if (wave.z <= 0.0) continue;
		float number = length(wave.xy);
		vec2 direction = wave.xy / number;
		float theta = dot(wave.xy, worldPos.xz) + wave.w;
		float rise = sin(theta);
		float sway = cos(theta);
		offset.y += wave.z * rise;
		offset.xz += seaSteepness * wave.z * sway * direction;
		normal.xz -= direction * number * wave.z * sway;
		normal.y -= seaSteepness * number * wave.z * rise;
		energy += wave.z * wave.z;

		float pinch = seaSteepness * wave.z * number;
		float frequency = sqrt(9.81 * number);
		vec3 shape = vec3(direction.x * direction.x, direction.x * direction.y, direction.y * direction.y) * pinch;
		squeeze += shape * rise;
		squeezeBehind += shape * sin(theta + frequency * SEA_TRAIL_SECONDS);
		squeezeFarBehind += shape * sin(theta + frequency * 2.0 * SEA_TRAIL_SECONDS);
		steepest += pinch;
	}
	warped.xyz += share * offset;
	seaNormal = normalize(mix(vec3(0.0, 1.0, 0.0), normal, share));
	seaCrest = energy > 0.0 ? share * offset.y / sqrt(2.0 * energy) : 0.0;
	seaFold = steepest > 0.0
		? share * max(seaFolding(squeeze), max(0.6 * seaFolding(squeezeBehind), 0.35 * seaFolding(squeezeFarBehind))) / steepest
		: 0.0;
	return warped;
}

vec3 seaTilt(vec3 faceNormal) {
	return faceNormal.y > 0.9 ? seaNormal : faceNormal;
}

