// Blossom Rally - dense procedural grass blades (built by WorldBuilder).
// Vertex colour RGB = blade colour (root dark -> tip light, sRGB), vertex ALPHA = height along the
// blade (0 root, 1 tip). UV0 = (root.x, root.y, root.z, random phase) in world space.
// Wind sway, gusts, car push / flatten / shaking wake (BR_Interact.cginc) and a distance fade
// that sinks blades into the ground before the camera's grass cull distance.
Shader "BlossomRally/Grass"
{
    Properties
    {
        _WindStrength ("Wind Strength", Float) = 0.14
        _WindSpeed ("Wind Speed", Float) = 1.7
        _FadeStart ("Fade Start", Float) = 88
        _FadeEnd ("Fade End", Float) = 126
        _Translucency ("Tip Translucency", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf GrassWrap vertex:vert addshadow nolightmap nodynlightmap nometa noforwardadd
        #pragma target 3.0
        #include "BR_Interact.cginc"

        float _WindStrength;
        float _WindSpeed;
        float _FadeStart;
        float _FadeEnd;
        half _Translucency;
        float4 _BR_WindGust;

        struct Input
        {
            float4 color : COLOR;
        };

        half4 LightingGrassWrap (SurfaceOutput s, half3 lightDir, half atten)
        {
            // wrapped diffuse: blades are thin, light bleeds through
            half ndl = saturate(dot(s.Normal, lightDir) * 0.55 + 0.45);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (ndl * atten);
            c.a = s.Alpha;
            return c;
        }

        void vert (inout appdata_full v)
        {
            float h = v.color.a;
            float3 root = v.texcoord.xyz;
            float phase = v.texcoord.w;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

            // distance fade (shrink towards the root)
            float dist = distance(root, _WorldSpaceCameraPos);
            float fade = saturate((_FadeEnd - dist) / max(_FadeEnd - _FadeStart, 1.0));
            wp = root + (wp - root) * fade;

            // wind: rolling waves across the field + per-blade flutter
            float t = _Time.y * _WindSpeed;
            float wave = sin(t + root.x * 0.09 + root.z * 0.06) * 0.6 + sin(t * 2.1 + root.x * 0.23 - root.z * 0.17) * 0.3;
            float flutter = sin(_Time.y * 6.0 + phase * 6.2831) * 0.12;
            float gust = 1.0 + _BR_WindGust.x * 1.3;
            float bend = (wave * gust + flutter) * _WindStrength * h * h;
            float3 off = float3(bend, -abs(bend) * 0.35, bend * 0.4);

            off += BR_Interact(root, h) * fade;
            wp += off;
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1.0)).xyz;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c;
            o.Emission = c * _Translucency * 0.18 * IN.color.a;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
