// Blossom Rally - vertex-coloured lit surface with world-space wind.
// Colour comes from the mesh vertex colour (authored in Blender, sRGB); the vertex
// colour ALPHA is the wind weight (0 = static, 1 = full sway). Casts and receives
// real-time shadows and uses scene fog. Works with static batching (wind is world-space).
// Low swaying plants (grass clumps, flowers) are also pushed aside by the player's car.
// Close-up detail (graphics upgrade): static surfaces get procedural world-space detail chosen by
// the vertex colour - barrel roof tiles on sloped reds / slates, stone blocks and paving on greys,
// wood grain on browns and lacquered pillars, mottled plaster on creams - fading out with distance.
// Driven by the globals _BR_DetailAmount (0 = off, exactly the old look) and _BR_DetailDist (m);
// _DetailPatterns = 0 on a material keeps only the soft variation (used for the terrain).
Shader "BlossomRally/VertexColorWind"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0, 1)) = 0.12
        _WindStrength ("Wind Strength", Float) = 0.16
        _WindSpeed ("Wind Speed", Float) = 1.3
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _DetailPatterns ("Detail Patterns", Float) = 1
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
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        float _WindStrength;
        float _WindSpeed;
        fixed4 _Tint;
        half _DetailPatterns;
        float4 _BR_WindGust;   // set globally by BlossomRally.Atmosphere: x = gust multiplier
        float _BR_DetailAmount;
        float _BR_DetailDist;

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

        // ---------------------------------------------------------------- procedural detail
        float BR_DHash (float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float BR_DNoise (float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(BR_DHash(i), BR_DHash(i + float2(1, 0)), u.x), lerp(BR_DHash(i + float2(0, 1)), BR_DHash(i + float2(1, 1)), u.x), u.y);
        }

        // mortar mask of a running-bond block grid (1 inside a block, 0 in the joints)
        float BR_Blocks (float2 p, float2 size, float joint, out float2 cell)
        {
            float row = floor(p.y / size.y);
            float2 q = float2(p.x / size.x + row * 0.5, p.y / size.y);
            cell = floor(q);
            float2 f = frac(q);
            float2 e = min(f, 1.0 - f) * size;
            return smoothstep(0.0, joint, min(e.x, e.y));
        }

        // c: sRGB colour (modified), sm: smoothness (modified)
        void BR_Detail (inout float3 c, inout half sm, float3 wp, float3 n, float fade)
        {
            float mx = max(c.r, max(c.g, c.b));
            float mn = min(c.r, min(c.g, c.b));
            float d = mx - mn;
            float sat = d / max(mx, 1e-3);
            float hue = 0.0;
            if (d > 1e-4)
            {
                if (mx == c.r) hue = (c.g - c.b) / d;
                else if (mx == c.g) hue = 2.0 + (c.b - c.r) / d;
                else hue = 4.0 + (c.r - c.g) / d;
                hue = frac(hue / 6.0);
            }
            float3 an = abs(n);
            float2 pc = an.y > 0.7 ? wp.xz : (an.x > an.z ? wp.zy : wp.xy);
            float m = lerp(0.965, 1.035, BR_DNoise(pc * 3.1));        // soft variation everywhere
            half dsm = 0.0;
            if (_DetailPatterns > 0.5)
            {
                bool red = (hue < 0.045 || hue > 0.95) && sat > 0.45;
                bool dark = sat < 0.3 && mx < 0.42;
                bool slope = an.y > 0.25 && an.y < 0.97;
                if ((red || dark) && slope)
                {
                    // barrel roof tiles: channels down the slope, overlapping courses
                    float2 sd = normalize(n.xz + 1e-4);
                    float u = dot(wp.xz, float2(-sd.y, sd.x));
                    float ch = abs(frac(u / 0.26) - 0.5) * 2.0;
                    float barrel = sqrt(saturate(1.0 - ch * ch));
                    float row = frac(wp.y / 0.15 + floor(u / 0.26) * 0.37);
                    float jit = BR_DHash(float2(floor(u / 0.26), floor(wp.y / 0.15)));
                    m *= lerp(0.80, 1.07, barrel) * lerp(0.84, 1.0, smoothstep(0.0, 0.14, row)) * lerp(0.94, 1.05, jit);
                    dsm = barrel * 0.12;
                }
                else if (sat < 0.22 && mx > 0.36 && mx < 0.84)
                {
                    // stone: running-bond blocks on walls, paving slabs on flat tops
                    float2 cell;
                    float joint;
                    if (an.y > 0.7) joint = BR_Blocks(pc, float2(0.5, 0.5), 0.025, cell);
                    else joint = BR_Blocks(pc, float2(0.55, 0.28), 0.022, cell);
                    float jit = BR_DHash(cell + 17.0);
                    m *= lerp(0.80, 1.0, joint) * lerp(0.93, 1.06, jit) * lerp(0.95, 1.04, BR_DNoise(pc * 7.0));
                }
                else if ((hue >= 0.025 && hue < 0.13 && sat > 0.25 && mx < 0.6) || (red && an.y <= 0.25))
                {
                    // wood grain (lacquered pillars keep it very subtle)
                    float2 g = an.y > 0.7 ? float2(pc.x * 0.6, pc.y * 16.0) : float2((wp.x + wp.z) * 14.0, wp.y * 0.7);
                    float grain = BR_DNoise(g) * 0.6 + BR_DNoise(g * float2(2.1, 2.3)) * 0.4;
                    m *= lerp(red ? 0.96 : 0.88, red ? 1.03 : 1.07, grain);
                    dsm = red ? 0.15 : 0.0;
                }
                else if (sat < 0.25 && mx >= 0.84)
                {
                    // plaster / paper: gentle trowel mottling
                    m *= lerp(0.95, 1.03, BR_DNoise(pc * 1.4) * 0.6 + BR_DNoise(pc * 6.0) * 0.4);
                }
            }
            c = lerp(c, c * m, fade);
            sm = saturate(sm + dsm * fade);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb * _Tint.rgb;
            half sm = _Glossiness;
            if (_BR_DetailAmount > 0.001 && IN.color.a < 0.05)
            {
                float dist = distance(_WorldSpaceCameraPos, IN.worldPos);
                float fade = saturate(1.0 - dist / max(_BR_DetailDist, 1.0)) * _BR_DetailAmount;
                if (fade > 0.001) BR_Detail(c, sm, IN.worldPos, normalize(IN.worldNormal), fade);
            }
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c;
            o.Metallic = 0;
            o.Smoothness = sm;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
