// Blossom Rally - scrolling white-water streaks for the waterfall ribbon, foam rings and foam strips.
// UV.x runs across the flow (columns), UV.y along it (scrolls with time, so the pattern streams in +V).
// Vertex colour alpha fades the edges; vertex colour RGB is a (white) tint in sRGB.
Shader "BlossomRally/FlowFoam"
{
    Properties
    {
        _WaterColor ("Water", Color) = (0.78, 0.90, 0.97, 0.45)
        _FoamColor ("Foam", Color) = (1, 1, 1, 0.95)
        _Tiling ("Tiling (columns, rows)", Vector) = (6, 3, 0, 0)
        _Speed ("Scroll Speed", Float) = 1.2
        _Streak ("Streak Amount", Range(0, 1)) = 0.8
        _Lit ("Sun Lit Amount", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _WaterColor;
            fixed4 _FoamColor;
            float4 _Tiling;
            float _Speed;
            float _Streak;
            float _Lit;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            float hash11(float p)
            {
                return frac(sin(p * 127.1 + 1.7) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _Tiling.xy;
                float4 c = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = GammaToLinearSpace(c.rgb);
                #endif
                o.color = c;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                // coarse streaks: one random phase / speed / length per column
                float col = floor(i.uv.x);
                float fx = frac(i.uv.x);
                float r = hash11(col);
                float s = frac(i.uv.y - t * (0.75 + 0.5 * r) + r * 5.0);
                float streak = smoothstep(0.0, 0.15, s) * (1.0 - smoothstep(0.3 + 0.35 * r, 0.95, s));
                streak *= smoothstep(0.0, 0.3, fx) * (1.0 - smoothstep(0.7, 1.0, fx));
                // finer, faster second layer offset by half a column
                float u2 = i.uv.x * 2.3 + 0.5;
                float col2 = floor(u2);
                float fx2 = frac(u2);
                float r2 = hash11(col2 + 17.0);
                float s2 = frac(i.uv.y * 1.7 - t * (1.1 + 0.4 * r2) + r2 * 3.0);
                float streak2 = smoothstep(0.0, 0.1, s2) * (1.0 - smoothstep(0.2, 0.6, s2));
                streak2 *= smoothstep(0.0, 0.35, fx2) * (1.0 - smoothstep(0.65, 1.0, fx2));
                float f = saturate(max(streak, streak2 * 0.75));
                f = lerp(0.5, f, _Streak);
                fixed4 c = lerp(_WaterColor, _FoamColor, f);
                c.rgb *= i.color.rgb;
                c.a *= i.color.a;
                // unity_AmbientSky is a global (always set), unlike per-object SH probes in an unlit pass
                float3 amb = unity_AmbientSky.rgb;
                c.rgb *= lerp(1.0, amb * 0.8 + 0.45, _Lit);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
