#version 450
// Fold Shift: the pawn under an opaque black filter, a fine dark-crimson outline and glowing eyes. Built on
// BX_DarkMirage's captured silhouette, distance field and eye anchors. _SpawnAge drives the full-body tint,
// rising rim and eye light. _Fade drives the exit. Mode 1 is the crimson pulse at the start of the shift.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _SpawnAge; // @default = 1.0
    float _Fade; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Mode; // @default = 0.0
    float _Seed; // @default = 0.0
    vec4 _RevealBounds; // @default = vec4(0.3,0.75,0.3,0.7)
    vec4 _CanvasRect; // @default = vec4(0,0,1,1)
    vec4 _EyeA; // @default = vec4(0.47,0.6,1,1)
    vec4 _EyeB; // @default = vec4(0.53,0.6,1,1)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _DistanceTex;

const vec3 Void = vec3(0.018,0.014,0.018);
const vec3 Ember = vec3(0.22,0.025,0.042);
const vec3 Crimson = vec3(0.65,0.085,0.12);
const vec3 Glow = vec3(0.74,0.10,0.14);
const vec3 EyeCore = vec3(0.96,0.65,0.62);

float silhouetteDistance(vec2 uv)
{
    vec2 fieldUV=(uv-uMaterial._CanvasRect.xy)/uMaterial._CanvasRect.zw;
    return (tex2D(_DistanceTex,fieldUV).r-0.5)*0.25;
}
float coverage(float distance)
{
    float aa=max(fwidth(distance)*0.7,0.0012);
    return 1-bxSmoothstep(-aa,aa,distance);
}
// Seamless lattice noise (BX_ShaderCommon.glsl); a sine hash broke the dissolve into rectangles.
float softNoise(vec2 p) { return bxValueNoiseQuintic(p); }
// Glowing eyes, large enough to read at the normal zoom.
vec2 eyeLight(vec2 uv, vec4 eye)
{
    vec2 d=(uv-eye.xy)*vec2(72,108)/max(eye.w,0.2);
    float r=dot(d,d);
    float core=1-bxSmoothstep(0.35,1.15,r);
    float halo=exp2(-r*0.30)*0.36+exp2(-r*0.09)*0.08;
    return vec2(core,halo)*eye.z;
}

vec4 shadow(vec2 canvasUv)
{
    float t=uMaterial._Phase+uMaterial._Seed;
    float fade=saturate(uMaterial._Fade);
    vec2 uv=canvasUv*uMaterial._CanvasRect.zw+uMaterial._CanvasRect.xy;
    vec4 source=tex2D(_MainTex,clamp(uv,vec2(0.0),vec2(1.0)));
    source*=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    float distance=silhouetteDistance(uv);
    float a=source.a*(1-bxSmoothstep(0.002,0.014,distance));

    vec2 lower=uMaterial._RevealBounds.zx;
    vec2 upper=uMaterial._RevealBounds.wy;
    vec2 beyond=max(lower-uv,uv-upper);
    float support=1-bxSmoothstep(0.036,0.062,max(beyond.x,beyond.y));
    float height=(uv.y-lower.y)/max(upper.y-lower.y,0.05);
    float lateral=(uv.x-mix(lower.x,upper.x,0.5))/max(upper.x-lower.x,0.05);
    float dissolveNoise=softNoise(uv*vec2(29,35)+uMaterial._Seed)*0.7+softNoise(uv*vec2(75,79)+13.2)*0.3;
    // A soft, curved front rises through the body. On exit it flows back down through the same silhouette.
    float entry=saturate(uMaterial._SpawnAge);
    float enterFront=mix(-0.24,1.29,bxSmoothstep(0.0,1.0,entry));
    float exitFront=mix(1.29,-0.24,bxSmoothstep(0.0,1.0,fade));
    float revealField=min(enterFront,exitFront)-height-lateral*lateral*0.13-(dissolveNoise-0.5)*0.22;
    float reveal=bxSmoothstep(-0.076,0.062,revealField);
    // Shade the face and body together as the rim travels. Ease the whole tint out behind the departing rim.
    float tint=0.86*bxSmoothstep(0.025,0.31,entry)*(1-bxSmoothstep(0.15,0.87,fade));
    float bodyReveal=mix(tint,1.0,reveal);
    float eyeIgnition=bxSmoothstep(0.13,0.48,entry)*(1-bxSmoothstep(0.13,0.68,fade));

    // Keep the rim fine enough to preserve the halo's gaps and sharp points.
    const float lineWidth=0.0032;
    float line=coverage(distance-lineWidth);
    float glowDistance=max(distance-lineWidth,0.0);
    float breath=0.94+0.06*sin(t*2.1);
    float glow=(exp2(-pow(glowDistance/0.006,2)*1.6)*0.24+exp2(-pow(glowDistance/0.016,2)*1.4)*0.055)*breath;
    glow*=1-bxSmoothstep(0.018,0.035,glowDistance);
    vec3 rim=mix(Glow*glow,Crimson,line)*reveal;
    float rimAlpha=mix(glow,1.0,line)*reveal;

    // The pawn itself under a black filter: opaque, its colours nearly gone, its shading and line art still
    // readable, as through smoked glass.
    float luma=dot(source.rgb,vec3(0.299,0.587,0.114));
    vec3 detail=mix(vec3(luma),source.rgb,0.25);
    vec3 body=Void+detail*0.18;
    // Retain the halo's native crimson linework instead of replacing it with a swollen outer rim.
    float crimsonInk=bxSmoothstep(0.10,0.42,source.r-max(source.g,source.b));
    body=mix(body,source.rgb*vec3(0.50,0.55,0.55),crimsonInk*0.82);
    vec3 rgb=mix(rim,body*bodyReveal,a);
    float alpha=mix(rimAlpha,bodyReveal,a);
    float dissolveEdge=(1-bxSmoothstep(0.016,0.092,abs(revealField)))*reveal*line;
    rgb+=Crimson*dissolveEdge*0.36;
    vec2 eyes=(eyeLight(uv,uMaterial._EyeA)+eyeLight(uv,uMaterial._EyeB))*eyeIgnition*bodyReveal;
    float pulse=0.92+sin(t*2.4)*0.08;
    rgb+=(EyeCore*eyes.x+Glow*eyes.y)*pulse;
    alpha=saturate(alpha+eyes.x*0.5+eyes.y*0.2);
    // At a gate the shadow breaks up in the veil as it moves through the face of the wall.
    float passage=saturate(uMaterial._Opacity);
    if(passage<1.0)
    {
        float drift=softNoise(uv*vec2(18,24)+vec2(t*0.35,-t*0.82));
        float threshold=mix(-0.09,1.09,passage)-mix(dissolveNoise,drift,0.45);
        float pieces=bxSmoothstep(-0.077,0.077,threshold);
        float embers=(1-bxSmoothstep(0.015,0.13,abs(threshold)))*pieces;
        rgb=rgb*pieces+Crimson*alpha*embers*0.42;
        alpha*=pieces;
    }
    return vec4(rgb,alpha)*support*(1-bxSmoothstep(0.87,1.0,fade));
}

// A crimson pulse and torn ring as the caster slips out of phase.
vec4 pulse(vec2 uv)
{
    float t=uMaterial._Phase;
    vec2 p=uv*2-1;
    float r=length(p);
    float torn=softNoise(p/max(r,0.001)*3.2+vec2(t*1.5,uMaterial._Seed));
    float front=0.14+0.65*(1-exp(-t*5.4));
    float lip=(r-front-(torn-0.5)*0.09)/0.063;
    float ring=exp(-lip*lip)*(0.42+0.58*torn);
    float inner=exp(-pow(r/max(front,0.01),2.0))*0.33;
    float life=bxSmoothstep(0.0,0.07,t)*(1-bxSmoothstep(0.17,0.39,t));
    float alpha=saturate(ring*0.54+inner)*life;
    vec3 c=mix(Ember,Crimson,ring*0.8);
    return vec4(c*alpha,alpha);
}

void main()
{
    vec4 col=uMaterial._Mode>0.5?pulse(vUv):shadow(vUv);
    outColor=col*uMaterial._Opacity;
}
