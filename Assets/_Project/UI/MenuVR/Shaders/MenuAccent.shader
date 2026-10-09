Shader "MenuVR/MenuAccent"
{
    // Barra de acento: a linha luminosa que corre no topo do painel e o tracinho
    // que cresce na base de cada botao. O gradiente desliza sozinho, o que da
    // movimento sem custo de script.
    //
    // O crescimento do tracinho e feito pelo script mudando o tamanho da
    // imagem, nao por uma propriedade daqui: assim a barra cresce de verdade e
    // o brilho continua na velocidade certa.
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)

        _ColorA ("Accent A", Color) = (0.16, 0.93, 0.7, 1)
        _ColorB ("Accent B", Color) = (0.95, 0.82, 0.5, 1)
        _ColorC ("Accent C", Color) = (0.55, 0.55, 0.98, 1)

        _Speed ("Flow Speed", Float) = 0.35
        _Sharpness ("Highlight Sharpness", Range(0.5, 8)) = 2.2
        _HeadWidth ("Highlight Width", Range(0.02, 0.6)) = 0.22
        _Intensity ("Intensity", Float) = 1.0
        _FadeEnds ("Fade Ends", Range(0, 4)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _Speed;
            float _Sharpness;
            float _HeadWidth;
            float _Intensity;
            float _FadeEnds;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Gradiente de tres paradas, deslizando e voltando, para nunca dar
                // um salto de cor no meio do ciclo.
                float travel = frac(i.uv.x * 0.75 + _Time.y * _Speed);
                float3 grad;
                if (travel < 0.3333)
                {
                    grad = lerp(_ColorA.rgb, _ColorB.rgb, travel * 3.0);
                }
                else if (travel < 0.6666)
                {
                    grad = lerp(_ColorB.rgb, _ColorC.rgb, (travel - 0.3333) * 3.0);
                }
                else
                {
                    grad = lerp(_ColorC.rgb, _ColorA.rgb, (travel - 0.6666) * 3.0);
                }

                // Uma cabeca de luz mais brilhante correndo por cima do gradiente.
                float head = frac(i.uv.x * 0.5 - _Time.y * _Speed * 1.6);
                float pulse = pow(1.0 - head, _Sharpness) * smoothstep(0.0, _HeadWidth, head);

                // Suaviza as pontas de cima e de baixo, para a barra de um pixel
                // nao virar um risco duro.
                float band = pow(abs(sin(i.uv.y * 3.14159265)), _FadeEnds);

                float peak = max(grad.a, max(_ColorB.a, _ColorC.a));
                fixed3 rgb = grad * _Intensity * band;
                rgb += _ColorA.rgb * pulse * 0.7 * band;

                float a = clamp(peak * _Intensity, 0.0, 1.0) * band * i.color.a;
                return fixed4(rgb * i.color.rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
