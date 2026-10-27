// Glow: warm halo around light sources, ignite wave, screen flash.
shader_type canvas_item;
render_mode blend_add;
#include "res://Assets/Shaders/Lighting/lights_common.gdshaderinc"

void fragment(){
  vec2 w = cam + (UV - 0.5) * view / zoom;
  vec3 acc = vec3(0.0);
  for (int i = 0; i < 96; i++) {
    if (i >= count) break;
    vec4 L = lights[i];
    if (L.z < 1.0 || L.w < 0.0) continue;
    float d = distance(w, L.xy);
    float t = clamp(1.0 - d / L.z, 0.0, 1.0);
    acc += vec3(1.0, 0.68, 0.30) * t * t * 0.20;
    float ring = exp(-pow((d - L.z) / 6.0, 2.0)) * L.w;
    acc += vec3(1.0, 0.86, 0.45) * ring * 0.9;
  }
  acc += vec3(flash * 0.45);
  COLOR = vec4(acc, 1.0);
}
