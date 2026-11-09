// Blossom Rally - road surface. Standard lighting + weather: when _BR_Wetness rises the road darkens
// and turns glossy (sky / probe reflections), puddles form in dips of a world-space noise field and
// show rain rings while _BR_Rain > 0. Set globally by BlossomRally.Atmosphere.
Shader "BlossomRally/Road"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _PuddleAmount ("Puddles", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _PuddleAmount;
        float _BR_Wetness;
        float _BR_Rain;
        float4 _BR_LightPools[16];      // set by Graphics/NightGlow: xyz ground centre, w radius
        float _BR_LightPoolCount;
        float4 _BR_LightPoolColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float vnoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float wet = saturate(_BR_Wetness);
            float2 wp = IN.worldPos.xz;
            float n = vnoise(wp * 0.16) * 0.65 + vnoise(wp * 0.55 + 7.1) * 0.35;
            float puddle = smoothstep(0.60, 0.67, n) * saturate((wet - 0.3) * 2.2) * _PuddleAmount;

            // rain rings inside puddles: one ring per 0.9 m cell, each on its own clock
            float ring = 0.0;
            if (_BR_Rain > 0.01 && puddle > 0.01)
            {
                float2 cellP = wp / 0.9;
                float2 cell = floor(cellP);
                float h = hash21(cell);
                float2 centre = cell + 0.25 + 0.5 * float2(hash21(cell + 3.1), hash21(cell + 9.7));
                float t = frac(_Time.y * (0.8 + h * 0.6) + h);
                float r = length(cellP - centre);
                ring = saturate(1.0 - abs(r - t * 0.55) * 18.0) * (1.0 - t) * _BR_Rain;
            }

            float3 albedo = c.rgb * lerp(1.0, 0.58, wet);
            albedo = lerp(albedo, c.rgb * 0.30, puddle);
            o.Albedo = albedo + ring * 0.08;
            // warm lantern glints on wet roads at night
            if (wet > 0.05 && _BR_LightPoolCount > 0.5)
            {
                half3 glow = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_BR_LightPoolCount) break;
                    float d = distance(IN.worldPos.xz, _BR_LightPools[i].xz) / (_BR_LightPools[i].w * 1.4);
                    glow += saturate(1.0 - d * d);
                }
                o.Emission = _BR_LightPoolColor.rgb * glow * wet * (0.25 + puddle * 0.6);
            }
            o.Metallic = 0;
            o.Smoothness = lerp(lerp(_Glossiness, 0.74, wet), 0.96, puddle) - ring * 0.25;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
