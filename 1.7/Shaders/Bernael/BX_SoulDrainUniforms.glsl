layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Progress; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Length; // @default = 3.0
    float _Width; // @default = 2.4
    float _Seed; // @default = 0.0
    float _Mode; // @default = 0.0
    vec4 _Pull; // @default = vec4(-3,0,0,0)
    float _SoulAngle; // @default = 0.0
    float _DrainAmount; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _SmokeTex;
layout(set = 2, binding = 3) uniform sampler2D _AshenTex;
#define _MainTex_TexelSize vec4(1.0/vec2(textureSize(_MainTex,0)),vec2(textureSize(_MainTex,0)))
