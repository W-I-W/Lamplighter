// Wind sway for trees, bushes and grass drawn as TileMapLayer tiles.
// Only tiles listed in amp[] move (indexed by the atlas column of the tile); every other tile is drawn unchanged, so the layer itself is never deformed.
// The top of a sprite moves most, the bottom stays put; displacement is rounded to whole pixels to keep the pixel grid intact.
shader_type canvas_item;

uniform sampler2D noise_tex : filter_linear, repeat_enable;
uniform sampler2D tile_data : filter_nearest, repeat_disable;   // one texel per atlas column: r = max sway (px), g = first opaque row (sprite top), b = glow
uniform float wind = 1.0;          // overall wind strength (config * player setting)
uniform vec2 player = vec2(-99999.0);   // world position of the player (grass bends away)
uniform vec4 shakes[8];            // x, y = tile coords, z = amplitude (px, already decayed), w = phase
varying vec2 wpos;
varying vec4 vcol;                 // модуляция слоя (прозрачность крон за игроком)

float hash(vec2 p) { return fract(sin(dot(p, vec2(41.7, 289.3))) * 4375.5453); }

void vertex() {
	vcol = COLOR;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 atlas = vec2(textureSize(TEXTURE, 0));
	float col = floor(UV.x * atlas.x / 16.0 + 0.001);
	vec4 td = texture(tile_data, vec2((col + 0.5) / 192.0, 0.5));
	float a = td.r;
	vec2 uv = UV;
	if (a > 0.0) {
		vec2 cell = floor(wpos / 16.0);
		float ph = hash(cell) * 6.2831;
		float sv = 0.75 + 0.5 * hash(cell + 7.0);
		float gust = texture(noise_tex, wpos / 700.0 + vec2(TIME * 0.012, 0.0)).r;
		float strength = wind * (0.45 + 1.1 * smoothstep(0.35, 0.8, gust));
		float sway = sin(TIME * 1.7 * sv + ph + wpos.x * 0.015) * strength + 0.35 * sin(TIME * 3.1 * sv + ph * 1.7) * strength;
		float ly = fract(UV.y * atlas.y / 16.0);                 // 0 = top of the tile, 1 = bottom
		// the base of the plant stays fixed, the tip moves most: 0 at the bottom row, 1 at the first opaque row of the sprite
		float tr = td.g / 16.0;
		// bend curve taken from the CapyCam "Tree & Grass Motion" shader (smoothstep * power), normalised so the tip of the sprite = 1 and the root = 0
		float yy = clamp((ly - tr) / max(1.0 - tr, 0.1), 0.0, 1.0);          // 0 at the first opaque row, 1 at the bottom
		float lift = smoothstep(0.3, 1.3, pow(1.0 - yy, 0.5)) * pow(2.0 - yy, 1.5) / (0.784 * 2.83);

		float shift = a * sway;
		// pushed by the player
		vec2 centre = (cell + 0.5) * 16.0;
		vec2 d = centre - player;
		float dl = length(d);
		if (dl < 14.0) shift += sign(d.x) * (1.0 - dl / 14.0) * min(a, 2.5) * 1.6 * wind;
		// axe/pickaxe hits
		for (int i = 0; i < 8; i++) {
			vec4 s = shakes[i];
			if (s.z > 0.0 && abs(s.x - cell.x) < 0.5 && abs(s.y - cell.y) < 0.5) shift += s.z * sin(TIME * 48.0 + s.w);
		}
		if (td.a > 0.0) {
			// hand-drawn poses: pick the upright tile or one of four bent tiles stored next to it in the atlas (whole sprite pixels, no distortion)
			float k = clamp(floor(shift * 2.0 / a + 0.5), -2.0, 2.0);
			float pose_col = k == 0.0 ? col : td.a + (k < -1.5 ? 0.0 : k < 0.0 ? 1.0 : k < 1.5 ? 2.0 : 3.0);
			uv.x = UV.x + (pose_col - col) * 16.0 / atlas.x;
			COLOR = texture(TEXTURE, uv) * vcol;
		} else {
		float px = shift * lift;          // fractional on purpose: with nearest sampling and a 3x zoom the blade edges then move in single screen pixels (smooth), not in 3-pixel jumps
		uv.x = UV.x - px / atlas.x;
		float lo = col * 16.0 / atlas.x + 0.5 / atlas.x;
		float hi = (col + 1.0) * 16.0 / atlas.x - 0.5 / atlas.x;
		uv.x = clamp(uv.x, lo, hi);                                // edge pixels are repeated instead of cut off, so the silhouette never gets sliced
		COLOR = texture(TEXTURE, uv) * vcol;
		}
	} else {
		COLOR = texture(TEXTURE, UV) * vcol;
	}
	float g = td.b;
	if (g > 0.0) {
		float ph = hash(floor(wpos / 16.0)) * 6.2831;
		float pulse = 0.5 + 0.5 * sin(TIME * 2.0 + ph);
		COLOR.rgb *= 1.0 + g * (0.25 + 0.75 * pulse);
		COLOR.rgb += vec3(0.10, 0.09, 0.05) * g * pulse * COLOR.a;
	}
}
