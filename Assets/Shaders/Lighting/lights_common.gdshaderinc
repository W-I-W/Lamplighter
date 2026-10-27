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
// Smooth noise from a seamless texture (Assets/Shaders/Lighting/noise.png, 16 cells across): linear filtering keeps it free of block artifacts.
uniform sampler2D noise_tex : filter_linear, repeat_enable;
float vn(vec2 p){ return texture(noise_tex, p / 16.0).r; }
