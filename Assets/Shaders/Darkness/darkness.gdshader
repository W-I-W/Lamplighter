// Darkness: dims everything except lit areas; w<0 = faint silhouette marks; smoke = grey haze.
shader_type canvas_item;
#include "res://Assets/Shaders/Lighting/lights_common.gdshaderinc"

void fragment(){
  vec2 w = cam + (UV - 0.5) * view / zoom;
  float n = vn(w * 0.07 + vec2(time * 0.12, -time * 0.09)) - 0.5;
  float n2 = vn(w * 0.21 - vec2(time * 0.2, 0.0)) - 0.5;
  float lit = 0.0;
  for (int i = 0; i < 96; i++) {
    if (i >= count) break;
    vec4 L = lights[i];
    if (L.z < 1.0) continue;
    float d = distance(w, L.xy) + (n * 0.28 + n2 * 0.1) * L.z;
    float a = 1.0 - smoothstep(L.z * 0.42, L.z, d);
    if (L.w < 0.0) a = (1.0 - smoothstep(0.0, L.z, d)) * (-L.w);
    lit = max(lit, a);
  }
  float dark = 1.0 - lit;
  vec3 col = mix(tint, tint * 1.8 + vec3(0.01), n + 0.5);
  float haze = 0.0;
  for (int j = 0; j < 16; j++) {
    vec4 S = smokes[j];
    if (S.z < 1.0) continue;
    float dd = distance(w, S.xy) + (vn(w * 0.09 + vec2(time * 0.3, float(j))) - 0.5) * S.z * 0.6;
    haze = max(haze, (1.0 - smoothstep(S.z * 0.2, S.z, dd)) * S.w);
  }
  float a = max(dark * 0.965 * (1.0 - dawn), haze * (0.3 + 0.5 * lit));
  vec3 outc = mix(col, vec3(0.2, 0.18, 0.17), haze * lit);
  COLOR = vec4(outc, a);
}
