// Cone de luz volumetrico falso, no estilo TwentyOne.
// Aditivo, sem sombra nem iluminacao, para simular o facho visivel
// das luminarias de galpao. Usado pelos cones sob as pendentes.
Shader "TwentyOne/LightShaft"
{
    Properties
    {
        _Color ("Shaft", Color) = (1, 0.86, 0.66, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.22
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct a { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct b { float4 v : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Color;
            float _Intensity;

            b vert(a i)
            {
                b o;
                o.v = UnityObjectToClipPos(i.v);
                o.uv = i.uv;
                return o;
            }

            fixed4 frag(b i) : SV_Target
            {
                // some para baixo: uv.y vai de 1 no topo do cone a 0 na base
                float fade = pow(saturate(i.uv.y), 2.0);
                // expoente alto concentra o brilho no miolo e apaga a silhueta,
                // senao o cone le como um triangulo solido em vez de um facho
                float rim = pow(abs(sin(i.uv.x * 6.28318)), 2.2);
                return float4(_Color.rgb * _Intensity * fade * rim, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
