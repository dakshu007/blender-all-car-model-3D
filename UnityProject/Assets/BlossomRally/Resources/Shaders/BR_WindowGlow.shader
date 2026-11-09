// Blossom Rally - warm glowing house windows at night (NightGlow). Additive panels sitting just in
// front of the cream paper panels and behind the real lattice bars, so the lattice reads as dark lines
// over warm paper light. uv1 = (intensity, phase, candle flicker 0/1, random).
Shader "BlossomRally/WindowGlow"
{
    Properties
    {
        _Color ("Glow", Color) = (1.0, 0.66, 0.34, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _BR_Night;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 data : TEXCOORD1; UNITY_FOG_COORDS(2) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.data = v.uv1;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                // brighter in the middle like a lamp behind paper, soft at the frame
                half glow = saturate(1.0 - dot(c, c) * 2.2);
                half edge = smoothstep(0.0, 0.08, 0.5 - abs(c.x)) * smoothstep(0.0, 0.08, 0.5 - abs(c.y));
                half flick = 1.0 + i.data.z * (sin(_Time.y * 9.0 + i.data.y * 7.0) * 0.06 + sin(_Time.y * 15.3 + i.data.y) * 0.04);
                half3 tint = lerp(_Color.rgb, half3(1.0, 0.80, 0.52), i.data.w * 0.5);
                half3 col = tint * (0.55 + glow * 0.9) * edge * i.data.x * flick * 1.6;
                col *= smoothstep(0.25, 0.8, _BR_Night);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
