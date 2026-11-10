// The other end of RoughSeas/Storms/SeaSurface.cs and SurfaceShader.cs, which set these uniforms.
uniform vec3 seaFoam;
uniform vec2 seaWavesToward;
uniform float seaFoamClock;
uniform float seaFoamDecay;

in float seaFoamCrest;
in float seaFoamFold;
in float seaFoamShare;
in float seaFoamBroken;
in vec2 seaFoamWorld;
in vec2 seaFoamCrestTime;
in vec4 seaFoamBrokenAt;

const float seaRollerSeconds = 0.4;
const float seaLaceSpread = 6.2;

float seaBrokenFoam(float share, float delay) {
	float period = 6.2831853 / seaFoamCrestTime.y;
	float since = mod(1.5707963 - seaFoamCrestTime.x, 6.2831853) / seaFoamCrestTime.y;
	float ragged = seaRollerSeconds * gnoise(vec3(seaFoamWorld / 3.0, seaFoamClock * 0.1));
	return share * exp(-mod(since - delay - ragged, period) / seaFoamDecay);
}

float seaFoamFaceAt() {
	if (seaFoamDecay <= 0.0 || seaFoamCrestTime.y <= 0.0) return 0.0;
	return max(seaBrokenFoam(seaFoamBrokenAt.x, seaFoamBrokenAt.y), seaBrokenFoam(seaFoamBrokenAt.z, seaFoamBrokenAt.w));
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

float seaBrokenLace(float cover, float along, float side, float t) {
	float n = gnoise(vec3(along / 1.0, side / 0.45, t * 0.2)) * 0.5 + gnoise(vec3(along / 0.35, side / 0.16, t * 0.35)) * 0.3
		+ gnoise(vec3(along / 0.12, side / 0.07, t * 0.5)) * 0.2;
	return 1.0 - smoothstep(cover - 0.04, cover + 0.04, tanh(seaLaceSpread * abs(n)));
}

float seaFoamAt() {
	float seaFoamFace = seaFoamFaceAt();
	if (seaFoamShare <= 0.0 && seaFoamFace <= 0.0) return 0.0;

	vec2 at = seaFoamWorld;
	float t = seaFoamClock;
	vec2 across = vec2(-seaWavesToward.y, seaWavesToward.x);
	float along = dot(at, seaWavesToward);
	float side = dot(at, across);
	float lace = seaLace(at, t);
	float offshore = smoothstep(0.5, 0.9, seaFoamShare) * (1.0 - seaFoamBroken);
	float foam = 0.0;

	if (offshore > 0.0) {
		if (seaFoamFold > seaFoam.x) {
			float breakingCrests = smoothstep(0.52, 0.62, gnoise(vec3(along / 3.0 - t * 0.4, side / 2.5, t * 0.2)) * 0.5 + 0.5);
			float breaking = smoothstep(seaFoam.x, seaFoam.x + 0.2, seaFoamFold) * breakingCrests;
			foam = 0.75 * smoothstep(lace - 0.3, lace + 0.3, breaking * 1.1);
		}
		if (seaFoam.y > 0.0) {
			float wander = gnoise(vec3(along / 40.0, side / 40.0, t * 0.03)) * 8.0;
			float width = 0.01 + 0.05 * seaFoam.y;
			float veins = max(seaVeins(along, side + wander, 1.0, width, t), 0.7 * seaVeins(along + 31.0, side + wander * 0.5, 0.4, width, t));
			float patchy = smoothstep(0.42, 0.6, gnoise(vec3((along - t * 1.5) / 14.0, side / 6.0, t * 0.05)) * 0.5 + 0.5);
			foam = max(foam, 0.5 * seaFlecked(veins * patchy * 1.2, lace));
		}
		if (seaFoam.z > 0.0) {
			foam = max(foam, 0.85 * seaFlecked(seaFoam.z * (0.6 + 0.6 * smoothstep(-0.2, 0.8, seaFoamCrest)), lace));
		}
		foam *= offshore;
	}

	if (seaFoamFace > 0.0) foam = max(foam, seaBrokenLace(seaFoamFace, along, side, t));

	return clamp(foam, 0.0, 1.0);
}
