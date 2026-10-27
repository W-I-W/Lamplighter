// Wind sway for trees, bushes and grass drawn as TileMapLayer tiles.
// Only tiles listed in amp[] move (indexed by the atlas column of the tile); every other tile is drawn unchanged, so the layer itself is never deformed.
// The top of a sprite moves most, the bottom stays put; displacement is rounded to whole pixels to keep the pixel grid intact.
shader_type canvas_item;

uniform sampler2D noise_tex : filter_linear, repeat_enable;
uniform float amp[140];            // max sway in pixels per atlas column (0 = static tile)
uniform float wind = 1.0;          // overall wind strength (config * player setting)
uniform float time_s = 0.0;
uniform vec2 player = vec2(-99999.0);   // world position of the player (grass bends away)
uniform vec4 shakes[8];            // x, y = tile coords, z = amplitude (px, already decayed), w = phase
varying vec2 wpos;

float hash(vec2 p) { return fract(sin(dot(p, vec2(41.7, 289.3))) * 4375.5453); }

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 atlas = 1.0 / TEXTURE_PIXEL_SIZE;
	float col = floor(UV.x * atlas.x / 16.0 + 0.001);
	float a = amp[int(col)];
	vec2 uv = UV;
	if (a > 0.0) {
		vec2 cell = floor(wpos / 16.0);
		float ph = hash(cell) * 6.2831;
		float sv = 0.75 + 0.5 * hash(cell + 7.0);
		float gust = texture(noise_tex, wpos / 700.0 + vec2(time_s * 0.012, 0.0)).r;
		float strength = wind * (0.45 + 1.1 * smoothstep(0.35, 0.8, gust));
		float sway = sin(time_s * 1.7 * sv + ph + wpos.x * 0.015) * strength + 0.35 * sin(time_s * 3.1 * sv + ph * 1.7) * strength;
		float ly = fract(UV.y * atlas.y / 16.0);                 // 0 = top of the tile, 1 = bottom
		float lift = pow(clamp(1.0 - ly, 0.0, 1.0), 1.6);
		float shift = a * sway;
		// pushed by the player
		vec2 centre = (cell + 0.5) * 16.0;
		vec2 d = centre - player;
		float dl = length(d);
		if (dl < 14.0) shift += sign(d.x) * (1.0 - dl / 14.0) * min(a, 2.5) * 1.6 * wind;
		// axe/pickaxe hits
		for (int i = 0; i < 8; i++) {
			vec4 s = shakes[i];
			if (s.z > 0.0 && abs(s.x - cell.x) < 0.5 && abs(s.y - cell.y) < 0.5) shift += s.z * sin(time_s * 48.0 + s.w);
		}
		float px = floor(shift * lift + 0.5);
		uv.x = UV.x - px / atlas.x;
		float lo = col * 16.0 / atlas.x;
		float hi = (col + 1.0) * 16.0 / atlas.x;
		if (uv.x < lo || uv.x >= hi) { COLOR = vec4(0.0); }
		else { COLOR = texture(TEXTURE, uv); }
	} else {
		COLOR = texture(TEXTURE, UV);
	}
}
