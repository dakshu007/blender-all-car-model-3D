// Blossom Rally - glowing lanterns (vertex colour drives albedo + emission; bloom picks it up).
Shader "BlossomRally/VertexColorGlow"
{
    Properties
    {
        _Glow ("Glow", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        float _Glow;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c * 0.5;
            o.Emission = c * _Glow;
            o.Metallic = 0;
            o.Smoothness = 0.4;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
