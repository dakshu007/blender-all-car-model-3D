// Blossom Rally - swaying glowing props (paper lanterns, lit lattice windows, incense tips).
// Vertex colour RGB (sRGB) drives albedo + emission (_Glow, raised at night by WorldProps so bloom picks
// it up); vertex colour ALPHA is the wind sway weight (0 static) - same world-space wind as VertexColorWind.
Shader "BlossomRally/PropGlow"
{
    Properties
    {
        _Glow ("Glow", Float) = 0.5
        _WindStrength ("Wind Strength", Float) = 0.28
        _WindSpeed ("Wind Speed", Float) = 1.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        float _Glow;
        float _WindStrength;
        float _WindSpeed;
        float4 _BR_WindGust;   // set globally by BlossomRally.Atmosphere: x = gust multiplier

        void vert (inout appdata_full v)
        {
            float w = v.color.a;
            if (w > 0.001)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y * _WindSpeed;
                float ph = wp.x * 0.071 + wp.z * 0.053;
                float s = sin(t + ph) * 0.65 + sin(t * 2.1 + ph * 1.7) * 0.35;
                float gust = 1.0 + _BR_WindGust.x;
                float3 off = float3(1.0, 0.0, 0.35) * (s * _WindStrength * w * gust);
                off.y = -abs(s) * _WindStrength * 0.15 * w;
                v.vertex.xyz += mul((float3x3)unity_WorldToObject, off);
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c * 0.55;
            o.Emission = c * _Glow;
            o.Metallic = 0;
            o.Smoothness = 0.35;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
