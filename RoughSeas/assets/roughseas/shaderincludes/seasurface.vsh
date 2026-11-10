// The other end of RoughSeas/Storms/SeaSurface.cs, SurfaceField.cs and RoughSeas/Surf/: DisplacementAt, FoldAt, Factor, SurfShape and Shoaling compute the same surface.
uniform vec4 seaWaves[SEA_SLOTS];
uniform float seaSteepness;
// highp hides these samplers from the engine's numbering, which must stay vanilla's: see docs/sea-and-weather.md.
uniform highp sampler2D seaHeightMap;
uniform highp sampler2D seaBed;
uniform highp sampler2D seaBedAux;
uniform highp sampler2D seaLagA;
uniform highp sampler2D seaLagB;
uniform vec4 seaHeightArea;
uniform vec4 seaBedArea;
uniform vec4 seaShore;
uniform vec2 seaPlunging;
uniform vec4 seaLagValidA;
uniform vec4 seaLagValidB;

out float seaFoamCrest;
out float seaFoamFold;
out float seaFoamShare;
out vec2 seaFoamWorld;

vec3 seaNormal = vec3(0.0, 1.0, 0.0);
float seaCrest = 0.0;
float seaFold = 0.0;
float seaShare = 0.0;

const float seaGravity = 9.81;

float seaFolding(vec3 squeeze) {
	return 1.0 - (1.0 - squeeze.x) * (1.0 - squeeze.z) + squeeze.y * squeeze.y;
}

float seaWavenumber(float deepNumber, float depth) {
	return deepNumber / sqrt(tanh(deepNumber * depth));
}

float seaShoaling(float deepNumber, float depth) {
	float number = seaWavenumber(deepNumber, depth);
	float kd = min(number * depth, 20.0);
	float groupShare = 0.5 * (1.0 + 2.0 * kd / sinh(2.0 * kd));
	float celerity = sqrt(seaGravity * deepNumber) / number;
	float deepGroup = 0.5 * sqrt(seaGravity / deepNumber);
	return sqrt(deepGroup / (groupShare * celerity));
}

float seaRefraction(float deepNumber, float depth, float cosineOffshore) {
	float sineOffshore = sqrt(max(0.0, 1.0 - cosineOffshore * cosineOffshore));
	float sine = sineOffshore * deepNumber / seaWavenumber(deepNumber, depth);
	float cosine = sqrt(max(0.0, 1.0 - sine * sine));
	return cosine > 0.0 ? sqrt(abs(cosineOffshore) / cosine) : 1.0;
}

float seaLag(int slot, vec4 lagA, vec4 lagB) {
	return slot < 4 ? lagA[slot] * seaLagValidA[slot] : slot < 8 ? lagB[slot - 4] * seaLagValidB[slot - 4] : 0.0;
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
	for (int i = 0; i < 4; i++) {
		float tread = texelFetch(seaBed, clamp(corner - ivec2(i & 1, i >> 1), ivec2(0), ivec2(int(seaBedArea.z) - 1)), 0).x;
		if (tread <= 0.0) rise = min(rise, -tread);
	}
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
	return warped;
}

vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div) {
	vec4 warped = applyVanillaLiquidWarping(windAffected, worldPos, div);
	float share = windAffected ? textureLod(seaHeightMap, (worldPos.xz - seaHeightArea.xy) / seaHeightArea.zw, 0.0).r : 0.0;
	seaShare = share;
	if (share <= 0.0) return warped;

	vec2 bedUv = (worldPos.xz - seaBedArea.xy) / seaBedArea.z;
	bool onBed = seaShore.z > 0.5 && bedUv.x >= 0.0 && bedUv.y >= 0.0 && bedUv.x < 1.0 && bedUv.y < 1.0;
	vec4 bed = onBed ? textureLod(seaBed, bedUv, 0.0) : vec4(1000.0, 0.0, 0.0, 1000000.0);
	vec4 aux = onBed ? textureLod(seaBedAux, bedUv, 0.0) : vec4(0.0);
	vec4 lagA = onBed ? textureLod(seaLagA, bedUv, 0.0) : vec4(0.0);
	vec4 lagB = onBed ? textureLod(seaLagB, bedUv, 0.0) : vec4(0.0);
	if (bed.x <= 0.0) return seaSwash(warped, worldPos, bed, aux, lagA, lagB);
	vec2 texel = vec2(1.0 / seaBedArea.z, 0.0);
	vec4 turnAX = onBed ? (textureLod(seaLagA, bedUv + texel.xy, 0.0) - textureLod(seaLagA, bedUv - texel.xy, 0.0)) / 2.0 : vec4(0.0);
	vec4 turnAZ = onBed ? (textureLod(seaLagA, bedUv + texel.yx, 0.0) - textureLod(seaLagA, bedUv - texel.yx, 0.0)) / 2.0 : vec4(0.0);
	vec4 turnBX = onBed ? (textureLod(seaLagB, bedUv + texel.xy, 0.0) - textureLod(seaLagB, bedUv - texel.xy, 0.0)) / 2.0 : vec4(0.0);
	vec4 turnBZ = onBed ? (textureLod(seaLagB, bedUv + texel.yx, 0.0) - textureLod(seaLagB, bedUv - texel.yx, 0.0)) / 2.0 : vec4(0.0);

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
		float cosine = slope > 0.0 ? -dot(wave.xy, aux.yz) / (deepNumber * slope) : 1.0;
		heights[i] = wave.z * seaShoaling(deepNumber, bed.x) * (cosine > 0.0 ? seaRefraction(deepNumber, bed.x, cosine) : 1.0);
		numbers[i] = seaWavenumber(deepNumber, bed.x);
		offshore += wave.z * wave.z;
		local += heights[i] * heights[i];
	}
	float breaking = 0.0;
	bool plunging = false;
	if (onBed && local > 0.0) {
		float kept = min(1.0, bed.x / seaShore.x / (share * 4.0 * sqrt(local / 2.0)));
		for (int i = 0; i < SEA_SLOTS; i++) heights[i] *= kept;
		breaking = 1.0 - kept;
		float dominantNumber = length(seaWaves[int(seaShore.y)].xy);
		float iribarren = slope / sqrt(share * 4.0 * sqrt(offshore / 2.0) * dominantNumber / 6.2831853);
		plunging = iribarren >= seaPlunging.x && iribarren < seaPlunging.y;
	}
	float slopes = 0.0;
	for (int i = 0; i < SEA_SLOTS; i++) slopes += heights[i] * numbers[i];
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
