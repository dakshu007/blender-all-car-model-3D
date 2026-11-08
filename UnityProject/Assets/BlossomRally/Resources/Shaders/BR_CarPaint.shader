// Blossom Rally - car paint. Standard lighting on the livery texture, plus:
//  * colour choice: pixels close to the livery's paint colour (_PaintKey) are recoloured to _PaintColor
//    keeping the original shading (_PaintMix = 0 keeps the factory livery)
//  * patterns in car space (_CarW2L = world -> car matrix, _CarExt = half width, height, half length):
//    1 racing stripes, 2 two-tone, 3 door roundels, 4 blossom sprigs
//  * mud / dust build-up from the sills upwards (_Dirt 0..1, _DirtColor), washed glossy by rain
Shader "BlossomRally/CarPaint"
{
    Properties
    {
        _MainTex ("Livery", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.55
        _PaintKey ("Paint Key", Color) = (1, 0, 0, 1)
        _PaintColor ("Paint Colour", Color) = (1, 0, 0, 1)
        _PaintMix ("Paint Mix", Range(0, 1)) = 0
        _Pattern ("Pattern", Float) = 0
        _PatternColor ("Pattern Colour", Color) = (1, 1, 1, 1)
        _Dirt ("Dirt", Range(0, 1)) = 0
        _DirtColor ("Dirt Colour", Color) = (0.42, 0.33, 0.24, 1)
        _CarExt ("Car Extents", Vector) = (0.9, 1.35, 2.1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        half _Glossiness;
        fixed4 _PaintKey;
        fixed4 _PaintColor;
        half _PaintMix;
        float _Pattern;
        fixed4 _PatternColor;
        half _Dirt;
        fixed4 _DirtColor;
        float4 _CarExt;
        float4x4 _CarW2L;
        float _BR_Wetness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
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
            float3 c = tex2D(_MainTex, IN.uv_MainTex).rgb;
            float3 lumW = float3(0.3, 0.55, 0.15);
            float keyDist = distance(c, _PaintKey.rgb) / (0.12 + dot(_PaintKey.rgb, lumW) * 0.5);
            float keyMask = 1.0 - smoothstep(0.35, 0.8, keyDist);
            float shade = clamp(dot(c, lumW) / max(dot(_PaintKey.rgb, lumW), 0.02), 0.45, 1.5);
            float3 albedo = lerp(c, _PaintColor.rgb * shade, keyMask * _PaintMix);

            float3 p = mul(_CarW2L, float4(IN.worldPos, 1.0)).xyz;
            float3 nrm = normalize(mul((float3x3)_CarW2L, IN.worldNormal));
            float pat = 0.0;
            float3 patCol = _PatternColor.rgb;
            if (_Pattern > 0.5 && _Pattern < 1.5)            // racing stripes over the top surfaces
            {
                float ax = abs(p.x);
                pat = step(abs(ax - 0.17), 0.075) * step(0.25, nrm.y);
                pat += step(abs(ax - 0.17), 0.075) * step(0.5, abs(nrm.z)) * step(_CarExt.y * 0.45, p.y);
            }
            else if (_Pattern > 1.5 && _Pattern < 2.5)       // two-tone: lower body
            {
                pat = 1.0 - smoothstep(_CarExt.y * 0.40, _CarExt.y * 0.42, p.y);
            }
            else if (_Pattern > 2.5 && _Pattern < 3.5)       // door roundels with a ring
            {
                float2 q = float2(p.z - _CarExt.z * 0.05, p.y - _CarExt.y * 0.45);
                float r = length(q);
                float side = step(0.6, abs(nrm.x));
                pat = side * step(r, 0.30);
                patCol = lerp(float3(0.95, 0.94, 0.9), _PatternColor.rgb, step(0.24, r));
            }
            else if (_Pattern > 3.5)                          // blossom sprigs on the sides, denser towards the rear
            {
                float side = step(0.55, abs(nrm.x));
                float2 g = float2(p.z, p.y) / 0.34;
                float2 cell = floor(g);
                float h = hash21(cell + floor(sign(p.x)) * 13.0);
                float2 ctr = cell + 0.5 + (float2(hash21(cell + 5.3), hash21(cell + 8.1)) - 0.5) * 0.5;
                float2 d = g - ctr;
                float ang = atan2(d.y, d.x);
                float petal = 0.33 * (0.7 + 0.3 * cos(ang * 5.0 + h * 6.28));
                float rear = saturate(0.6 - p.z / max(_CarExt.z, 0.5));
                float on = step(1.0 - rear * 0.8, h) * step(p.y, _CarExt.y * 0.62);
                pat = side * on * step(length(d), petal);
                patCol = lerp(_PatternColor.rgb, float3(1.0, 0.97, 0.97), step(length(d), 0.08));
            }
            albedo = lerp(albedo, patCol, saturate(pat) * keyMask);

            // mud and dust
            float dirtLine = lerp(0.12, _CarExt.y * 0.75, _Dirt);
            float splat = vnoise(float2(p.x * 5.0 + p.z * 3.0, p.y * 7.0)) * 0.6 + vnoise(float2(p.z * 13.0, p.y * 17.0 + p.x * 9.0)) * 0.4;
            float mud = saturate((dirtLine - p.y) / 0.35) * smoothstep(0.35, 0.65, splat + _Dirt * 0.35);
            float dust = _Dirt * 0.22 * smoothstep(0.2, 0.8, splat);
            float dirt = saturate(mud * 0.92 + dust) * step(0.001, _Dirt);
            albedo = lerp(albedo, _DirtColor.rgb, dirt);

            float wet = saturate(_BR_Wetness);
            o.Albedo = albedo;
            o.Metallic = 0;
            o.Smoothness = lerp(lerp(_Glossiness, 0.12, dirt), 0.82, wet * (1.0 - dirt * 0.5));
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
