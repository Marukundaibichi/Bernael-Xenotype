#version 450
// Port of the matching 1.6 ShaderLab effect; timing, palette and geometry are unchanged.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Seed; // @default = 0.0
    float _Mode; // @default = 0.0
    vec4 _Direction; // @default = vec4(1,0,0,0)
    float _Length; // @default = 2.6
    float _Travel; // @default = 0.0
    float _Charge; // @default = 1.0
    float _Landed; // @default = 0.0
    float _Radius; // @default = 2.6
} uMaterial;


const float TAU = 6.2831853;
// Cold flame drawn like Dark Mirage's: pale blue deepening toward its base, a navy heart,
// a solid white contour and a soft white glow. The colours are sampled from the reference art.
const vec3 Navy = vec3(0.086,0.133,0.169);
const vec3 Deep = vec3(0.51,0.71,0.859);
const vec3 Pale = vec3(0.725,0.851,0.929);
// Sizes are in map cells. The contour is as thick as Dark Mirage's.
const float Stroke = 0.036;
const float Halo = 0.055;
// Half of Projectile_DireOrb.OrbSize, its TrailWidth, and how far past the blast radius
// DireOrbMapComponent's burst quad reaches, so flames at the edge fit with their glow.
const float OrbCells = 2.0;
const float TrailWidth = 1.2;
const float BurstMargin = 1.9;
// Radius of the flying orb's head, the bowl of its flame.
const float HeadR = 0.40;
// Seconds from impact until the orb bursts, matching DireOrbMapComponent.DetonateTicks.
const float Collapse = 0.30;




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
// Union that rounds the crease between two shapes over k: wide where a tongue flows out of the
// bowl, tight where two tongues meet in the reference's sharp notches.
float smin(float a, float b, float k)
{
    float h=saturate(0.5+0.5*(b-a)/k);
    return mix(b,a,h)-k*h*(1-h);
}
vec4 over(vec4 back, vec4 front)
{
    return front+back*(1-front.a);
}
// Distance to a tapered segment: a circle of radius ra at a, joined by tangents to one of radius rb at b.
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
// A flame tongue from base toward dir, len long and width in radius at its root, tapering to a
// sharp tip. It leans sideways by bend and curl hooks its tip, giving the S of the reference flames.
float tongue(vec2 p, vec2 base, vec2 dir, float len, float width, float bend, float curl)
{
    vec2 side=vec2(dir.y,-dir.x)*len;
    vec2 along=dir*len;
    vec2 a=base+along*0.25+side*(bend*0.4375+curl*0.0156);
    vec2 b=base+along*0.5+side*(bend*0.75+curl*0.125);
    vec2 c=base+along*0.75+side*(bend*0.9375+curl*0.4219);
    vec2 tip=base+along+side*(bend+curl);
    float d=cone(p,base,a,width,width*0.92);
    d=min(d,cone(p,a,b,width*0.92,width*0.75));
    d=min(d,cone(p,b,c,width*0.75,width*0.45));
    return min(d,cone(p,c,tip,width*0.45,0));
}
// One lane of fire: two tongues in turn stretch out of base along dir, pinch off at the root and
// shrink away as loose wisps, each with its own reach, lean and hooked tip.
float lane(vec2 p, vec2 base, vec2 dir, float len, float width, float lean, float seed, float time)
{
    float d=1e3;
    float rate=2.5+0.9*randomCell(vec2(seed,5.3));
    for(int k=0;k<2;k++)
    {
        float tick=time*rate+seed+k*0.5;
        float cycle=floor(tick), age=fract(tick);
        float rnd=randomCell(vec2(cycle,seed+k*13.7));
        float rnd2=randomCell(vec2(cycle+9.1,seed-k*3.1));
        float l=len*(0.85+0.3*rnd);
        // Stretches out until 0.55; then its root climbs after the tip until only a wisp a third
        // as wide is left, which shrinks away about its middle.
        float loose=saturate((age-0.55)/0.45);
        float pinch=bxSmoothstep(0,0.5,loose);
        float tip=l*(0.35+0.65*saturate(age/0.55)+0.2*loose);
        float w=width*(0.85+0.3*rnd2)*bxSmoothstep(0,0.2,age)*mix(1,0.28,pinch);
        float piece=mix(tip,3.2*w,pinch);
        float fade=1-bxSmoothstep(0.55,1,loose);
        float mid=tip-piece*0.5;
        w*=fade;
        if(w>1e-3) d=min(d,tongue(p,base+dir*(mid-piece*0.5*fade),dir,max(piece*fade,1e-3),w,
            lean+(rnd-0.5)*0.4,(rnd2-0.5)*0.6-lean*1.5));
    }
    return d;
}

// Distances in cells to a flame's outline and to its navy heart, and how far up the flame the
// point lies, which deepens its blue toward the base.
struct Fire { float flame; float heart; float height; };

// Paints one flame: its fill deepens toward the base as in the reference, the navy heart sits
// inside, the white contour lies outside the fill and a soft white glow outside that.
// px is one screen pixel in cells, which keeps the contour at least a pixel and a half wide.
vec4 paint(Fire fire, float px)
{
    float aa=px*0.75;
    float stroke=max(Stroke,px*1.5);
    float fill=1-bxSmoothstep(-aa,aa,fire.flame);
    float rim=1-bxSmoothstep(-aa,aa,fire.flame-stroke);
    float heart=1-bxSmoothstep(-aa,aa,fire.heart);
    float depth=1-pow(saturate(1-fire.height/0.65),1.5);
    vec3 c=mix(mix(Deep,Pale,depth),Navy,heart);
    c=mix(vec3(1,1,1),c,fill);
    float g=max(fire.flame-stroke,0);
    float glow=0.45*exp2(-g*g/(Halo*Halo)*1.4427)*(1-bxSmoothstep(Halo*1.8,Halo*2.8,g))*(1-rim);
    return vec4(c*rim+glow,rim+glow);
}

// A flame in the reference's shape, in units of its bowl's half width from the bowl's middle,
// rising along +y: a wide, flattened bowl with a horn flicking up out of each shoulder, a broad main
// tongue with a branch off either side, and a navy heart low in it that is a smaller flame of its
// own, a scooped bowl with a slender spire. squash flattens the bowl and reach scales the tongues.
Fire flame(vec2 p, float time, float seed, float reach, float squash)
{
    Fire o;
    float side=randomCell(vec2(seed,2.7))<0.5?-1:1;
    // A slow sway with a quicker flutter on top, so the flame never settles.
    float sway=softNoise(vec2(time*1.6,seed))-0.5+0.5*(softNoise(vec2(time*3.7,seed+4.4))-0.5);
    float lean=side*0.18+sway*0.45;
    float grow=sqrt(reach);
    // The main tongue is a broad column swaying in an S; its tip whips about and flickers in length.
    vec2 up=normalize(vec2(sway*0.45,1));
    vec2 across=vec2(up.y,-up.x);
    float curl=-lean*1.6+(softNoise(vec2(time*2.9,seed+7.7))-0.5)*0.9;
    float len=max(2.4*reach*(0.85+0.3*softNoise(vec2(time*2.6,seed+1.9))),1e-3);
    // Halfway up it and its tip, on the curve tongue() draws for this lean and curl.
    vec2 middle=up*len*0.5+across*len*(lean*0.75+curl*0.125);
    vec2 top=up*len+across*len*(lean+curl);
    o.flame=(length(p/vec2(1,squash))-1)*squash;
    for(int h=0;h<2;h++)
    {
        float s=h*2-1;
        float bob=softNoise(vec2(time*2.6,seed+h*5.1))-0.5;
        o.flame=smin(o.flame,tongue(p,vec2(s*0.82,0.15),normalize(vec2(s*0.9,1)),max(0.8*reach*(0.8+0.4*(bob+0.5)),1e-3),
            0.24*grow,s*(0.1+bob*0.55),-s*0.5-bob*0.4),0.1);
    }
    // The column flows out of the bowl, and tongues growing out of its middle stretch past its tip
    // and tear loose there. The branches meet it in sharp notches.
    o.flame=smin(o.flame,tongue(p,vec2(0,0),up,len,0.52*grow,lean,curl),0.25);
    o.flame=smin(o.flame,lane(p,middle,normalize(top-middle),length(top-middle)*1.15,0.3*grow,-lean*0.5,seed,time),0.08);
    o.flame=smin(o.flame,lane(p,vec2(side*0.28,0.75),normalize(vec2(side*0.7,1)),1.1*reach,0.28*grow,
        side*0.2,seed+3.1,time),0.04);
    o.flame=smin(o.flame,lane(p,vec2(-side*0.22,1.25),normalize(vec2(-side*0.6,1)),0.9*reach,0.22*grow,
        -side*0.2,seed+6.3,time),0.04);
    float bowl=max((length((p-vec2(0,-0.12))/vec2(0.74,0.5))-1)*0.5,0.46-length(p-vec2(0,0.52)));
    // The heart's spire follows the column's S up its middle and flows smoothly out of the bowl.
    float spire=tongue(p,vec2(0,-0.2),up,len*0.85,0.3*grow,lean,curl);
    o.heart=max(smin(bowl,spire,0.2),o.flame+0.15);
    o.height=(p.y+squash)/(squash+2.4*max(reach,0.3));
    return o;
}

vec4 orb(vec2 p, float px)
{
    float time=uMaterial._Phase+uMaterial._Seed;
    float landed=uMaterial._Landed;
    // Once landed, the tongues are dragged back into the head, leaving a stub of flame still
    // writhing, and the orb tightens and shudders until it bursts.
    float reach=mix(1,0.35,bxSmoothstep(0,0.2,landed));
    float strain=bxSmoothstep(0.1,Collapse,landed);
    float scale=max(bxSmoothstep(0,1,uMaterial._Charge),0.04)*mix(1,0.85,strain);
    vec2 shake=(vec2(softNoise(vec2(time*40,uMaterial._Seed)),softNoise(vec2(uMaterial._Seed,time*40)))-0.5)*0.04*strain;
    vec2 dir=normalize(uMaterial._Direction.xy+vec2(1e-4,0));
    vec2 q=(p-shake)/(scale*HeadR);
    // The orb is a flame lying along its path, its bowl in front and its tongues streaming back.
    // Its bowl rounds out as it collapses.
    Fire fire=flame(vec2(dot(q,vec2(-dir.y,dir.x)),-dot(q,dir)),time,uMaterial._Seed,reach,mix(0.85,1,strain));
    fire.flame*=scale*HeadR;
    fire.heart*=scale*HeadR;
    // Loose flames are drawn in from all round and swallowed.
    if(landed>0)
    {
        for(int m=0;m<6;m++)
        {
            float rnd=randomCell(vec2(m,uMaterial._Seed+4.2));
            float t=saturate((landed-0.02-0.1*rnd)/0.16);
            float angle=(m+rnd*0.7)/6*TAU;
            vec2 ray=vec2(cos(angle),sin(angle));
            float size=0.07*(1-t*t)*bxSmoothstep(0,0.2,t);
            if(size>1e-3) fire.flame=smin(fire.flame,tongue(p,ray*mix(1.3,0.3,t*t),ray,size*3,size,(rnd-0.5)*0.6,0.3),0.05);
        }
    }
    return paint(fire,px);
}

// Wisps torn off the orb's tongues stay where they tore loose and burn down to nothing.
vec4 trail(vec2 uv, float px)
{
    // Cells behind the orb, across its path, and from the launch point along it.
    float s=uv.x*max(uMaterial._Length,0.3);
    float y=(uv.y-0.5)*TrailWidth;
    float world=uMaterial._Travel-s;
    const float spacing=0.32;
    float slot=floor(world/spacing);
    // Once the orb lands they all burn out together.
    float linger=1-bxSmoothstep(0,0.25,uMaterial._Landed);
    Fire fire;
    fire.flame=1e3;
    fire.heart=1e3;
    fire.height=0.8;
    for(int k=-1;k<=2;k++)
    {
        float n=slot+k;
        float rnd=randomCell(vec2(n,uMaterial._Seed+3.1));
        float rnd2=randomCell(vec2(n+7.7,uMaterial._Seed));
        float rnd3=randomCell(vec2(n+2.9,uMaterial._Seed+5.5));
        float at=(n+0.15+0.7*rnd2)*spacing;
        float behind=uMaterial._Travel-at;
        float life=saturate((behind-0.9)/1.4);
        // None behind the launch point, and smaller ones while the orb was still growing.
        float size=0.075*(0.7+0.6*rnd)*bxSmoothstep(0,0.15,life)*(1-bxSmoothstep(0.3,1,life))*linger
            *step(0.35,randomCell(vec2(n+5.3,uMaterial._Seed+1.7)))*saturate(at/2);
        float across=(rnd3-0.5)*0.45;
        if(size>1e-3) fire.flame=smin(fire.flame,tongue(vec2(s,y),vec2(behind,across),normalize(vec2(1,across)),
            size*3,size,(rnd-0.5)*0.4,(rnd2-0.5)*0.8),0.03);
    }
    return paint(fire,px);
}

// One flame of the burst standing upright on the ground at foot, in cells.
Fire standing(vec2 p, vec2 foot, float size, float squash, float time, float seed, float reach)
{
    Fire fire;
    // Once a flame has burned out its size is 0, and dividing by it would leave NaN distances, which
    // paint() fills with solid navy.
    if(size<1e-3)
    {
        fire.flame=1e3;
        fire.heart=1e3;
        fire.height=1;
        return fire;
    }
    fire=flame((p-foot)/size-vec2(0,squash),time,seed,reach,squash);
    fire.flame*=size;
    fire.heart*=size;
    return fire;
}

// The orb bursts: pale fire blows out of it with broad tongues all round, then falls back into a tall
// flame rising in its place, while blobs of fire flung out of the burst light flames where they land
// all over the blast. Each burns up, sheds its tongues and sinks away. Flames are drawn from the back
// row to the front one, so nearer flames overlap farther ones like the reference's.
vec4 burst(vec2 p, float px)
{
    float boom=uMaterial._Phase-Collapse;
    if(boom<0) return vec4(0.0);
    float R=uMaterial._Radius;
    float r=length(p);
    vec4 centre=vec4(0.0);
    if(r<3)
    {
        // The orb's own flame grows out of the collapsed orb, as wide as its head, and sinks last.
        float rise=bxSmoothstep(0,0.18,boom);
        Fire fire=standing(p,vec2(0,-HeadR*0.85),mix(HeadR*0.85,0.55,rise)*(1-bxSmoothstep(0.9,1.3,boom)),
            mix(1,0.72,rise),boom*1.2+uMaterial._Seed,uMaterial._Seed,mix(0.35,1,rise)*(1-bxSmoothstep(0.8,1.2,boom)));
        // The blast, out and back in a quarter second. It is already bursting on the first frame, so the
        // collapsed orb never shows on its own.
        float blast=bxSmoothstep(-0.03,0.06,boom)*(1-bxSmoothstep(0.08,0.26,boom));
        if(blast>0)
        {
            float reach=mix(HeadR*0.85,1.05,blast);
            float d=r-reach;
            const float rays=9;
            float nearest=floor(atan(p.y,p.x)/TAU*rays+0.5);
            for(int k=-1;k<=1;k++)
            {
                float n=nearest+k;
                float id=n-rays*floor(n/rays);
                float rnd=randomCell(vec2(id,uMaterial._Seed+5.1));
                float rnd2=randomCell(vec2(id+2.6,uMaterial._Seed+0.7));
                float angle=(n+(rnd-0.5)*0.5)/rays*TAU;
                vec2 ray=vec2(cos(angle),sin(angle));
                d=smin(d,tongue(p,ray*reach*0.6,ray,reach*mix(0.6,1.4,rnd2),reach*mix(0.22,0.32,rnd),
                    (rnd-0.5)*0.9,(0.5-rnd2)*1.2),0.1);
            }
            fire.flame=smin(fire.flame,d,0.1);
            // Deep at its middle and pale at its tips, like a flame spread flat.
            fire.height=mix(fire.height,r/1.6,blast);
        }
        // Blobs of fire flung out of the burst, part of it until they fly clear.
        const float blobs=12;
        float nearestBlob=floor(atan(p.y,p.x)/TAU*blobs);
        for(int b=-1;b<=1;b++)
        {
            float n=nearestBlob+b;
            float id=n-blobs*floor(n/blobs);
            float rnd=randomCell(vec2(id,uMaterial._Seed+8.8));
            float rnd2=randomCell(vec2(id+3.3,uMaterial._Seed+1.1));
            float angle=(n+0.2+0.6*rnd)/blobs*TAU;
            vec2 ray=vec2(cos(angle),sin(angle));
            float land=mix(1.0,R*0.9,rnd2);
            float size=mix(0.07,0.1,rnd)*bxSmoothstep(0,0.05,boom)*(1-bxSmoothstep(land/9,land/9+0.1,boom));
            if(size>1e-3) fire.flame=smin(fire.flame,tongue(p,ray*(0.3+min(boom*9,land-0.3)),-ray,size*3.2,size,
                (rnd-0.5)*0.5,0),0.03);
        }
        centre=paint(fire,px);
    }
    vec4 col=vec4(0.0);
    bool drawn=false;
    float top=floor(p.y-0.12);
    float left=floor(p.x-1.3);
    for(int row=0;row<4;row++)
    {
        float j=top-row;
        if(!drawn&&j<0)
        {
            col=over(col,centre);
            drawn=true;
        }
        for(int column=0;column<3;column++)
        {
            vec2 cell=vec2(left+column,j);
            float rnd=randomCell(cell+uMaterial._Seed*1.7);
            float rnd2=randomCell(cell.yx+uMaterial._Seed*2.3+7.1);
            vec2 foot=cell+0.3+0.4*vec2(rnd,rnd2);
            float dist=length(foot);
            // None inside the orb's own flame, none whose body stands outside the blast, since a
            // flame rises above its foot, and fewer toward the edge.
            float body=length(foot+vec2(0,0.4));
            if(dist<0.95||body>R*0.92||randomCell(cell+uMaterial._Seed*3.9+2.2)>mix(0.95,0.65,body/R)) continue;
            // Lit as the flung fire lands, a flame burns for most of a second.
            float local=boom-dist/9-rnd*0.05;
            float life=mix(0.7,1.0,rnd2);
            if(local<0||local>life) continue;
            float size=mix(0.26,0.36,rnd)*mix(1,0.65,body/R)*mix(0.5,1,bxSmoothstep(0,0.1,local))
                *(1-bxSmoothstep(life-0.2,life,local));
            // Its horns, branches and glow reach no further.
            vec2 q=p-foot;
            if(abs(q.x)>size*1.5+0.18||q.y<-0.18||q.y>size*4.4+0.18) continue;
            float reach=bxSmoothstep(0,0.12,local)*(1-bxSmoothstep(life-0.3,life,local));
            col=over(col,paint(standing(p,foot,size,0.72,boom*1.3+rnd*9,rnd*31+uMaterial._Seed,reach),px));
        }
    }
    if(!drawn) col=over(col,centre);
    return col;
}

vec4 bxFragment(vec2 uv)
{
    // The quad's size in cells, and one screen pixel in cells, measured outside any branch.
    bool strip=uMaterial._Mode>0.5&&uMaterial._Mode<1.5;
    vec2 cells=strip?vec2(max(uMaterial._Length,0.3),TrailWidth):(uMaterial._Mode>1.5?(uMaterial._Radius+BurstMargin)*2:OrbCells*2);
    vec2 dx=dFdx(uv)*cells, dy=dFdy(uv)*cells;
    float px=sqrt(max(dot(dx,dx),dot(dy,dy)));
    vec4 col;
    if(uMaterial._Mode>1.5) col=burst((uv-0.5)*cells,px);
    else if(strip) col=trail(uv,px);
    else col=orb((uv-0.5)*cells,px);
    // The trail fades at its quad edges; round effects fade on a circle,
    // so a stray glow never shows the square outline.
    vec2 edge=abs(uv-0.5)*2;
    float bound=strip?max(edge.x,edge.y):length(edge);
    col*=1-bxSmoothstep(0.90,0.99,bound);
    return col*uMaterial._Opacity;
}

void main() { outColor=bxFragment(vUv); }
