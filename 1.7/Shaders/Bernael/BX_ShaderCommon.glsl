// Preserve the ShaderLab algorithms, including reversed smoothstep edges.
float bxSmoothstep(float a, float b, float x) { float t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec2 bxSmoothstep(float a, float b, vec2 x) { vec2 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec3 bxSmoothstep(float a, float b, vec3 x) { vec3 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec4 bxSmoothstep(float a, float b, vec4 x) { vec4 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
#define saturate(x) clamp(x,0.0,1.0)
// Captures and uploaded textures are top-down in Waylith; effect geometry uses Unity UVs.
#define tex2D(tex,uv) texture(tex,vec2((uv).x,1.0-(uv).y))
#define tex2Dbias(tex,uv) texture(tex,vec2((uv).x,1.0-(uv).y),(uv).w)
#define UNITY_PI 3.14159265358979323846
