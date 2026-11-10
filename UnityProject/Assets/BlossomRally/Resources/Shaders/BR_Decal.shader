// Blossom Rally - car decals (number panels, sponsor stickers, plates, gauge faces, trip computer).
// The decal mesh is cut from the body mesh (or is a flat quad); per vertex:
//   uv0.xy = position inside this decal (0..1; outside = clipped, so a triangle may overhang the sticker)
//   uv1    = atlas rect (x, y, w, h) of the sticker in _MainTex
//   colour = tint (rgb) and self-illumination (a, scaled by _Glow) for backlit gauges / LCDs
// Alpha-tested so it writes depth (the tinted glass in front sorts correctly), pulled towards the camera
// with Offset to sit on the paint, lit like the body, and muddied with the same height rule as BR_CarPaint.
Shader "BlossomRally/Decal"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Glow ("Glow", Float) = 0
        _Dirt ("Dirt", Range(0, 1)) = 0
        _DirtColor ("Dirt Colour", Color) = (0.42, 0.33, 0.24, 1)
        _CarExt ("Car Extents", Vector) = (0.9, 1.35, 2.1, 0)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest+10" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        Offset -1, -1

        CGPROGRAM
        #pragma surface surf Standard vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        half _Glossiness;
        half _Glow;
        half _Dirt;
        fixed4 _DirtColor;
        float4 _CarExt;
        float4x4 _CarW2L;

        struct Input
        {
            float2 local;
            float4 rect;
            float4 color : COLOR;
            float3 worldPos;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.local = v.texcoord.xy;
            o.rect = v.texcoord1;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.local;
            clip(min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));
            fixed4 c = tex2D(_MainTex, IN.rect.xy + saturate(uv) * IN.rect.zw);
            clip(c.a - 0.5);
            float3 tint = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            tint = GammaToLinearSpace(tint);
            #endif
            float3 albedo = c.rgb * tint;
            // mud from the sills up, like the paint (no noise here: stickers are smoother)
            float3 p = mul(_CarW2L, float4(IN.worldPos, 1.0)).xyz;
            float dirtLine = lerp(0.12, _CarExt.y * 0.75, _Dirt);
            float mud = saturate((dirtLine - p.y) / 0.35) * step(0.001, _Dirt);
            albedo = lerp(albedo, _DirtColor.rgb, saturate(mud * 0.85 + _Dirt * 0.18));
            o.Albedo = albedo;
            o.Emission = c.rgb * tint * IN.color.a * _Glow;
            o.Metallic = 0.0;
            o.Smoothness = _Glossiness * (1.0 - mud * 0.7);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack Off
}
