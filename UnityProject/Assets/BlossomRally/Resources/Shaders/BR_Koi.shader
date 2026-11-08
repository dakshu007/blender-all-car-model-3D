// Blossom Rally - koi seen through the lake surface. Drawn after the (transparent) water, so the
// fish are tinted towards the deep-water colour instead of being hidden by it.
// Vertex colour RGB = fish pattern (sRGB); vertex colour ALPHA = how deep the fish is (0 surface, 1 deep).
Shader "BlossomRally/Koi"
{
    Properties
    {
        _WaterTint ("Water Tint", Color) = (0.20, 0.50, 0.66, 1)
        _Submerge ("Submerge Tint", Range(0, 1)) = 0.45
        _Alpha ("Opacity", Range(0, 1)) = 0.9
        _Glossiness ("Smoothness", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        fixed4 _WaterTint;
        half _Submerge;
        half _Alpha;
        half _Glossiness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            float deep = saturate(IN.color.a);
            o.Albedo = lerp(c, _WaterTint.rgb, _Submerge * deep);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = _Alpha * (1.0 - 0.25 * deep);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
