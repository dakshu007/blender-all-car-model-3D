// Blossom Rally - vertex-coloured lit surface with world-space wind.
// Colour comes from the mesh vertex colour (authored in Blender, sRGB); the vertex
// colour ALPHA is the wind weight (0 = static, 1 = full sway). Casts and receives
// real-time shadows and uses scene fog. Works with static batching (wind is world-space).
// Low swaying plants (grass clumps, flowers) are also pushed aside by the player's car.
// Close-up detail (graphics upgrade): static surfaces get procedural world-space detail chosen by
// the vertex colour - barrel roof tiles on sloped reds / slates, stone blocks and paving on greys,
// wood grain on browns and lacquered pillars, mottled plaster on creams - fading out with distance.
// Driven by the globals _BR_DetailAmount (0 = off, exactly the old look) and _BR_DetailDist (m);
// _DetailPatterns = 0 on a material marks the terrain: instead of the building patterns it gets ground
// micro-detail chosen by colour - grass tufts with dry tips, rock cracks and strata, wind ripples on sand and
// layered canyon walls, wind-blown snow drifts with sparkling ice crystals - plus a sun-lit relief (cheap
// emboss towards _BR_SunDir). All inside the same distance fade, so Low (_BR_DetailAmount 0) is unchanged.
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
        float4 _BR_SunDir;     // xyz towards the sun, w daylight (Atmosphere)
        float4 _BR_SunColor;   // linear sun colour * intensity (Atmosphere)

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

        // c: sRGB colour (modified), sm: smoothness (modified), em: emission (linear, added)
        void BR_Detail (inout float3 c, inout half sm, inout float3 em, float3 wp, float3 n, float fade)
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
            else
            {
                // ---- terrain micro-detail by ground colour
                float3 L = _BR_SunDir.xyz;
                float2 lp = an.y > 0.7 ? L.xz : (an.x > an.z ? L.zy : L.xy);
                float reliefK = 0.05;
                if (mx > 0.78 && sat < 0.16)
                {
                    // snow: soft wind-blown drifts + ice crystals glinting in the sun
                    m *= lerp(0.975, 1.02, BR_DNoise(pc * 0.9) * 0.6 + BR_DNoise(pc * 3.7) * 0.4);
                    reliefK = 0.09;
                    float2 cellS = floor(wp.xz * 22.0 + wp.y * 7.0);
                    float hs = BR_DHash(cellS);
                    if (hs > 0.93)
                    {
                        float3 V = normalize(_WorldSpaceCameraPos - wp);
                        float3 jit = float3(BR_DHash(cellS + 1.7), BR_DHash(cellS + 4.3), BR_DHash(cellS + 8.9)) - 0.5;
                        float3 rn = normalize(n + jit * 1.2);
                        float3 H = normalize(L + V);
                        float glint = pow(saturate(dot(rn, H)), 160.0);
                        em += _BR_SunColor.rgb * (glint * 2.5 * fade * fade);
                    }
                    dsm = 0.1;
                }
                else if (hue > 0.03 && hue < 0.16 && sat > 0.22 && mx > 0.42)
                {
                    if (an.y > 0.75)
                    {
                        // sand: wind ripples + fine grain
                        float rip = sin(dot(wp.xz, float2(0.8, 0.6)) * 9.0 + BR_DNoise(wp.xz * 0.7) * 6.0) * 0.5 + 0.5;
                        m *= lerp(0.95, 1.03, rip) * lerp(0.97, 1.03, BR_DNoise(wp.xz * 11.0));
                        reliefK = 0.06;
                    }
                    else
                    {
                        // canyon walls: layered strata
                        float strata = sin(wp.y * 3.1 + BR_DNoise(pc * 0.35) * 4.0) * 0.5 + 0.5;
                        m *= lerp(0.88, 1.06, strata) * lerp(0.95, 1.04, BR_DNoise(pc * 5.0));
                        reliefK = 0.12;
                    }
                }
                else if (hue > 0.16 && hue < 0.48 && sat > 0.15)
                {
                    // grass: darker tufts, dry sunlit tips
                    float tuft = BR_DNoise(wp.xz * 2.3) * 0.55 + BR_DNoise(wp.xz * 9.0) * 0.45;
                    m *= lerp(0.90, 1.05, tuft);
                    c = lerp(c, c * float3(1.04, 1.03, 0.92), smoothstep(0.6, 0.9, tuft) * fade * 0.6);
                }
                else if (sat < 0.3)
                {
                    // rock: ridged cracks + strata
                    float cr = abs(BR_DNoise(pc * 1.6) - 0.5) * 2.0;
                    float strata = sin(wp.y * 4.0 + BR_DNoise(pc * 0.5) * 3.0) * 0.5 + 0.5;
                    m *= lerp(0.82, 1.0, smoothstep(0.0, 0.08, cr)) * lerp(0.94, 1.04, strata);
                    reliefK = 0.14;
                }
                // sun-lit relief: the ground undulation compared a step towards the sun (lit where it falls away)
                float2 rp = pc * 1.3;
                float relief = BR_DNoise(rp) - BR_DNoise(rp + lp * 0.35);
                m *= 1.0 + relief * reliefK * 2.0 * saturate(_BR_SunDir.w);
            }
            c = lerp(c, c * m, fade);
            sm = saturate(sm + dsm * fade);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.color.rgb * _Tint.rgb;
            half sm = _Glossiness;
            float3 em = float3(0, 0, 0);
            if (_BR_DetailAmount > 0.001 && IN.color.a < 0.05)
            {
                float dist = distance(_WorldSpaceCameraPos, IN.worldPos);
                float fade = saturate(1.0 - dist / max(_BR_DetailDist, 1.0)) * _BR_DetailAmount;
                if (fade > 0.001) BR_Detail(c, sm, em, IN.worldPos, normalize(IN.worldNormal), fade);
            }
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c;
            o.Metallic = 0;
            o.Smoothness = sm;
            o.Emission = em;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
