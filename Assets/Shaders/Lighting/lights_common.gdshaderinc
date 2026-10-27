// Common uniforms and noise for the darkness and glow shaders. Filled by Game.UpdateShaders.

uniform vec2 cam;
uniform vec2 view;
uniform float zoom;
uniform float time;
uniform int count;
uniform float flash;
uniform vec3 tint;
uniform float dawn;
uniform vec4 lights[96];
uniform vec4 smokes[16];
float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p){
  vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1.0, 0.0)), f.x), mix(h(i + vec2(0.0, 1.0)), h(i + vec2(1.0, 1.0)), f.x), f.y);
}
