// The other end of RoughSeas/Storms/SeaSurface.cs, SurfaceField.cs and RoughSeas/Surf/: DisplacementAt, FoldAt, Factor, SurfShape and Shoaling compute the same surface.
uniform vec4 seaWaves[SEA_SLOTS];
uniform float seaSteepness;
// highp hides these samplers from the engine's numbering, which must stay vanilla's: see docs/sea-and-weather.md.
uniform highp sampler2D seaHeightMap;
uniform highp sampler2D seaBed;
uniform highp sampler2D seaBedAux;
uniform highp sampler2D seaLagA;
uniform highp sampler2D seaLagB;
uniform highp sampler2D seaFace;
uniform highp sampler2D seaGainA;
uniform highp sampler2D seaGainB;
uniform vec4 seaHeightArea;
uniform vec4 seaBedArea;
uniform vec4 seaShore;
uniform vec2 seaPlunging;
uniform vec4 seaLagValidA;
uniform vec4 seaLagValidB;
uniform float seaFoamDecay;
uniform float seaFaceSlope;
uniform float seaWallSlope;
uniform vec4 seaShort[SEA_SHORT];
uniform vec2 seaMesh;
uniform vec3 seaTerms;
uniform float seaBedEdge;

out float seaFoamCrest;
out float seaFoamFold;
out float seaFoamShare;
out float seaFoamFace;
out vec2 seaFoamWorld;

vec3 seaNormal = vec3(0.0, 1.0, 0.0);
float seaCrest = 0.0;
float seaFold = 0.0;
float seaShare = 0.0;
float seaFaceFoam = 0.0;

const float seaGravity = 9.81;
const float seaStepFootBelow = -1.0;
const float seaFaceStands = 0.75;

float seaFolding(vec3 squeeze) {
	return 1.0 - (1.0 - squeeze.x) * (1.0 - squeeze.z) + squeeze.y * squeeze.y;
}

float seaWavenumber(float deepNumber, float depth) {
	return deepNumber / sqrt(tanh(deepNumber * depth));
}

float seaGain(int slot, vec4 gainA, vec4 gainB) {
	float gain = slot < 4 ? mix(1.0, gainA[slot], seaLagValidA[slot]) : slot < 8 ? mix(1.0, gainB[slot - 4], seaLagValidB[slot - 4]) : 1.0;
	return mix(1.0, gain, seaTerms.z);
}

float seaLag(int slot, vec4 lagA, vec4 lagB) {
	return seaTerms.y * (slot < 4 ? lagA[slot] * seaLagValidA[slot] : slot < 8 ? lagB[slot - 4] * seaLagValidB[slot - 4] : 0.0);
}

vec4 seaSwash(vec4 warped, vec4 worldPos, vec4 bed, vec4 aux, vec4 lagA, vec4 lagB) {
	if (bed.z <= 0.0 || bed.w >= 100000.0) return warped;
	float re = 0.0;
	float im = 0.0;
	float energy = 0.0;
	float dominant = 0.0;
	float frequency = 1.0;
	for (int i = 0; i < SEA_SLOTS; i++) {
		vec4 wave = seaWaves[i];
		if (wave.z <= 0.0) continue;
		float theta = dot(wave.xy, worldPos.xz) + wave.w + seaLag(i, lagA, lagB);
		re += wave.z * cos(theta);
		im += wave.z * sin(theta);
		energy += wave.z * wave.z;
		if (i == int(seaShore.y)) {
			dominant = theta;
			frequency = sqrt(seaGravity * length(wave.xy));
		}
	}
	if (energy <= 0.0) return warped;
	float waveHeight = 2.0 * sqrt(re * re + im * im);
	float twoPercent = sqrt(log(50.0) / 2.0) * 4.0 * sqrt(energy / 2.0);
	float runUp = bed.z * sqrt(waveHeight / twoPercent);
	if (runUp < bed.w) return warped;

	float since = mod(1.5707963 - dominant, 6.2831853) / frequency;
	ivec2 corner = ivec2(floor(worldPos.xz - seaBedArea.xy + 0.5));
	float rise = 1000000.0;
	float highest = 0.0;
	for (int i = 0; i < 4; i++) {
		float tread = texelFetch(seaBed, clamp(corner - ivec2(i & 1, i >> 1), ivec2(0), ivec2(int(seaBedArea.z) - 1)), 0).x;
		if (tread <= 0.0) {
			rise = min(rise, -tread);
			highest = max(highest, -tread);
		}
	}
	if (rise < 100000.0 && highest - rise > seaWallSlope) return warped;
	if (rise >= 100000.0) rise = max(-bed.x, 0.0);
	float distance = bed.y;
	float sine = distance > 0.0 ? rise / sqrt(distance * distance + rise * rise) : 1.0;
	float speed = sqrt(2.0 * seaGravity * runUp);
	float front = speed * since - 0.5 * seaGravity * sine * since * since;
	if (since <= 0.0 || front <= distance) return warped;
	float arrived = sine > 0.0001 ? (speed - sqrt(max(0.0, speed * speed - 2.0 * seaGravity * sine * distance))) / (seaGravity * sine) : distance / speed;
	float depth = min((front - distance) * (front - distance) / (9.0 * seaGravity * since * since), seaShore.w * waveHeight) - aux.x * max(0.0, since - arrived);
	if (depth <= 0.0) return warped;
	warped.y += rise + depth;
	seaShare = 0.0;
	if (seaFoamDecay > 0.0) {
		for (int i = 0; i < 4; i++) {
			if (texelFetch(seaFace, clamp(corner - ivec2(i & 1, i >> 1), ivec2(0), ivec2(int(seaBedArea.z) - 1)), 0).x < seaStepFootBelow) {
				seaFaceFoam = exp(-max(0.0, since - arrived) / seaFoamDecay);
				break;
			}
		}
	}
	return warped;
}

vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div) {
	float share = windAffected ? textureLod(seaHeightMap, (worldPos.xz - seaHeightArea.xy) / seaHeightArea.zw, 0.0).r : 0.0;
	seaRippleKept = 1.0 - share;
	vec4 warped = applyVanillaLiquidWarping(windAffected, worldPos, div);
	seaShare = share;
	if (share <= 0.0) return warped;

	vec2 bedUv = (worldPos.xz - seaBedArea.xy) / seaBedArea.z;
	bool onBed = seaShore.z > 0.5 && bedUv.x >= 0.0 && bedUv.y >= 0.0 && bedUv.x < 1.0 && bedUv.y < 1.0;
	vec4 bed = onBed ? textureLod(seaBed, bedUv, 0.0) : vec4(1000.0, 0.0, 0.0, 1000000.0);
	vec4 aux = onBed ? textureLod(seaBedAux, bedUv, 0.0) : vec4(0.0);
	float bedEdge = onBed ? smoothstep(0.0, 1.0, min(min(bedUv.x, 1.0 - bedUv.x), min(bedUv.y, 1.0 - bedUv.y)) * seaBedArea.z / seaBedEdge) : 0.0;
	vec4 lagA = bedEdge * (onBed ? textureLod(seaLagA, bedUv, 0.0) : vec4(0.0));
	vec4 lagB = bedEdge * (onBed ? textureLod(seaLagB, bedUv, 0.0) : vec4(0.0));
	vec4 face = onBed ? textureLod(seaFace, bedUv, 0.0) : vec4(1000.0, 0.0, 0.0, 0.0);
	vec4 gainA = onBed ? textureLod(seaGainA, bedUv, 0.0) : vec4(1.0);
	vec4 gainB = onBed ? textureLod(seaGainB, bedUv, 0.0) : vec4(1.0);
	if (bed.x <= 0.0) return seaSwash(warped, worldPos, bed, aux, lagA, lagB);
	float faceDistance = max(face.x, 0.0);
	float faceLength = length(face.yz);
	vec2 faceNormal = faceLength > 0.0 ? face.yz / faceLength : vec2(0.0);
	float faceReflects = faceLength > 0.0 ? face.w * seaTerms.x * bedEdge : 0.0;
	vec2 texel = vec2(1.0 / seaBedArea.z, 0.0);
	vec4 turnAX = bedEdge * (onBed ? (textureLod(seaLagA, bedUv + texel.xy, 0.0) - textureLod(seaLagA, bedUv - texel.xy, 0.0)) / 2.0 : vec4(0.0));
	vec4 turnAZ = bedEdge * (onBed ? (textureLod(seaLagA, bedUv + texel.yx, 0.0) - textureLod(seaLagA, bedUv - texel.yx, 0.0)) / 2.0 : vec4(0.0));
	vec4 turnBX = bedEdge * (onBed ? (textureLod(seaLagB, bedUv + texel.xy, 0.0) - textureLod(seaLagB, bedUv - texel.xy, 0.0)) / 2.0 : vec4(0.0));
	vec4 turnBZ = bedEdge * (onBed ? (textureLod(seaLagB, bedUv + texel.yx, 0.0) - textureLod(seaLagB, bedUv - texel.yx, 0.0)) / 2.0 : vec4(0.0));

	float heights[SEA_SLOTS];
	float numbers[SEA_SLOTS];
	float offshore = 0.0;
	float local = 0.0;
	float slope = length(aux.yz);
	for (int i = 0; i < SEA_SLOTS; i++) {
		vec4 wave = seaWaves[i];
		float deepNumber = length(wave.xy);
		heights[i] = 0.0;
		numbers[i] = deepNumber;
		if (wave.z <= 0.0 || deepNumber <= 0.0) continue;
		if (!onBed) {
			heights[i] = wave.z;
			continue;
		}
		heights[i] = wave.z * mix(1.0, seaGain(i, gainA, gainB), bedEdge);
		numbers[i] = mix(deepNumber, seaWavenumber(deepNumber, bed.x), bedEdge);
		offshore += wave.z * wave.z;
		local += heights[i] * heights[i];
	}
	float breaking = 0.0;
	bool plunging = false;
	if (onBed && local > 0.0) {
		breaking = aux.w * bedEdge;
		float dominantNumber = length(seaWaves[int(seaShore.y)].xy);
		float iribarren = slope / sqrt(share * 4.0 * sqrt(offshore / 2.0) * dominantNumber / 6.2831853);
		plunging = iribarren >= seaPlunging.x && iribarren < seaPlunging.y;
	}
	float slopes = 0.0;
	float reflected = 0.0;
	for (int i = 0; i < SEA_SLOTS; i++) {
		slopes += heights[i] * numbers[i];
		if (faceReflects <= 0.0 || heights[i] <= 0.0) continue;
		float into = max(0.0, -dot(normalize(seaWaves[i].xy + vec2(seaLag(i, turnAX, turnBX), seaLag(i, turnAZ, turnBZ))), faceNormal));
		slopes += heights[i] * numbers[i] * faceReflects * into;
		reflected += heights[i] * faceReflects * into;
	}
	float reflectedAllowed = seaFaceSlope * max(0.0, faceDistance - 0.5);
	float reflectedShare = reflected > reflectedAllowed ? reflectedAllowed / reflected : 1.0;
	float cusp = slopes > 0.0 ? 1.0 / slopes : seaSteepness;
	float steepness = min(seaSteepness, cusp);
	if (plunging) steepness += (cusp - steepness) * breaking;

	vec3 offset = vec3(0.0);
	vec3 normal = vec3(0.0, 1.0, 0.0);
	vec3 squeeze = vec3(0.0);
	vec3 squeezeBehind = vec3(0.0);
	vec3 squeezeFarBehind = vec3(0.0);
	float energy = 0.0;
	float steepest = 0.0;
	float incident = 0.0;
	for (int i = 0; i < SEA_SLOTS; i++) {
		vec4 wave = seaWaves[i];
		float height = heights[i];
		if (height <= 0.0) continue;
		float number = numbers[i];
		vec2 direction = normalize(wave.xy + vec2(seaLag(i, turnAX, turnBX), seaLag(i, turnAZ, turnBZ)));
		float theta = dot(wave.xy, worldPos.xz) + wave.w + seaLag(i, lagA, lagB);
		float rise = sin(theta);
		float sway = cos(theta);
		offset.y += height * rise;
		incident += height * rise;
		offset.xz += steepness * height * sway * direction;
		normal.xz -= direction * number * height * sway;
		normal.y -= steepness * number * height * rise;
		energy += height * height;

		float pinch = steepness * height * number;
		float frequency = sqrt(seaGravity * length(wave.xy));
		vec3 shape = vec3(direction.x * direction.x, direction.x * direction.y, direction.y * direction.y) * pinch;
		squeeze += shape * rise;
		squeezeBehind += shape * sin(theta + frequency * SEA_TRAIL_SECONDS);
		squeezeFarBehind += shape * sin(theta + frequency * 2.0 * SEA_TRAIL_SECONDS);
		steepest += pinch;

		float toward = dot(direction, faceNormal);
		if (faceReflects > 0.0 && toward < 0.0) {
			float returned = height * faceReflects * -toward * reflectedShare;
			float mirrored = theta - 2.0 * number * toward * faceDistance;
			vec2 away = direction - 2.0 * toward * faceNormal;
			offset.y += returned * sin(mirrored);
			offset.xz += steepness * returned * cos(mirrored) * away;
			normal.xz -= away * number * returned * cos(mirrored);
			normal.y -= steepness * number * returned * sin(mirrored);
		}
	}
	float shortEnergy = 0.0;
	for (int i = 0; i < SEA_SHORT; i++) shortEnergy += seaShort[i].z * seaShort[i].z;
	float shortKept = onBed && shortEnergy > 0.0 ? min(1.0, bed.x / (seaShore.x * 4.0 * sqrt(shortEnergy / 2.0))) : 1.0;
	float spacing = clamp(length(worldPos.xz) / seaMesh.x, seaMesh.y, 1.0);
	for (int i = 0; i < SEA_SHORT; i++) {
		vec4 wave = seaShort[i];
		if (wave.z <= 0.0) continue;
		float height = wave.z * shortKept * smoothstep(2.0 * spacing, 4.0 * spacing, 6.2831853 / length(wave.xy));
		float theta = dot(wave.xy, worldPos.xz) + wave.w;
		offset.y += height * sin(theta);
		normal.xz -= wave.xy * height * cos(theta);
	}
	warped.xyz += share * offset;
	seaNormal = normalize(mix(vec3(0.0, 1.0, 0.0), normal, share));
	seaCrest = energy > 0.0 ? share * incident / sqrt(2.0 * energy) : 0.0;
	seaFold = steepest > 0.0
		? share * max(seaFolding(squeeze), max(0.6 * seaFolding(squeezeBehind), 0.35 * seaFolding(squeezeFarBehind))) / steepest
		: 0.0;
	int slot = int(seaShore.y);
	vec4 strongest = seaWaves[slot];
	bool incoming = dot(strongest.xy + vec2(seaLag(slot, turnAX, turnBX), seaLag(slot, turnAZ, turnBZ)), faceNormal) < 0.0;
	if (seaFoamDecay > 0.0 && breaking > 0.0 && faceLength > seaFaceStands && incoming && faceDistance <= max(1.0, share * 4.0 * sqrt(local / 2.0) * (1.0 - breaking))) {
		float angular = sqrt(seaGravity * length(strongest.xy));
		float celerity = angular / max(numbers[slot], 0.0001);
		float crestAgo = mod(1.5707963 - (dot(strongest.xy, worldPos.xz) + strongest.w + seaLag(slot, lagA, lagB)), 6.2831853) / angular;
		float sinceStrike = mod(crestAgo - faceDistance / celerity, 6.2831853 / angular);
		if (celerity * sinceStrike >= faceDistance) seaFaceFoam = breaking * exp(-sinceStrike / seaFoamDecay);
	}
	if (seaFoamDecay > 0.0) seaFaceFoam = max(seaFaceFoam, breaking * smoothstep(0.0, 1.0, 0.5 + 0.5 * seaCrest));
	return warped;
}

vec3 seaTilt(vec3 faceNormal) {
	return faceNormal.y > 0.9 ? seaNormal : faceNormal;
}
