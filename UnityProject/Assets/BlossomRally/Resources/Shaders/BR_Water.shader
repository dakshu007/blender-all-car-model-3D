// Blossom Rally - stylised animated water (procedural ripples, fresnel tint, sun glints, fog).
Shader "BlossomRally/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.55, 0.82, 0.88, 0.75)
        _Deep ("Deep", Color) = (0.20, 0.50, 0.68, 0.92)
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        fixed4 _Shallow;
        fixed4 _Deep;
        float _RippleScale;
        float _RippleSpeed;

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            v.vertex.y += sin(wp.x * 0.35 + _Time.y * 1.2) * 0.03 + sin(wp.z * 0.42 + _Time.y * 0.9) * 0.03;
        }

        float ripple(float2 p, float t)
        {
            return sin(p.x * 1.7 + t) * 0.5 + sin(p.y * 2.3 - t * 1.3) * 0.35 + sin((p.x + p.y) * 3.1 + t * 2.1) * 0.15;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _RippleScale;
            float t = _Time.y * _RippleSpeed * 4.0;
            float e = 0.08;
            float h = ripple(p, t);
            float hx = ripple(p + float2(e, 0), t);
            float hz = ripple(p + float2(0, e), t);
            o.Normal = normalize(float3((h - hx) * 2.5, (h - hz) * 2.5, 1.0));
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 3.0);
            fixed4 c = lerp(_Shallow, _Deep, saturate(0.35 + 0.5 * h * 0.5 + fres * 0.4));
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = 0.93;
            o.Alpha = saturate(c.a + fres * 0.2);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
