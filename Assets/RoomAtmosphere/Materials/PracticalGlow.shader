Shader "TwentyOne/PracticalGlow" {Properties{_Color("Glow",Color)=(.65,.7,.75,1)} SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Blend One One ZWrite Off Cull Off Pass{CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct a{float4 v:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct b{float4 v:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};float4 _Color;
b vert(a i){b o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_OUTPUT(b,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 center=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;float sx=length(unity_ObjectToWorld._m00_m10_m20);float sy=length(unity_ObjectToWorld._m01_m11_m21);float3 world=center+UNITY_MATRIX_I_V._m00_m10_m20*i.v.x*sx+UNITY_MATRIX_I_V._m01_m11_m21*i.v.y*sy;o.v=mul(UNITY_MATRIX_VP,float4(world,1));o.uv=i.uv;return o;}
fixed4 frag(b i):SV_Target{float2 p=(i.uv-.5)*2;float r=length(p);float haze=exp(-r*r*18)*.28;float core=exp(-r*r*250)*1.4;float streak=exp(-abs(p.y)*110)*exp(-abs(p.x)*4)*.14;float edge=1-smoothstep(.65,1,r);return float4(_Color.rgb*(haze+core+streak)*edge,1);}
ENDCG}}}
