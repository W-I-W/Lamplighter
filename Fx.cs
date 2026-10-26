namespace Lamplighter;

public static class Fx
{
    public const int MaxLights = 64;

    const string Common = @"
uniform vec2 cam;
uniform vec2 view;
uniform float zoom;
uniform float time;
uniform int count;
uniform float flash;
uniform vec4 lights[64];
float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p){
  vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1.0, 0.0)), f.x), mix(h(i + vec2(0.0, 1.0)), h(i + vec2(1.0, 1.0)), f.x), f.y);
}
";

    // Тьма: затемняет всё, кроме областей света (игрок, маяки, фонари)
    public const string Dark = "shader_type canvas_item;\n" + Common + @"
void fragment(){
  vec2 w = cam + (UV - 0.5) * view / zoom;
  float n = vn(w * 0.07 + vec2(time * 0.12, -time * 0.09)) - 0.5;
  float n2 = vn(w * 0.21 - vec2(time * 0.2, 0.0)) - 0.5;
  float lit = 0.0;
  for (int i = 0; i < 64; i++) {
    if (i >= count) break;
    vec4 L = lights[i];
    if (L.z < 1.0) continue;
    float d = distance(w, L.xy) + (n * 0.28 + n2 * 0.1) * L.z;
    lit = max(lit, 1.0 - smoothstep(L.z * 0.42, L.z, d));
  }
  float dark = 1.0 - lit;
  vec3 col = mix(vec3(0.012, 0.016, 0.045), vec3(0.03, 0.02, 0.07), n + 0.5);
  COLOR = vec4(col, dark * 0.965);
}";

    // Свечение: тёплый ореол вокруг источников, волна при зажигании, вспышка
    public const string Glow = "shader_type canvas_item;\nrender_mode blend_add;\n" + Common + @"
void fragment(){
  vec2 w = cam + (UV - 0.5) * view / zoom;
  vec3 acc = vec3(0.0);
  for (int i = 0; i < 64; i++) {
    if (i >= count) break;
    vec4 L = lights[i];
    if (L.z < 1.0) continue;
    float d = distance(w, L.xy);
    float t = clamp(1.0 - d / L.z, 0.0, 1.0);
    acc += vec3(1.0, 0.68, 0.30) * t * t * 0.20;
    float ring = exp(-pow((d - L.z) / 6.0, 2.0)) * L.w;
    acc += vec3(1.0, 0.86, 0.45) * ring * 0.9;
  }
  acc += vec3(flash * 0.45);
  COLOR = vec4(acc, 1.0);
}";
}
