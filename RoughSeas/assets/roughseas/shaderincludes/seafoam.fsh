uniform vec4 seaFoam;
uniform vec2 seaWind;

in float seaFoamCrest;
in float seaFoamFold;
in float seaFoamOcean;

float seaWaterDepth() {
	vec2 screen = gl_FragCoord.xy / frameSize;
	float through = (linearDepth(texture(depthTex, screen).x) - linearDepth(gl_FragCoord.z)) * zFar;
	return max(0.0, through * abs(normalize(fWorldPos).y));
}

float seaLace(vec2 at, float t) {
	return gnoise(vec3(at * 2.3, t * 0.6)) * 0.3 + gnoise(vec3(at * 6.5, t)) * 0.25 + 0.5;
}

float seaFlecked(float density, float lace) {
	return smoothstep(lace - 0.15, lace + 0.15, density);
}

float seaVeins(float along, float side, float scale, float width, float t) {
	float line = abs(gnoise(vec3((along - t * 1.5) / (9.0 * scale), side / (1.3 * scale), t * 0.05)));
	return 1.0 - clamp(line / width, 0.0, 1.0);
}

float seaFoamAt(vec2 at) {
	float t = waterWaveCounter;
	vec2 across = vec2(-seaWind.y, seaWind.x);
	float along = dot(at, seaWind);
	float side = dot(at, across);
	float lace = seaLace(at, t);
	float openSea = smoothstep(0.5, 0.9, seaFoamOcean);

	float breakingCrests = smoothstep(0.52, 0.62, gnoise(vec3(along / 3.0 - t * 0.4, side / 2.5, t * 0.2)) * 0.5 + 0.5);
	float breaking = smoothstep(seaFoam.x, seaFoam.x + 0.2, seaFoamFold) * breakingCrests;
	float caps = 0.75 * smoothstep(lace - 0.3, lace + 0.3, breaking * 1.1);

	float wander = gnoise(vec3(along / 40.0, side / 40.0, t * 0.03)) * 8.0;
	float width = 0.01 + 0.05 * seaFoam.y;
	float veins = max(seaVeins(along, side + wander, 1.0, width, t), 0.7 * seaVeins(along + 31.0, side + wander * 0.5, 0.4, width, t));
	float patchy = smoothstep(0.42, 0.6, gnoise(vec3((along - t * 1.5) / 14.0, side / 6.0, t * 0.05)) * 0.5 + 0.5);
	float streaks = seaFoam.y > 0.0 ? 0.5 * seaFlecked(veins * patchy * 1.2, lace) : 0.0;

	float spray = seaFlecked(seaFoam.z * (0.6 + 0.6 * smoothstep(-0.2, 0.8, seaFoamCrest)), lace) * 0.85;

	float surf = 0.0;
	float breakDepth = 1.28 * max(seaFoam.w, 0.6);
	if (seaFoamOcean > 0.0) {
		float depth = max(0.0, seaWaterDepth() + 1.6 * gnoise(vec3(at * 0.5, t * 0.3)) + 0.9 * gnoise(vec3(at * 1.7, t * 0.5)));
		if (depth < breakDepth) {
			float shore = 1.0 - depth / breakDepth;
			float bands = fract(shore * 3.0 - t * 0.6 + 0.15 * gnoise(vec3(at * 0.3, t * 0.2)));
			float band = smoothstep(0.0, 0.12, bands) * (1.0 - smoothstep(0.3, 0.75, bands));
			float swash = smoothstep(0.75, 0.95, shore);
			float density = max(band * (0.6 + shore), swash * 1.4);
			surf = seaFlecked(density, lace * 0.8) * smoothstep(0.02, 0.15, seaFoamOcean);
		}
	}

	return clamp(max(openSea * max(caps, max(streaks, spray)), surf), 0.0, 1.0);
}

