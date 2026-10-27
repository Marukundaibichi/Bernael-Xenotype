#version 450
// Port of the matching 1.6 ShaderLab effect; timing, palette and geometry are unchanged.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Inscribe; // @default = 1.0
    float _Close; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Seed; // @default = 0.0
    float _Mode; // @default = 2.0
    float _Length; // @default = 2.0
    float _Size; // @default = 1.0
} uMaterial;


// Dire Orb's and Dark Mirage's palette: the concept's black emblem becomes navy and its grey skull pale
// blue bone, inside the same solid white contour and soft white glow.
const vec3 Navy = vec3(0.086,0.133,0.169);
const vec3 Deep = vec3(0.51,0.71,0.859);
const vec3 Pale = vec3(0.725,0.851,0.929);
// The emblem is measured in its ring's radius, and its quad reaches this far from the middle.
// MaliciousSealMapComponent sizes the brand by it.
const float Extent = 1.75;
// Contour and glow widths in cells, thinner than Dire Orb's since the brand is small.
const vec2 BrandLine = vec2(0.018,0.03);
const vec2 ChainLine = vec2(0.022,0.04);
// MaliciousSealMapComponent.ChainWidth, the chain quad's width in cells.
const float ChainWidth = 0.8;
// Shapes measured from the concept, with the ring's radius as 1 and +y up.
const float RingHalf = 0.055;
// Its diagonal spikes lean 47.5 degrees off the vertical and its side daggers 50: (sin, cos) of each.
const vec2 DiagonalAxis = vec2(0.7373,0.6756);
const vec2 SideAxis = vec2(0.7660,0.6428);
// How far each dagger's point rests from the middle, just clear of the skull, and the skull's scale.
const float CentreTip = 0.37;
const float SideTip = 0.39;
const float SkullScale = 1.12;
// The right eye socket's middle.
const vec2 EyeCentre = vec2(0.115,-0.085);




vec4 over(vec4 back, vec4 front)
{
    return front+back*(1-front.a);
}
float sq(float x)
{
    return x*x;
}
float easeInOut(float x)
{
    x=saturate(x);
    return x*x*(3-2*x);
}
float smin(float a, float b, float k)
{
    float h=saturate(0.5+0.5*(b-a)/k);
    return mix(b,a,h)-k*h*(1-h);
}
float segment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa=p-a, ba=b-a;
    return length(pa-ba*saturate(dot(pa,ba)/dot(ba,ba)));
}
float box(vec2 p, vec2 size, float round)
{
    vec2 q=abs(p)-size+round;
    return length(max(q,0))+min(max(q.x,q.y),0)-round;
}
// Close enough to an ellipse's distance for fills, contours and glow while its radii are alike.
float ellipse(vec2 p, vec2 radii)
{
    float k0=length(p/radii);
    float k1=length(p/(radii*radii));
    return k0*(k0-1)/max(k1,1e-5);
}
// Narrowing from topWidth at y = +height to bottomWidth at y = -height.
float trapezoid(vec2 p, float bottomWidth, float topWidth, float height)
{
    vec2 k1=vec2(topWidth,height), k2=vec2(topWidth-bottomWidth,2*height);
    p.x=abs(p.x);
    vec2 ca=vec2(p.x-min(p.x,p.y<0?bottomWidth:topWidth),abs(p.y)-height);
    vec2 cb=p-k1+k2*saturate(dot(k1-p,k2)/dot(k2,k2));
    float s=cb.x<0&&ca.y<0?-1:1;
    return s*sqrt(min(dot(ca,ca),dot(cb,cb)));
}
// A circle of radius ra at a, joined by tangents to one of radius rb at b.
float cone(vec2 p, vec2 a, vec2 b, float ra, float rb)
{
    vec2 ab=b-a;
    float h=max(length(ab),1e-4);
    vec2 d=ab/h;
    p-=a;
    vec2 q=vec2(abs(d.x*p.y-d.y*p.x),dot(p,d));
    float s=clamp((ra-rb)/h,-0.99,0.99);
    float c=sqrt(1-s*s);
    float k=c*q.y-s*q.x;
    if(k<0) return length(q)-ra;
    if(k>c*h) return length(q-vec2(0,h))-rb;
    return c*q.x+s*q.y-ra;
}
// A diamond along +y with points at a and b, widest (width) at m.
float kite(vec2 p, float a, float m, float b, float width)
{
    p.x=abs(p.x);
    vec2 A=vec2(0,a), M=vec2(width,m), B=vec2(0,b);
    float d=min(segment(p,A,M),segment(p,M,B));
    bool inside=(M.x-A.x)*(p.y-A.y)-(M.y-A.y)*(p.x-A.x)>0&&(B.x-M.x)*(p.y-M.y)-(B.y-M.y)*(p.x-M.x)>0;
    return inside?-d:d;
}

// Fills a shape, rims it in a solid white contour and wraps that in a soft white glow, as Dire Orb paints
// its flames. d is the distance to the shape and px one screen pixel, both in the shape's units, and
// widths holds its contour and glow widths.
vec4 paint(float d, vec3 fill, float px, vec2 widths)
{
    float aa=px*0.75;
    float stroke=max(widths.x,px*1.3);
    float halo=max(widths.y,px*2);
    float inside=1-bxSmoothstep(-aa,aa,d);
    float rim=1-bxSmoothstep(-aa,aa,d-stroke);
    vec3 c=mix(vec3(1,1,1),fill,inside);
    float g=max(d-stroke,0);
    float glow=0.45*exp2(-g*g/(halo*halo)*1.4427)*(1-bxSmoothstep(halo*1.8,halo*2.8,g))*(1-rim);
    return vec4(c*rim+glow,rim+glow);
}

// The concept's ring with its eight spikes: short ones on the axes, longer ones on the diagonals.
float ringShape(vec2 p)
{
    float d=abs(length(p)-1)-RingHalf;
    // Folded into one quadrant, so each line draws a spike on every side.
    vec2 q=abs(p);
    d=min(d,kite(q,0.84,1,1.21,0.1));
    d=min(d,kite(q.yx,0.84,1,1.21,0.1));
    vec2 slanted=vec2(dot(q,vec2(DiagonalAxis.y,-DiagonalAxis.x)),dot(q,DiagonalAxis));
    return min(d,kite(slanted,0.9,1,1.32,0.1));
}

// A dagger along +y, its point tip from the middle: a straight blade widening to the crossguard, a short
// grip and a round pommel, so it still reads as a dagger when the brand is small.
float dagger(vec2 p, float tip)
{
    p.y-=tip;
    float d=cone(p,vec2(0,0.016),vec2(0,0.22),0.014,0.064);
    d=min(d,box(p-vec2(0,0.245),vec2(0.1,0.025),0.014));
    d=min(d,box(p-vec2(0,0.31),vec2(0.028,0.05),0.01));
    return min(d,length(p-vec2(0,0.38))-0.042);
}

// A plain skull: a round crown over square cheekbones and a narrower jaw.
float skullShape(vec2 p)
{
    float d=length(p-vec2(0,0.02))-0.28;
    d=smin(d,box(p-vec2(0,-0.14),vec2(0.215,0.1),0.07),0.05);
    return smin(d,box(p-vec2(0,-0.29),vec2(0.135,0.085),0.045),0.04);
}
// Bone deepening toward the jaw, with scowling navy eye sockets in which an ember burns, a small nose and
// three gaps between the teeth. As eyes rises to 1 the ember fills its socket and turns white.
vec3 skullColor(vec2 p, float eyes, float aa)
{
    vec2 q=vec2(abs(p.x),p.y);
    vec3 c=mix(Deep,Pale,bxSmoothstep(-0.38,0.1,p.y));
    // Each socket is cut flat by a brow sloping down toward the nose.
    float socket=max(ellipse(q-EyeCentre,vec2(0.088,0.075)),dot(q-vec2(0.03,-0.06),vec2(-0.316,0.949)));
    float lit=saturate(eyes);
    float core=1-bxSmoothstep(0,0.02+0.05*lit,length(q-EyeCentre+vec2(0,0.008)));
    vec3 burn=mix(Deep,vec3(1,1,1),bxSmoothstep(0.35,0.9,lit)*core);
    vec3 hole=mix(Navy,burn,core*saturate(lit*2.2));
    c=mix(c,hole,1-bxSmoothstep(-aa,aa,socket));
    // The nose narrows to a point at the top.
    float nose=trapezoid(p-vec2(0,-0.2),0.042,0.008,0.036);
    float teeth=max(max(abs(q.x-round(q.x/0.065)*0.065)-0.011,q.x-0.1),p.y+0.275);
    return mix(c,Navy,1-bxSmoothstep(-aa,aa,min(nose,teeth)));
}
// Heats a colour through the palette's blue to white, so a flash never passes through grey.
vec3 heat(vec3 c, float flash)
{
    return flash<0.5?mix(c,Deep,flash*2):mix(Deep,vec3(1,1,1),flash*2-1);
}

struct Pose
{
    float ringScale;
    vec2 skullShift;
    float squash;
    float stab;           // how far the daggers are driven at the skull; negative when drawn back
    float eyes, flash;
};

// The ring and daggers under one fill and contour, then the skull over them, so a dagger driven into the
// skull vanishes behind it. Its eyes light up as the blades bite.
vec4 emblem(vec2 p, float px, vec2 widths, Pose o)
{
    float aa=px*0.75;
    vec3 white=1;
    vec2 q=vec2(abs(p.x),p.y);
    vec2 along=vec2(dot(q,vec2(SideAxis.y,-SideAxis.x)),dot(q,SideAxis));
    float metal=ringShape(p/o.ringScale)*o.ringScale;
    metal=min(metal,dagger(q,CentreTip-o.stab));
    metal=min(metal,dagger(along,SideTip-o.stab));
    vec4 col=paint(metal,heat(Navy,o.flash),px,widths);
    float unit=o.squash*SkullScale;
    vec2 local=(p-o.skullShift)/(vec2(1,o.squash)*SkullScale);
    vec3 bone=skullColor(local,o.eyes,aa/unit);
    col=over(col,paint(skullShape(local)*unit,mix(bone,white,o.flash*0.7),px,widths));
    // The burning eyes light the bone around them.
    vec2 e=(vec2(abs(local.x),local.y)-EyeCentre)/vec2(1,0.75);
    float light=o.eyes*o.eyes*exp2(-dot(e,e)*55);
    col.rgb+=mix(Deep,white,0.4)*light;
    col.a=saturate(col.a+light*0.45);
    return col;
}

// Once a second of a brand's loop: how far its daggers are driven at the skull, striking it at c = 0.
// They stay buried a moment, work free, rest, are drawn back, then strike again.
float stab(float c)
{
    const float Buried=0.13, Drawn=0.07;
    if(c<0.1) return Buried-0.012*sin(c/0.1*UNITY_PI);
    if(c<0.42) return Buried*(1-easeInOut((c-0.1)/0.32));
    if(c<0.6) return 0;
    if(c<0.9) return -Drawn*easeInOut((c-0.6)/0.3);
    return mix(-Drawn,Buried,sq((c-0.9)/0.1));
}

// The chain the seal throws from a sealed corpse (uv.x 0) to the pawn it spreads to (uv.x 1). Links run
// out as it grows; then a pulse of light runs down it to the victim over the first half of its release,
// and it falls apart link by link from the corpse outward.
vec4 chain(vec2 uv, float px)
{
    float L=max(uMaterial._Length,0.3);
    float s=uv.x*L;
    float y=(uv.y-0.5)*ChainWidth;
    float close=saturate(uMaterial._Close);
    float reach=saturate(uMaterial._Inscribe)*L;
    float pulse=saturate(close/0.5)*L;
    float dissolve=saturate((close-0.5)/0.5)*(L+0.3);
    // A slight sway, pinned at both ends.
    y-=0.04*sin(saturate(s/L)*UNITY_PI)*sin(s*2.3-uMaterial._Phase*4+uMaterial._Seed);
    // Face-on links alternate with links seen edge-on, each reaching into its neighbours.
    const float Pitch=0.4;
    float faces=1e3, edges=1e3;
    float nearest=floor(s/Pitch);
    for(int k=-1;k<=1;k++)
    {
        float n=nearest+k;
        float at=(n+0.5)*Pitch;
        float size=saturate((reach-at)/0.3)*(1-saturate((dissolve-at)/0.3));
        if(n<0||at>L-0.12||size<0.01) continue;
        vec2 local=vec2(s-at,y)/size;
        vec2 bar=vec2(local.x-clamp(local.x,-0.1,0.1),local.y);
        if(fmod(n,2)<0.5) faces=min(faces,(abs(length(bar)-0.1)-0.026)*size);
        else edges=min(edges,(length(vec2(local.x-clamp(local.x,-0.19,0.19),local.y))-0.034)*size);
    }
    float light=exp2(-sq((s-pulse)/0.3))*step(0.001,close)*(1-saturate((close-0.5)/0.15));
    vec3 fill=mix(Navy,vec3(1,1,1),light);
    return over(paint(faces,fill,px,ChainLine),paint(edges,fill,px,ChainLine));
}

// The seal branded over a victim's head. It stamps down white hot (MaliciousSealMapComponent shrinks its
// quad onto the pawn and lands it at 0.55 of uMaterial._Inscribe), cools to its colours, then bobs while its daggers
// stab the skull once a second, lighting its eyes. Fading, the daggers work free and the eyes go out.
vec4 brand(vec2 p, float px, vec2 widths)
{
    float t=uMaterial._Phase, stamp=saturate(uMaterial._Inscribe), fade=saturate(uMaterial._Close);
    p.y-=0.03*sin(t*UNITY_PI);
    float c=fract(t+0.45);
    float jolt=exp2(-c*12);
    Pose o;
    o.ringScale=1+0.025*exp2(-c*10);
    o.skullShift=vec2(0,-0.022*jolt);
    o.squash=1-0.05*jolt;
    o.stab=stab(c)-0.15*fade;
    o.eyes=(0.18+0.82*exp2(-c*8))*(1-fade);
    o.flash=stamp<0.55?0.6+0.4*sq(stamp/0.55):exp2(-sq((stamp-0.55)/0.2)*3);
    return emblem(p,px,widths,o)*bxSmoothstep(0,0.3,stamp)*(1-fade);
}

vec4 bxFragment(vec2 uv)
{
    // Emblem units and chain cells, and one screen pixel in each, measured outside any branch.
    vec2 e=(uv-0.5)*2*Extent;
    vec2 c=vec2(uv.x*max(uMaterial._Length,0.3),uv.y*ChainWidth);
    float pxE=sqrt(max(dot(dFdx(e),dFdx(e)),dot(dFdy(e),dFdy(e))));
    float pxC=sqrt(max(dot(dFdx(c),dFdx(c)),dot(dFdy(c),dFdy(c))));
    float cells=max(uMaterial._Size,0.01)/(2*Extent);
    bool strip=uMaterial._Mode<1.5;
    vec4 col=strip?chain(uv,pxC):brand(e,pxE,BrandLine/cells);
    // The brand fades on a circle and the chain at its sides, so a glow never shows the quad's outline.
    vec2 edge=abs(uv-0.5)*2;
    col*=1-bxSmoothstep(0.93,1,strip?edge.y:length(edge));
    return col*uMaterial._Opacity;
}

void main() { outColor=bxFragment(vUv); }
