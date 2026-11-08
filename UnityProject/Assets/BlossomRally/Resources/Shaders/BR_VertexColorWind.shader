// Blossom Rally - vertex-coloured lit surface with world-space wind.
// Colour comes from the mesh vertex colour (authored in Blender, sRGB); the vertex
// colour ALPHA is the wind weight (0 = static, 1 = full sway). Casts and receives
// real-time shadows and uses scene fog. Works with static batching (wind is world-space).
// Low swaying plants (grass clumps, flowers) are also pushed aside by the player's car.
Shader "BlossomRally/VertexColorWind"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0, 1)) = 0.12
        _WindStrength ("Wind Strength", Float) = 0.16
        _WindSpeed ("Wind Speed", Float) = 1.3
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "BR_Interact.cginc"

        struct Input
        {
            float4 color : COLOR;
        };

        half _Glossiness;
        float _WindStrength;
        float _WindSpeed;
        fixed4 _Tint;
        float4 _BR_WindGust;   // set globally by BlossomRally.Atmosphere: x = gust multiplier

        void vert (inout appdata_full v)
        {
            float w = v.color.a;
            if (w > 0.001)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y * _WindSpeed;
                float ph = wp.x * 0.071 + wp.z * 0.053;
                float s = sin(t + ph) * 0.6 + sin(t * 2.37 + ph * 1.9) * 0.3 + sin(t * 5.3 + wp.x * 0.9 + wp.z * 0.7) * 0.1;
                float gust = 1.0 + _BR_WindGust.x;
                float3 off = float3(1.0, 0.0, 0.35) * (s * _WindStrength * w * gust);
                off.y = -abs(s) * _WindStrength * 0.25 * w * w;
                if (w > 0.2) off += BR_Interact(wp, w) * 0.8;
                v.vertex.xyz += mul((float3x3)unity_WorldToObject, off);
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb * _Tint.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
