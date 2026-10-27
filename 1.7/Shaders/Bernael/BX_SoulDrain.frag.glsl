#version 450
// Port of the matching 1.6 ShaderLab effect; timing, palette and geometry are unchanged.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
#include "BX_SoulDrainUniforms.glsl"

void over(inout vec4 dst, vec3 color, float alpha)
{
    alpha=saturate(alpha);
    dst.rgb=color*alpha+dst.rgb*(1-alpha);
    dst.a=alpha+dst.a*(1-alpha);
}
vec4 bxFragment(vec2 uv)
{
    vec4 col=vec4(0.0);
    if (uMaterial._Mode > 2.5)
    {
        vec4 pawn=tex2D(_MainTex,uv);
        pawn=mix(pawn,tex2D(_AshenTex,uv),uMaterial._DrainAmount);
        return vec4(pawn.rgb*pawn.a,pawn.a)*uMaterial._Opacity;
    }
    if (uMaterial._Mode > 1.5)
    {
        vec4 captured=tex2D(_MainTex,uv);
        vec2 pixel=_MainTex_TexelSize.xy*2.2;
        float halo=max(max(tex2D(_MainTex,uv+vec2(pixel.x,0)).a,
            tex2D(_MainTex,uv-vec2(pixel.x,0)).a),
            max(tex2D(_MainTex,uv+vec2(0,pixel.y)).a,
            tex2D(_MainTex,uv-vec2(0,pixel.y)).a));
        float inside=min(min(tex2D(_MainTex,uv+vec2(pixel.x,0)).a,
            tex2D(_MainTex,uv-vec2(pixel.x,0)).a),
            min(tex2D(_MainTex,uv+vec2(0,pixel.y)).a,
            tex2D(_MainTex,uv-vec2(0,pixel.y)).a));
        float smoke=tex2D(_SmokeTex,uv*1.6+vec2(uMaterial._Phase*0.10,-uMaterial._Phase*0.12)).g;
        float detail=dot(captured.rgb,vec3(0.30,0.59,0.11));
        float visibility=1-bxSmoothstep(0.975,1,uMaterial._Progress);
        float fill=captured.a*(0.44+smoke*0.24);
        float edge=saturate(captured.a-inside);
        over(col,vec3(0.12,0.57,0.63),(halo-captured.a)*0.32);
        over(col,mix(vec3(0.16,0.47,0.51),vec3(0.54,0.87,0.86),detail),fill);
        over(col,vec3(0.68,0.98,0.96),edge*0.8);
        return col*visibility*uMaterial._Opacity;
    }
    if (uMaterial._Mode > 0.5)
    {
        vec2 p=(uv-0.5)*2;
        float r=length(p);
        over(col,vec3(0.65,0.004,0.019),exp(-r*r*8)*0.6);
        over(col,vec3(1,0.03,0.065),exp(-abs(p.y)*60-abs(p.x)*4)*0.24);
        over(col,vec3(1,0.58,0.49),exp(-r*r*165));
        // The final condensed soul is swallowed by the red eye light.
        over(col,vec3(0.61,0.97,0.95),exp(-r*r*95)*
            bxSmoothstep(0.82,0.94,uMaterial._Progress)*(1-bxSmoothstep(0.97,1,uMaterial._Progress)));
        return col*uMaterial._Opacity*(1-bxSmoothstep(0.75,1,r));
    }
    // Only advected smoke here; the soul is a separate deforming pawn capture.
    vec2 p=vec2(uv.x*(uMaterial._Length+1.1)-0.55,(uv.y-0.5)*uMaterial._Width);
    float u=saturate(p.x/max(uMaterial._Length,0.1));
    float gate=bxSmoothstep(-0.10,0.15,p.x)*(1-bxSmoothstep(uMaterial._Length-0.08,uMaterial._Length+0.45,p.x));
    float open=bxSmoothstep(0.015,0.16,uMaterial._Progress);
    float center=sin(u*4.5-uMaterial._Phase*1.2+uMaterial._Seed)*0.10*sin(u*UNITY_PI);
    float width=0.07+0.5*u;
    float y=(p.y-center)/width;
    vec2 flow=vec2(p.x*0.33+uMaterial._Phase*0.24+uMaterial._Seed,p.y*0.62+uMaterial._Seed*0.19);
    float broad=tex2D(_SmokeTex,flow*0.85+vec2(uMaterial._Phase*0.04,0)).r;
    flow+=vec2(broad-0.5,sin(u*5-uMaterial._Phase)*0.12)*0.19;
    float smoke=tex2Dbias(_SmokeTex,vec4(flow,0,2)).g;
    float detail=tex2D(_SmokeTex,flow*1.73+vec2(uMaterial._Phase*0.08,0.37)).b;
    float billow=tex2D(_SmokeTex,flow*1.21+vec2(0.38,-uMaterial._Phase*0.055)).r;
    float softEnvelope=exp(-y*y*1.35);
    // Broad, translucent density carries the effect. Fine wisps only add
    // internal motion, so the connection reads as smoke rather than ribbons.
    float cloud=pow(saturate(broad*0.82+billow*0.66+detail*0.42),1.12);
    float alpha=cloud*softEnvelope*gate*open*0.86;
    float red=saturate(billow*0.72+detail*0.32);
    over(col,mix(vec3(0.018,0.016,0.023),vec3(0.29,0.042,0.052),red),alpha);
    over(col,vec3(0.42,0.035,0.045),smoke*softEnvelope*gate*open*0.26);
    return col*uMaterial._Opacity*(1-bxSmoothstep(1.0,1.19,abs(p.y)));
}

void main() { outColor=bxFragment(vUv); }
