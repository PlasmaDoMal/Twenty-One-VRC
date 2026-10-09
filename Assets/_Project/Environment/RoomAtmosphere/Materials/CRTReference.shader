Shader "TwentyOne/CRTReference"
{
    Properties
    {
        _Tint("Screen tint", Color) = (.72,.79,.80,1)
        _Intensity("Intensity", Range(0,3)) = 1

        [Header(Text)]
        [Toggle] _ShowText("Show text", Float) = 1
        [Enum(None,0,YouLost,1,YouWon,2,Draw,3,P1Won,4,P2Won,5)] _Message("Message (overrides time)", Float) = 0
        _Seconds("Time in seconds (MM:SS). 0 = show the X", Float) = 0
        _TextColor("Text color", Color) = (.85,.95,.90,1)
        _TextHeight("Time text height (fraction of screen height)", Range(.05,.8)) = .25
        _MessageWidth("Message width (fraction of screen width)", Range(.1,1)) = .8
        _Aspect("Surface aspect (width / height)", Float) = 1
        [Toggle] _FlipX("Mirror X (fixes reversed text)", Float) = 1
        [Toggle] _ColonBlink("Blink colon", Float) = 0

        [Header(CRT effect)]
        [Toggle] _CRT("Enable CRT effect", Float) = 1
        _Curvature("Screen curvature", Range(0,0.5)) = 0.15
        _MaskStrength("RGB phosphor mask", Range(0,1)) = 0.35
        _MaskCount("RGB mask triads across screen", Float) = 200
        _Flicker("Flicker", Range(0,0.2)) = 0.03
        _Noise("Static noise", Range(0,0.3)) = 0.05
        _Roll("Rolling bar", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off

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
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                nointerpolation float4 digits : TEXCOORD1; // M M S S, computed per vertex
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4  _Tint;
            half   _Intensity;
            half   _ShowText;
            float  _Message;
            float  _Seconds;
            half4  _TextColor;
            float  _TextHeight;
            float  _MessageWidth;
            float  _Aspect;
            half   _FlipX;
            half   _ColonBlink;
            half   _CRT;
            float  _Curvature;
            float  _MaskStrength;
            float  _MaskCount;
            float  _Flicker;
            float  _Noise;
            float  _Roll;

            // 7-segment masks (bit0=a top, 1=b, 2=c, 3=d bottom, 4=e, 5=f, 6=g middle)
            static const int SEG[10] = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
            static const float OX[4] = { 0.0, 0.67, 1.66, 2.33 };

            // 5x7 font, 7 rows per glyph (top to bottom), bit4 = leftmost column
            // glyph ids: Y=0 O=1 U=2 L=3 S=4 T=5 W=6 N=7
            static const int FONT[98] = {
                17,17,10,4,4,4,4,          // Y
                14,17,17,17,17,17,14,      // O
                17,17,17,17,17,17,14,      // U
                16,16,16,16,16,16,31,      // L
                15,16,16,14,1,1,30,        // S
                31,4,4,4,4,4,4,            // T
                17,17,17,21,21,27,17,      // W
                17,25,21,19,17,17,17,      // N
                30,17,17,17,17,17,30,      // D
                30,17,17,30,20,18,17,      // R
                14,17,17,31,17,17,17,      // A
                30,17,17,30,16,16,16,      // P
                4,12,4,4,4,4,14,          // 1
                14,17,1,2,4,8,31          // 2
            };
            // -1 = space
            static const int MSG_LOST[8] = { 0,1,2,-1,3,1,4,5 }; // YOU LOST
            static const int MSG_WON[8]  = { 0,1,2,-1,6,1,7,-1 }; // YOU WON

            static const int MSG_DRAW[4] = { 8,9,10,6 };
            static const int MSG_P1[6] = { 11,12,-1,6,1,7 };
            static const int MSG_P2[6] = { 11,13,-1,6,1,7 };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                uint t  = (uint)clamp(floor(_Seconds), 0.0, 5999.0);
                uint mm = t / 60u;
                uint ss = t - mm * 60u;
                uint d0 = mm / 10u;
                uint d2 = ss / 10u;
                o.digits = float4(d0, mm - d0 * 10u, d2, ss - d2 * 10u);
                return o;
            }

            float box(float2 q, float2 lo, float2 hi)
            {
                float2 s = step(lo, q) * step(q, hi);
                return s.x * s.y;
            }

            float digit(float2 q, int d)
            {
                int m = SEG[d];
                float r = 0;
                r += ( m       & 1) * box(q, float2(.06,.88), float2(.49,1.0));   // a
                r += ((m >> 1) & 1) * box(q, float2(.43,.53), float2(.55,.94));   // b
                r += ((m >> 2) & 1) * box(q, float2(.43,.06), float2(.55,.47));   // c
                r += ((m >> 3) & 1) * box(q, float2(.06,.00), float2(.49,.12));   // d
                r += ((m >> 4) & 1) * box(q, float2(.00,.06), float2(.12,.47));   // e
                r += ((m >> 5) & 1) * box(q, float2(.00,.53), float2(.12,.94));   // f
                r += ((m >> 6) & 1) * box(q, float2(.06,.44), float2(.49,.56));   // g
                return r;
            }

            // p: centered uv (-.5..+.5). which: 1 = YOU LOST, 2 = YOU WON
            float message(float2 p, int which)
            {
                uint n = which == 1 ? 8u : which == 2 ? 7u : which == 3 ? 4u : 6u;
                float cols = (float)(n * 6u - 1u);
                float ps = _MessageWidth * _Aspect / cols;           // size of one LED dot
                float2 q = float2(p.x * _Aspect, p.y) / ps + float2(cols * 0.5, 3.5);

                if (q.x < 0.0 || q.x >= cols || q.y < 0.0 || q.y >= 7.0) return 0.0;

                uint cx  = (uint)q.x;
                uint ci  = cx / 6u;
                uint col = cx - ci * 6u;
                if (col > 4u) return 0.0;                            // gap between letters

                int g = which == 1 ? MSG_LOST[ci] : which == 2 ? MSG_WON[ci] : which == 3 ? MSG_DRAW[ci] : which == 4 ? MSG_P1[ci] : MSG_P2[ci];
                if (g < 0) return 0.0;                               // space

                uint row  = 6u - (uint)q.y;
                int bits  = FONT[g * 7 + (int)row];
                float on  = (float)((bits >> (4 - (int)col)) & 1);

                float2 f = frac(q);                                  // slight gap = LED look
                float2 s = step(0.08, f) * step(f, 0.92);
                return on * s.x * s.y;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                if (_FlipX > 0.5) p.x = -p.x;

                // CRT: barrel distortion + soft rounded screen border
                half crtMask = 1;
                if (_CRT > 0.5)
                {
                    float2 c = p * 2.0;
                    c += c * (c.yx * c.yx) * _Curvature;
                    p = c * 0.5;
                    float2 e = smoothstep(0.0, 0.012, 0.5 - abs(p));
                    crtMask = (half)(e.x * e.y);
                }
                float2 suv = p + 0.5; // screen uv after distortion

                half scan = (half)(0.87 + 0.13 * sin(suv.y * 720.0));
                half band = (half)(0.88 + 0.12 * sin(suv.y * 43.0));
                // saturate(len*1.4)^2 == saturate(dot(p,p)*1.96): no sqrt, no pow
                half edge = (half)(1.0 - 0.6 * saturate(dot(p, p) * 1.96));

                half3 col = _Tint.rgb * (scan * band * edge * (half)0.65);

                int msg = (int)round(_Message);

                if (_ShowText > 0.5 && (msg >= 1 || _Seconds >= 1.0))
                {
                    float lit = 0.0;

                    if (msg >= 1)
                    {
                        lit = message(p, msg);
                    }
                    else
                    {
                        // centered time text: width 2.88, height 1
                        float2 q = float2(p.x * _Aspect, p.y) / _TextHeight + float2(1.44, 0.5);

                        if (q.y >= 0.0 && q.y <= 1.0 && q.x >= 0.0 && q.x <= 2.88)
                        {
                            int4 d = (int4)round(i.digits);
                            lit = digit(q - float2(OX[0], 0.0), d.x)
                                + digit(q - float2(OX[1], 0.0), d.y)
                                + digit(q - float2(OX[2], 0.0), d.z)
                                + digit(q - float2(OX[3], 0.0), d.w);

                            float colon = box(q, float2(1.38,.24), float2(1.50,.36))
                                        + box(q, float2(1.38,.64), float2(1.50,.76));
                            lit += colon * lerp(1.0, step(0.5, frac(_Time.y)), _ColonBlink);
                        }
                    }

                    col += (half)lit * _TextColor.rgb * scan;
                }
                else
                {
                    // original X (shown when _Seconds is 0 or text is off)
                    float xm = (1.0 - smoothstep(0.015, 0.032, min(abs(p.x - p.y * 0.65), abs(p.x + p.y * 0.65))))
                             * (1.0 - smoothstep(0.12, 0.18, abs(p.y)));
                    col += (half)xm * half3(0.13, 0.10, 0.10);
                }

                if (_CRT > 0.5)
                {
                    // RGB phosphor triads
                    float3 tri = 0.75 + 0.25 * cos(6.28318 * (suv.x * _MaskCount + float3(0.0, 0.3333, 0.6667)));
                    col *= (half3)lerp(float3(1, 1, 1), tri * 1.3333, _MaskStrength);

                    // flicker + slow rolling bar
                    col *= (half)(1.0 - _Flicker * 0.5 * (1.0 + sin(_Time.y * 95.0)));
                    col *= (half)(1.0 + _Roll * 0.25 * sin((suv.y * 3.0 - _Time.y * 0.6) * 6.28318));

                    // static noise (cheap hash, changes ~24x per second)
                    float2 nc = floor(suv * float2(240.0 * _Aspect, 240.0)) + floor(_Time.y * 24.0) * float2(17.3, 31.7);
                    float n = frac(52.9829189 * frac(dot(nc, float2(0.06711056, 0.00583715))));
                    col += (half)((n - 0.5) * _Noise) * _Tint.rgb;

                    col *= crtMask;
                }

                return half4(col * _Intensity, 1);
            }
            ENDCG
        }
    }
}
