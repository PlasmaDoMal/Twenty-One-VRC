Shader "TwentyOne/WornFelt" {Properties{_MainTex("Weave",2D)="white"{} _Color("Felt",Color)=(.28,.30,.24,1)} SubShader{Tags{"RenderType"="Opaque"} LOD 200 CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;fixed4 _Color;struct Input{float3 worldPos;};void surf(Input IN,inout SurfaceOutputStandard o){fixed grain=tex2D(_MainTex,IN.worldPos.xz*5).r;float patch=.92+.08*sin(IN.worldPos.x*8+sin(IN.worldPos.z*4));o.Albedo=_Color.rgb*grain*patch;o.Metallic=0;o.Smoothness=.08;o.Alpha=1;}
ENDCG} FallBack "Diffuse"}
