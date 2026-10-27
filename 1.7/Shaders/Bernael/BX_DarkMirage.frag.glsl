#version 450
// Port of the matching 1.6 ShaderLab effect; timing, palette and geometry are unchanged.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _SpawnAge; // @default = 10.0
    vec4 _RevealBounds; // @default = vec4(0.3,0.75,0.3,0.7)
    vec4 _CanvasRect; // @default = vec4(0,0,1,1)
    float _Seed; // @default = 0.0
    float _Opacity; // @default = 1.0
    vec4 _EyeA; // @default = vec4(0.47,0.6,1,1)
    vec4 _EyeB; // @default = vec4(0.53,0.6,1,1)
    float _PreviewFireOnly; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _DistanceTex;
#define _MainTex_TexelSize vec4(1.0/vec2(textureSize(_MainTex,0)),vec2(textureSize(_MainTex,0)))

float silhouetteDistance(vec2 uv)
{
    vec2 fieldUV = (uv-uMaterial._CanvasRect.xy)/uMaterial._CanvasRect.zw;
    return (tex2D(_DistanceTex,fieldUV).r-0.5)*0.25;
}
float coverage(float distance)
{
    float aa = max(fwidth(distance)*0.7,0.0012);
    return 1-bxSmoothstep(-aa,aa,distance);
}
float randomCell(vec2 p)
{
    return fract(sin(dot(p,vec2(41.73,289.17)))*15731.743);
}
float softNoise(vec2 p)
{
    vec2 cell=floor(p), f=fract(p);
    f=f*f*f*(f*(f*6-15)+10);
    return mix(mix(randomCell(cell),randomCell(cell+vec2(1,0)),f.x),
        mix(randomCell(cell+vec2(0,1)),randomCell(cell+1),f.x),f.y);
}
vec2 eyeLight(vec2 uv, vec4 eye)
{
    vec2 d = (uv - eye.xy) * vec2(105, 160) / max(eye.w, 0.2);
    float r = dot(d,d);
    float core = 1-bxSmoothstep(0.35,1.15,r);
    float halo = exp2(-r * 0.24)*0.52 + exp2(-r * 0.065)*0.13;
    return vec2(core, halo) * eye.z;
}
float joinFlames(float a, float b, float radius)
{
    float h=saturate(0.5+0.5*(b-a)/radius);
    return mix(b,a,h)-radius*h*(1-h);
}
// A broad curved tongue with a tapered tip. Its shape travels with
// the rising flame packet; it does not oscillate sideways in place.
float flameTongue(vec2 uv, vec2 anchor, float width, float reach,
    float bend, float curl)
{
    float y=uv.y-anchor.y;
    float h=saturate(y/reach);
    float center=anchor.x + bend*h*h + curl*width*sin(h*3.141593)*h;
    float radius=width*pow(saturate(1-h),0.8)
        *(0.88+0.16*sin(h*3.141593));
    if(y<0) return length(uv-anchor)-radius;
    vec2 bounds=vec2(abs(uv.x-center)-radius,y-reach);
    // Euclidean distance outside the tip gives a round white cap
    // and smooth halo, instead of a square caused by max(x,y).
    return length(max(bounds,0))+min(max(bounds.x,bounds.y),0);
}
float risingFlame(vec2 uv, vec2 anchor, float width, float rise,
    float direction, float phase, float time)
{
    // Independent emission rates and per-emission shapes remove the
    // shared sine-wave rhythm. Two overlapping packets feed each root.
    float rate=0.90+0.35*randomCell(vec2(phase,17.2));
    float clock=time*rate+phase
        +0.12*(softNoise(vec2(time*0.7,phase+4.2))-0.5);
    float field=1;
    for(int packet=0;packet<2;packet++)
    {
        float tick=clock+packet*0.5;
        float cycle=floor(tick);
        float age=fract(tick);
        float variation=randomCell(vec2(cycle,phase+packet*13.7));
        float lean=randomCell(vec2(cycle+9.1,phase))-0.5;
        // The tip always travels up. Later, the tail catches it to
        // pinch off a small wisp rather than pull the whole tip down.
        float tip=0.022+rise*(0.82+variation*0.36)*age;
        float lift=(tip+0.012)*bxSmoothstep(0.42,1.0,age);
        vec2 root=anchor+vec2(lean*0.012*age,lift-0.012);
        float reach=max(tip-lift+0.012,0.003);
        // Side roots must feed the existing blue bed gradually;
        // a fast radius ramp makes the lower white contour kick out.
        float birthEnd=mix(0.14,0.28,abs(direction));
        float widthScale=bxSmoothstep(0,birthEnd,age)
            *(1-bxSmoothstep(0.52,1.0,age));
        float bodyWidth=max(width*(0.85+variation*0.30)*widthScale,0.0005);
        float bend=(direction*0.45+lean*1.60)*width*(0.25+age*1.3);
        float tongue=flameTongue(uv,root,bodyWidth,reach,bend,lean*2.4);
        // Anchor the side flame's first section inside the blue bed.
        // Its rising head remains free, but a new packet cannot puff
        // out the lower boundary before it has started climbing.
        tongue+=abs(direction)*0.018
            *(1-bxSmoothstep(0,0.045,uv.y-anchor.y));
        // End with no stroke/glow before the cycle wraps. The next
        // packet starts inside the connected blue bed, without a pop.
        tongue+=0.090*bxSmoothstep(0.80,1.0,age)
            +0.090*(1-bxSmoothstep(0,max(birthEnd,0.16),age));
        field=joinFlames(field,tongue,0.012);
    }
    return field;
}
vec4 bxFragment(vec2 uv)
{
    float t = uMaterial._Phase + uMaterial._Seed;
    // Slow only the flame flow; summoning, expiry and eyes keep their timing.
    float flameTime = uMaterial._Phase*0.70 + uMaterial._Seed;
    // All shape sizes and eye anchors remain in capture UV units;
    // only the field/mesh canvas grows to contain the complete fire.
    vec2 uv = uv*uMaterial._CanvasRect.zw+uMaterial._CanvasRect.xy;
    vec4 source = tex2D(_MainTex, uv);
    // Clamp sampling must not stretch an edge texel into the new
    // transparent margin if a captured accessory touches an edge.
    source *= step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    float a = source.a;
    float distance = silhouetteDistance(uv);
    float outline = coverage(distance-0.0095);

    // Independent of the random flame phase: body, eyes, then fire.
    float bodyProgress = saturate(uMaterial._SpawnAge/0.85);
    float bottom = uMaterial._RevealBounds.x-0.022;
    float top = uMaterial._RevealBounds.y+0.022;
    float height = (uv.y-bottom)/max(top-bottom,0.05);
    // Static noise makes the inverse dissolve strictly bottom-to-top:
    // newly revealed pieces never flicker away on the following tick.
    float dissolveNoise = softNoise(uv*vec2(43,51)+uMaterial._Seed)*0.75
        + softNoise(uv*vec2(89,97)+13.2)*0.25;
    float front = mix(-0.10,1.10,bodyProgress);
    float revealField = front-height-(dissolveNoise-0.5)*0.13;
    float reveal = bxSmoothstep(-0.008,0.008,revealField);
    if (uMaterial._SpawnAge<=0) reveal=0;
    if (bodyProgress>=1) reveal=1;
    float eyeIgnition = bxSmoothstep(0.85,1.15,uMaterial._SpawnAge);
    float fireProgress = bxSmoothstep(1.15,1.95,uMaterial._SpawnAge);
    // Move the entire reveal band above the canvas once ignited.
    // Capping it relative to the pawn keeps clipping tall flame tips
    // and their glow long after the summoning animation has ended.
    float fireFront = mix(bottom-0.06,uMaterial._CanvasRect.y+uMaterial._CanvasRect.w+0.025,fireProgress);
    float fireIgnition = bxSmoothstep(1.15,1.35,uMaterial._SpawnAge)
        * (1-bxSmoothstep(fireFront-0.025,fireFront+0.025,uv.y));

    // Solid blue flame sheets around the unchanged caster outline.
    // The packed contour anchors are generated once from the caster.
    float flame=distance-0.035;
    float span=max(uMaterial._RevealBounds.w-uMaterial._RevealBounds.z,0.12);
    for(int tongue=0;tongue<5;tongue++)
    {
        float lane=(tongue+0.5)/5.0;
        float x=mix(uMaterial._RevealBounds.z,uMaterial._RevealBounds.w,lane);
        float rootY=tex2D(_DistanceTex,vec2((x-uMaterial._CanvasRect.x)/uMaterial._CanvasRect.z,0.5)).g;
        float phase=tongue*2.399+uMaterial._Seed;
        float width=clamp(span*0.132,0.0275,0.0473);
        vec2 anchor=vec2(x,rootY);
        if(rootY>0.01)
        {
            flame=joinFlames(flame,risingFlame(uv,anchor,width,0.185,0,phase,flameTime),0.025);
        }
    }
    // Side tongues curl upwards out of the shoulders and lower body.
    for(int side=0;side<2;side++)
    {
        float sign=side*2-1;
        for(int tongue=0;tongue<2;tongue++)
        {
            float y=mix(uMaterial._RevealBounds.x,uMaterial._RevealBounds.y,0.14+tongue*0.36);
            vec4 contour=tex2D(_DistanceTex,vec2(0.5,(y-uMaterial._CanvasRect.y)/uMaterial._CanvasRect.w));
            float x=side==0?contour.b:contour.a;
            float phase=side*4.7+tongue*2.399+uMaterial._Seed;
            // Keep newborn bases under the permanent blue sheet;
            // only the upward part should push out the visible edge.
            vec2 anchor=vec2(x+sign*0.008,y);
            if(contour.a>contour.b)
            {
                flame=joinFlames(flame,risingFlame(uv,anchor,0.0363,0.155,sign,phase,flameTime),0.023);
            }
        }
    }
    // Use the same continuous field for fill, stroke and glow; only
    // the antialiasing coverage depends on screen derivatives.
    float edge=flame;
    float fill=coverage(edge);
    const float rimWidth=0.012; // Twice the previous 0.006 UV white stroke.
    float stroke=coverage(edge-rimWidth);
    float tint=saturate((uv.y-(uMaterial._RevealBounds.x-0.035)) /
        max(uMaterial._RevealBounds.y-uMaterial._RevealBounds.x+0.12,0.15));
    vec3 navy=vec3(0.075,0.137,0.173);
    vec3 fireColor=mix(vec3(0.49,0.70,0.86),vec3(0.74,0.865,0.945),tint);
    // Broad curved ribbons travel upward inside the BLUE sheet.
    // Deeper blue troughs and pale crests give the flame depth while
    // keeping the character and the solid white contour independent.
    vec2 flowUV=uv*vec2(14,8)+vec2(uMaterial._Seed*0.3,-flameTime*1.35);
    float curl=softNoise(flowUV*0.48+vec2(3.7,8.1));
    flowUV.x+=(curl-0.5)*1.4;
    float flow=softNoise(flowUV);
    float ribbons=softNoise(flowUV*vec2(1.55,0.80)+vec2(4.3,9.2));
    float depth=bxSmoothstep(0.20,0.80,flow);
    float crest=bxSmoothstep(0.48,0.83,ribbons)*(0.45+0.55*depth);
    fireColor*=0.87+0.14*depth;
    fireColor=mix(fireColor,vec3(0.81,0.92,0.99),crest*0.30);
    float innerLight=exp2(-pow(min(edge,0)/0.016,2)*1.4)*0.28;
    fireColor=mix(fireColor,vec3(0.90,0.96,1),innerLight);
    vec3 rgb=mix(vec3(1,1,1),fireColor,fill)*stroke;
    float glowDistance=max(edge-rimWidth,0);
    float glow=exp2(-pow(glowDistance/0.014,2)*1.7)*0.32
        +exp2(-pow(glowDistance/0.028,2)*1.5)*0.07;
    glow*=1-bxSmoothstep(0.035,0.060,glowDistance);
    rgb+=vec3(0.93,0.97,1)*glow*(1-stroke);
    float alpha=stroke+glow*(1-stroke);
    rgb *= fireIgnition;
    alpha *= fireIgnition;
    // Editor inspection isolates the exterior flame layer.
    if (uMaterial._PreviewFireOnly>0.5) return vec4(rgb,alpha)*(uMaterial._Opacity*(1-outline));

    // Keep the caster a single dark shape within the pale fire.
    rgb = mix(rgb,navy,outline);
    alpha = mix(alpha,1,outline);
    float detail = dot(source.rgb, vec3(0.25,0.55,0.2));
    vec3 bodyColor = navy
        + bxSmoothstep(0.15,0.7,detail)*vec3(0.007,0.009,0.010);
    rgb = mix(rgb,bodyColor,a);
    alpha = mix(alpha,1,a);
    rgb *= reveal;
    alpha *= reveal;
    float dissolveEdge = (1-bxSmoothstep(0.009,0.045,revealField))*reveal*outline
        * (1-step(1,bodyProgress));
    rgb += vec3(0.51,0.71,0.86)*dissolveEdge*0.68;
    vec2 eyes = (eyeLight(uv,uMaterial._EyeA) + eyeLight(uv,uMaterial._EyeB))*eyeIgnition;
    float pulse = 0.94 + sin(t*2.4)*0.06;
    rgb += (vec3(1,1,1)*eyes.x + vec3(0.51,0.71,0.86)*eyes.y)*pulse;
    alpha = saturate(alpha + eyes.x*0.5 + eyes.y*0.18);
    // Transparent field padding already encloses the glow. A fade
    // at the old capture border would cut tall/wide flames again.
    return vec4(rgb,alpha) * uMaterial._Opacity;
}

void main() { outColor=bxFragment(vUv); }
