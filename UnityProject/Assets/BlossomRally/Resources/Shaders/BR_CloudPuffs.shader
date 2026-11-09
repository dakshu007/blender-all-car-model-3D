// Blossom Rally - drifting cumulus over the valley, seen from below (DistantScenery).
// Each puff is a camera-facing quad: vertex position = puff centre, uv = corner, uv1 = (width, height,
// random, cloud index). Positions wrap inside a 2.2 km tile as the wind (_Drift) moves them; clouds
// whose index is above _Params.x collapse (coverage follows the weather without rebuilding the mesh).
Shader "BlossomRally/CloudPuffs"
{
    Properties
    {
        _MainTex ("Puff", 2D) = "white" {}
        _Lit ("Lit", Color) = (1, 0.97, 0.96, 1)
        _Shade ("Shade", Color) = (0.74, 0.72, 0.80, 1)
        _Horizon ("Horizon", Color) = (1, 0.87, 0.86, 1)
        _Params ("x visible fraction, y opacity", Vector) = (1, 0.85, 0, 0)
        _Drift ("Drift", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-55" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            sampler2D _MainTex;
            fixed4 _Lit, _Shade, _Horizon;
            float4 _Params, _Drift;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 fade : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 c = mul(unity_ObjectToWorld, v.vertex).xyz;
                c.xz = frac((c.xz + _Drift.xz + 1100.0) / 2200.0) * 2200.0 - 1100.0;
                float dist = distance(c, _WorldSpaceCameraPos);
                float4 vp = mul(UNITY_MATRIX_V, float4(c, 1.0));
                vp.xy += (v.uv - 0.5) * v.uv1.xy;
                o.pos = mul(UNITY_MATRIX_P, vp);
                if (v.uv1.w > _Params.x) o.pos = float4(0, 0, -2, 1);
                o.uv = v.uv;
                // fade near the camera (title flyover) and into the horizon far away
                o.fade = float2(saturate((dist - 60.0) / 120.0) * (1.0 - saturate((dist - 1200.0) / 500.0)), saturate(dist / 1600.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                half3 col = lerp(_Shade.rgb, _Lit.rgb, t.r);
                col = lerp(col, _Horizon.rgb, i.fade.y * 0.55);
                return fixed4(col, t.a * i.fade.x * _Params.y);
            }
            ENDCG
        }
    }
    FallBack Off
}
