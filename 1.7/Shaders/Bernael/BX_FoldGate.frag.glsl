#version 450
// A seam tears along each wall face. Black and crimson mist streams into the entrance and spills out of the far side.
// uv.x runs along the wall face; uv.y runs out from the wall, the face itself at Face. The quad is Length by Depth
// cells (FoldGateVisuals in FoldGateMapComponent.cs).
layout(location=0) in vec2 vUv;
layout(location=0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set=2,binding=0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Exit; // @default = 0.0
    float _Push; // @default = 0.0
    float _PushAt; // @default = 0.5
    float _Open; // @default = 1.0
    float _Close; // @default = 0.0
    float _Seed; // @default = 0.0
} uMaterial;
// Piloto Studio's tiling fog mask in alpha (Common/Textures/Effects/Piloto/SOURCES.json).
layout(set=2,binding=1) uniform sampler2D _FlowTex;

const float Face=0.3;
const float Length=2.5;
const float Depth=1.5;
const vec3 Abyss=vec3(0.018,0.009,0.014);
const vec3 Wine=vec3(0.145,0.025,0.042);
const vec3 Crimson=vec3(0.48,0.055,0.085);
const vec3 Scarlet=vec3(0.72,0.16,0.20);
const vec3 Fold=vec3(0.80,0.085,0.135);

// Use the derivatives of the unwrapped coordinates so the texture's repeat seam keeps the same mip level.
float flowMask(vec2 uv)
{
    vec2 g=fract(uv);
    return textureGrad(_FlowTex,vec2(g.x,1-g.y),dFdx(uv)*vec2(1,-1),dFdy(uv)*vec2(1,-1)).a;
}
vec4 over(vec4 back, vec4 front) { return front+back*(1-front.a); }

void main()
{
    float t=uMaterial._Phase;
    float seed=uMaterial._Seed;
    vec2 p=vec2((vUv.x-0.5)*Length,(vUv.y-Face)*Depth);
    float open=bxSmoothstep(0.0,1.0,uMaterial._Open);
    float close=bxSmoothstep(0.0,1.0,uMaterial._Close);
    float push=saturate(uMaterial._Push)*(1-close);
    float direction=mix(1.0,-1.0,uMaterial._Exit);
    float at=(uMaterial._PushAt-0.5)*Length;
    float part=exp(-pow((p.x-at)/0.37,2.0))*push;
    float lengthwise=p.x/(mix(0.32,1.02,open)*mix(1.0,0.24,close));
    float ends=1-bxSmoothstep(0.75,1.02,abs(lengthwise));
    float taper=pow(saturate(1-lengthwise*lengthwise),0.73);

    float grain=bxValueNoiseQuintic(vec2(p.x*4.8+seed,t*1.35));
    float fray=bxValueNoise(vec2(p.x*13.8+seed,t*3.6));
    float bend=(grain-0.5)*0.10+(fray-0.5)*0.035+part*0.064;
    float across=p.y-bend;
    float halfWidth=(0.041+0.12*open+part*0.095)*taper*(1-close*0.8)+0.009;
    float lip=abs(across)-halfWidth;
    float opening=(1-bxSmoothstep(-0.034,0.014,lip))*ends;
    float foldLight=exp(-pow(across/0.058,2.0))*part*(0.42+0.58*fray);

    // The veil flows away from the far face and toward the entrance. Its ends follow the tear in the wall.
    float floor=max(0.0,across);
    float twist=p.x+sin(floor*8.2+t*2.7+seed)*(0.074+floor*0.16);
    float wisps=flowMask(vec2(twist*0.57+seed+t*0.10,floor*0.81+t*direction*0.37));
    float mistNoise=bxValueNoiseQuintic(vec2(twist*4.1+seed,floor*4.7+t*direction*1.6));
    float spill=(1-bxSmoothstep(0.16,0.92,floor))*bxSmoothstep(-0.10,0.10,across)*ends;
    float mist=spill*bxSmoothstep(0.18,0.76,wisps*0.61+mistNoise*0.39)*(0.34+0.20*part);
    vec4 col=vec4(mix(Wine,Scarlet,wisps*0.51)*mist,mist);

    float dark=opening*(0.83+0.10*part);
    vec3 core=mix(Abyss,Wine,bxSmoothstep(0.0,halfWidth,abs(across)));
    core=mix(core,Fold,foldLight*0.77);
    col=over(col,vec4(core*dark,dark));

    float edge=exp(-pow(lip/0.044,2.0))*ends*(0.48+0.52*fray);
    float light=bxSmoothstep(0.28,0.81,wisps*0.66+grain*0.34);
    vec3 rim=mix(Crimson,Scarlet,light*(0.68+0.25*part));
    float rimAlpha=edge*(0.60+part*0.17);
    col=over(col,vec4(rim*rimAlpha,rimAlpha));
    float bounds=bxSmoothstep(-0.44,-0.25,p.y)*(1-bxSmoothstep(0.81,1.02,p.y));
    outColor=col*bounds*open*(1-close)*uMaterial._Opacity;
}
