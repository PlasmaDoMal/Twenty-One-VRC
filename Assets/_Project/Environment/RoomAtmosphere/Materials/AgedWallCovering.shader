Shader "TwentyOne/AgedWallCovering" {Properties{_Color("Charcoal",Color)=(.15,.155,.145,1) _Relief("Relief",Range(0,1))=.18} SubShader{Tags{"RenderType"="Opaque"} LOD 200 
CGPROGRAM

#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
fixed4 _Color;
float _Relief;
struct Input{float3 worldPos;
};
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
}void surf(Input IN,inout SurfaceOutputStandard o){float h=IN.worldPos.x+IN.worldPos.z;
float2 uv=float2(h,IN.worldPos.y);
float fibre=sin(h*1450+sin(IN.worldPos.y*12)*.3);
float grain=hash(floor(uv*650));
float stain=.84+.16*sin(h*3+sin(IN.worldPos.y*4));
float band=.91+.09*cos(h*75);
o.Albedo=_Color.rgb*(.87+.09*fibre+.08*grain)*stain*band;
o.Normal=normalize(float3(fibre*_Relief,(grain-.5)*.05,1));
o.Smoothness=.13;
o.Occlusion=.85;
o.Alpha=1;
}

ENDCG
}Fallback "Diffuse"}
