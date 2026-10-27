#version 450
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 required
layout(location = 0) out vec2 vUv;
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
#include "BX_ShaderCommon.glsl"
#include "BX_SoulDrainUniforms.glsl"
void main()
{
vec4 position=vec4(inPosition,1.0);
if (uMaterial._Mode > 1.5 && uMaterial._Mode < 2.5)
{
    // One continuous head-first pull: rotation, stretching and travel
    // all begin together, without a separate floating or turning stage.
    float progress = saturate(uMaterial._Progress);
    vec2 soulPos = position.xz;
    vec2 direction = normalize(uMaterial._Pull.xy+vec2(0.0001,0));
    vec2 normal = vec2(-direction.y,direction.x);
    vec2 bodyAxis = vec2(sin(radians(uMaterial._SoulAngle)),cos(radians(uMaterial._SoulAngle)));
    float angle = atan(bodyAxis.x*direction.y-bodyAxis.y*direction.x,dot(bodyAxis,direction));
    float turn = 1-pow(1-progress,2.5);
    float sine, cosine;
    sine=sin(angle*turn); cosine=cos(angle*turn);
    soulPos = vec2(cosine*soulPos.x-sine*soulPos.y,sine*soulPos.x+cosine*soulPos.y);
    float leading = saturate(0.5 + dot(position.xz,bodyAxis)*0.82);
    soulPos -= normal*dot(soulPos,normal)*0.62*bxSmoothstep(0,1,progress);
    float travel = bxSmoothstep(0,1,pow(progress,mix(2.3,0.85,leading)));
    float arc = sin(travel*UNITY_PI);
    vec2 destination = uMaterial._Pull.xy;
    vec2 deformed = mix(soulPos,destination,travel);
    deformed += normal * arc * (0.09 + sin(inUv0.xy.y*9+uMaterial._Phase*4)*0.035);
    deformed += normal * sin(inUv0.xy.y*17+uMaterial._Phase*5) * 0.012 * progress*(1-travel);
    position.xz = deformed;
}
gl_Position=uVertex.ViewProj*push.modelMatrix*position;
vUv=inUv0.xy;
}
