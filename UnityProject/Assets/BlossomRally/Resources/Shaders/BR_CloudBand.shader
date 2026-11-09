// Blossom Rally - soft cloud band around the far mountains (DistantScenery). The tall far peaks poke
// through it. uv.x wraps 6x around the ring (tileable noise), uv.y = 0 bottom .. 1 top.
Shader "BlossomRally/CloudBand"
{
    Properties
    {
        _Lit ("Lit", Color) = (1, 0.97, 0.96, 1)
        _Shade ("Shade", Color) = (0.78, 0.76, 0.84, 1)
        _Horizon ("Horizon", Color) = (1, 0.87, 0.86, 1)
        _Params ("x density, y opacity, z haze", Vector) = (0.6, 0.7, 0.3, 0)
        _Drift ("Drift", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-60" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Lit, _Shade, _Horizon;
            float4 _Params, _Drift;
            sampler2D _BR_Noise;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                half n = tex2D(_BR_Noise, float2(uv.x + _Drift.x, uv.y * 0.3 + 0.13)).r * 0.65
                       + tex2D(_BR_Noise, float2(uv.x * 3.0 - _Drift.x * 1.7, uv.y * 0.9 + _Drift.y)).g * 0.35;
                half prof = smoothstep(0.0, 0.35, uv.y) * (1.0 - smoothstep(0.5, 1.0, uv.y + (n - 0.5) * 0.4));
                half a = saturate((n * (0.6 + prof) - (1.0 - _Params.x) * 0.75) * 3.0) * prof * _Params.y;
                half3 col = lerp(_Shade.rgb, _Lit.rgb, saturate(uv.y * 0.9 + n * 0.5 - 0.1));
                col = lerp(col, _Horizon.rgb, _Params.z);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
    FallBack Off
}
