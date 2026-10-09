Shader "IntroMenuVRChat/ReferenceLogo"
{
 Properties { [PerRendererData] _MainTex("Reference logo",2D)="black"{} }
 SubShader {
 Tags {"Queue"="Overlay+998" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Cull Off ZWrite Off ZTest Always
 Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
 sampler2D _MainTex;
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 fixed luminance=tex2D(_MainTex,i.uv).r;
 return fixed4(i.color.rgb*0.78,saturate(luminance/0.78)*i.color.a);}
 ENDCG
 }
 }
 Fallback Off
}