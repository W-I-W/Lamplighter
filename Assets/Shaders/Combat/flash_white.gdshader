// Hit flash: draws the sprite as a white silhouette with additive blending. Strength comes from the modulate alpha of each draw call.
shader_type canvas_item;
render_mode blend_add;

void fragment() {
	float a = texture(TEXTURE, UV).a;
	COLOR = vec4(vec3(1.0), a * COLOR.a);
}
