// Animated water for the ground TileMapLayer: only the water tile column changes, every other tile is drawn untouched.
// Pixel waves: brightness is quantised into three bands that drift across the surface, plus a few white sparkle pixels. No smooth gradients.
shader_type canvas_item;

uniform sampler2D noise_tex : filter_linear, repeat_enable;
uniform int water_col = 43;              // atlas column of the water tile
uniform float time_s = 0.0;
uniform float strength = 1.0;            // Settings.Fx
varying vec2 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 base = texture(TEXTURE, UV);
	vec2 atlas = 1.0 / TEXTURE_PIXEL_SIZE;
	int col = int(floor(UV.x * atlas.x / 16.0 + 0.001));
	if (col != water_col) { COLOR = base; }
	else {
		vec2 px = floor(wpos);                                        // work on whole world pixels so waves never shimmer at sub-pixel level
		float w = sin(px.x * 0.22 + px.y * 0.31 + time_s * 1.2) + sin(px.x * 0.11 - px.y * 0.19 + time_s * 0.7);
		float n = texture(noise_tex, px / 64.0 + vec2(time_s * 0.01, 0.0)).r;
		float band = floor((w * 0.25 + n * 0.5) * 3.0) / 3.0;          // three brightness steps
		vec3 c = base.rgb * (1.0 + (band - 0.25) * 0.28 * strength);
		c = mix(c, vec3(0.18, 0.42, 0.62), 0.10 * strength * step(0.5, fract(band * 3.0 + time_s * 0.05)));   // slow hue drift
		float sp = step(0.985, fract(sin(dot(px + floor(time_s * 1.5), vec2(12.9898, 78.233))) * 43758.5453));
		c += vec3(0.5) * sp * strength;                                  // sparkle pixels
		COLOR = vec4(c, base.a);
	}
}
