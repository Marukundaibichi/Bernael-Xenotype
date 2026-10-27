#version 450
// Port of the matching 1.6 ShaderLab effect; timing, palette and geometry are unchanged.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @default = vec4(1,1,1,1)
    vec4 _ColorTwo; // @default = vec4(1,1,1,1)
    float _UseMask; // @default = 0.0
    float _DrainAmount; // @default = 1.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _MaskTex;

vec4 bxFragment(vec2 uv)
{
    vec4 tex=tex2D(_MainTex,uv);
    clip(tex.a*uMaterial._Color.a-0.35);
    // CutoutComplex applies the red and green tint masks multiplicatively.
    // An unmasked pixel retains the source texture color.
    vec2 mask = tex2D(_MaskTex,uv).rg;
    vec3 maskedTint = mix(vec3(1,1,1),uMaterial._Color.rgb,mask.r) *
        mix(vec3(1,1,1),uMaterial._ColorTwo.rgb,mask.g);
    vec3 original = tex.rgb*mix(uMaterial._Color.rgb,maskedTint,uMaterial._UseMask);
    float luminance=dot(original,vec3(0.299,0.587,0.114));
    float gray=0.018+pow(saturate(luminance),0.85)*0.31;
    return vec4(mix(original,gray.xxx,saturate(uMaterial._DrainAmount)),1);
}

void main() { outColor=bxFragment(vUv); }
