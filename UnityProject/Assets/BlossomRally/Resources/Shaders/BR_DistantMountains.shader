// Blossom Rally - distant mountain silhouettes in the shan shui style (DistantScenery rings).
// uv.x = ring layer (0 near .. 1 far), uv.y = 0 at the foot .. 1 on the ridge line.
// Ink-dark crests, mist pooling at the feet, brush texture, haze towards the horizon colour that is
// capped (_Haze.z) so fog never wipes the horizon out completely. Unlit, no scene fog.
// The ShadowCaster pass puts the rings into the camera depth texture (sky masks for sun shafts / mist)
// but culls them from directional (orthographic) shadow maps so they never shade the valley.
Shader "BlossomRally/DistantMountains"
{
    Properties
    {
        _NearCol ("Near", Color) = (0.42, 0.50, 0.62, 1)
        _FarCol ("Far", Color) = (0.72, 0.76, 0.86, 1)
        _MistCol ("Mist", Color) = (0.92, 0.88, 0.88, 1)
        _Horizon ("Horizon", Color) = (1, 0.87, 0.86, 1)
        _Haze ("Haze (base, per layer, max, mist)", Vector) = (0.2, 0.11, 0.7, 0.6)
        _SunDir ("Sun dir (w = strength)", Vector) = (0, 0.5, 0.5, 1)
        _SunCol ("Sun", Color) = (1, 0.9, 0.8, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+450" "RenderType" = "BR_Far" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _NearCol, _FarCol, _MistCol, _Horizon, _SunCol;
            float4 _Haze;
            float4 _SunDir;
            sampler2D _BR_Noise;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float layer = i.uv.x;
                float hf = i.uv.y;
                float3 wp = i.wp;
                half n = tex2D(_BR_Noise, wp.xz * 0.0021 + layer * 0.37).r;
                half streak = tex2D(_BR_Noise, float2((wp.x + wp.z) * 0.012, wp.y * 0.0035 + layer)).b;
                half3 col = lerp(_NearCol.rgb, _FarCol.rgb, layer);
                // ink wash: darker crest, lighter body, dry-brush texture
                col *= lerp(1.12, 0.84, smoothstep(0.35, 1.0, hf + (n - 0.5) * 0.2));
                col *= lerp(0.93, 1.05, n) * lerp(0.96, 1.03, streak);
                // sun-facing slopes catch a little warm light
                float2 outward = normalize(wp.xz);
                half sunSide = saturate(dot(-outward, normalize(_SunDir.xz + 1e-4))) * saturate(_SunDir.y * 3.0 + 0.3);
                col += _SunCol.rgb * sunSide * 0.06 * _SunDir.w * hf;
                // mist pooling at the feet
                half mist = 1.0 - smoothstep(0.0, 0.5 + 0.12 * layer, hf + (n - 0.5) * 0.18);
                col = lerp(col, _MistCol.rgb, mist * _Haze.w);
                // aerial perspective, capped so the silhouettes always survive
                half hz = min(saturate(_Haze.x + layer * _Haze.y * 4.0), _Haze.z);
                col = lerp(col, _Horizon.rgb, hz);
                return fixed4(col, 1);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vertSC
            #pragma fragment fragSC
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vertSC (appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER(o)
                // orthographic projection = directional light shadow map: drop the triangle
                if (UNITY_MATRIX_P[3][3] > 0.5) o.pos = float4(0, 0, -2, 1);
                return o;
            }

            float4 fragSC (v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack Off
}
