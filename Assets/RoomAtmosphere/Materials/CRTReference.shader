Shader "TwentyOne/CRTReference" {
Properties { _Tint("Screen tint",Color)=(.72,.79,.80,1) _Intensity("Intensity",Range(0,3))=1 }
SubShader {Tags{"RenderType"="Opaque"} Cull Off Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct a{float4 v:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct b{float4 v:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};float4 _Tint;float _Intensity;
b vert(a i){b o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_OUTPUT(b,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.v=UnityObjectToClipPos(i.v);o.uv=i.uv;return o;}
fixed4 frag(b i):SV_Target {float2 p=i.uv-.5;float scan=.87+.13*sin(i.uv.y*720);float band=.88+.12*sin(i.uv.y*43);float edge=1-.6*pow(saturate(length(p)*1.4),2);float cross=(1-smoothstep(.015,.032,min(abs(p.x-p.y*.65),abs(p.x+p.y*.65))))*(1-smoothstep(.12,.18,abs(p.y)));return float4((_Tint.rgb*scan*band*edge*.65+cross*float3(.13,.10,.10))*_Intensity,1);}
ENDCG}}}
