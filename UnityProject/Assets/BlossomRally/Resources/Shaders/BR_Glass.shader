// Blossom Rally - tinted car glass.
Shader "BlossomRally/Glass"
{
    Properties
    {
        _Color ("Tint", Color) = (0.04, 0.06, 0.08, 0.82)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
        };

        fixed4 _Color;
        half _Glossiness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), 4.0);
            o.Albedo = _Color.rgb;
            o.Metallic = 0.1;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(_Color.a + fres * 0.15);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
