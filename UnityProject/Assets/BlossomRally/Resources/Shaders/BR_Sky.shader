// Blossom Rally - soft pastel gradient sky with a hazy sun glow and drifting procedural clouds
// (skybox material). Cloud coverage, colour and speed are driven by Atmosphere's weather presets.
// Graphics upgrade, global _BR_SkyQ (0 = exactly the original sky, used on Low):
//   1 (Medium) : warm scattering band along the horizon under a low sun, rosy anti-twilight band, crisp sun
//                disc, a real moon disc with maria at night, a second layer of fainter coloured stars
//   2 (High+)  : clouds lit from the sun's side (self-shadowing), drifting high cirrus, the milky way
Shader "BlossomRally/Sky"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.42, 0.62, 0.90, 1)
        _Horizon ("Horizon", Color) = (1.00, 0.86, 0.84, 1)
        _Bottom ("Below Horizon", Color) = (0.86, 0.80, 0.82, 1)
        _SunColor ("Sun Glow", Color) = (1.00, 0.86, 0.66, 1)
        _SunDir ("Sun Direction", Vector) = (0.3, 0.5, 0.4, 0)
        _Exposure ("Exposure", Float) = 1.0
        _Clouds ("Cloud Coverage", Range(0, 1)) = 0.3
        _CloudColor ("Cloud Lit", Color) = (1.0, 0.96, 0.96, 1)
        _CloudShade ("Cloud Shade", Color) = (0.74, 0.72, 0.80, 1)
        _CloudSpeed ("Cloud Speed", Float) = 0.004
        _Stars ("Stars", Range(0, 1)) = 0
        _MoonDisk ("Moon", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Horizon;
            fixed4 _Bottom;
            fixed4 _SunColor;
            float4 _SunDir;
            float _Exposure;
            float _Clouds;
            fixed4 _CloudColor;
            fixed4 _CloudShade;
            float _CloudSpeed;
            float _Stars;
            float _MoonDisk;
            float _BR_SkyQ;        // global (GraphicsHub), not a material property

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

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
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * vnoise(p);
                    p = p * 2.03 + float2(17.1, 9.7);
                    a *= 0.5;
                }
                return v;
            }

            float fbm3(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += a * vnoise(p);
                    p = p * 2.03 + float2(17.1, 9.7);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col;
                if (h >= 0.0)
                {
                    float t = pow(saturate(h), 0.55);
                    col = lerp(_Horizon.rgb, _Top.rgb, t);
                }
                else
                {
                    col = lerp(_Horizon.rgb, _Bottom.rgb, saturate(-h * 4.0));
                }
                float3 s = normalize(_SunDir.xyz);
                float sd = saturate(dot(d, s));
                float3 glow = _SunColor.rgb * (pow(sd, 6.0) * 0.35 + pow(sd, 64.0) * 0.8);
                float3 disk = _SunColor.rgb * pow(sd, 900.0) * 3.0;
                float sq = _BR_SkyQ;
                if (sq > 0.5)
                {
                    // scattering: a warm band along the horizon under a low sun, strongest towards its azimuth,
                    // and the rosy anti-twilight band (belt of Venus) on the opposite side
                    float lowSun = saturate(1.0 - s.y * 3.0) * saturate(s.y * 8.0 + 1.0);
                    float2 dxz = normalize(d.xz + float2(1e-5, 0.0));
                    float2 sxz = normalize(s.xz + float2(1e-5, 0.0));
                    float az = dot(dxz, sxz) * 0.5 + 0.5;
                    float band = exp(-abs(h) * 7.0);
                    float day = saturate(1.0 - _MoonDisk * 1.4);
                    col += _SunColor.rgb * (band * lowSun * az * az * az * 0.35 * day);
                    col += float3(0.95, 0.62, 0.72) * (exp(-abs(h - 0.06) * 14.0) * lowSun * (1.0 - az) * 0.10 * day);
                    col += _Horizon.rgb * (exp(-abs(h) * 30.0) * 0.06);
                    // crisp sun disc (bloom turns it into a glowing orb), gone once the moon takes over
                    disk += _SunColor.rgb * (smoothstep(0.99985, 0.99993, sd) * 5.0 * day);
                    // the moon: a disc with soft maria and a darker limb, at the night light's position
                    float moonOn = saturate(_MoonDisk * 2.0 - 0.8);
                    if (moonOn > 0.001 && sd > 0.995)
                    {
                        float3 mr = normalize(cross(s, float3(0.0, 1.0, 0.0)) + float3(1e-4, 0.0, 0.0));
                        float3 mu = cross(mr, s);
                        float2 mp = float2(dot(d, mr), dot(d, mu)) / 0.021;
                        float r2 = dot(mp, mp);
                        float disc = 1.0 - smoothstep(0.88, 1.0, r2);
                        float maria = vnoise(mp * 2.3 + 3.1) * 0.6 + vnoise(mp * 5.7 + 11.3) * 0.4;
                        float shade = lerp(0.70, 1.0, smoothstep(0.38, 0.62, maria)) * lerp(0.78, 1.0, sqrt(saturate(1.0 - r2)));
                        disk += float3(1.0, 0.97, 0.90) * (disc * shade * 1.6 * moonOn);
                    }
                }

                // stars (night): one candidate star per cell of a sky grid, twinkling
                if (_Stars > 0.001 && h > 0.0)
                {
                    float2 suv = d.xz / (h + 0.35) * 70.0;
                    float2 cell = floor(suv);
                    float rnd = hash21(cell);
                    float2 spos = cell + float2(hash21(cell + 17.3), hash21(cell + 41.9));
                    float sdist = length(suv - spos);
                    float star = step(0.93, rnd) * saturate(1.0 - sdist * 3.5);
                    float tw = 0.65 + 0.35 * sin(_Time.y * (2.0 + rnd * 5.0) + rnd * 40.0);
                    col += float3(0.9, 0.93, 1.0) * star * star * tw * _Stars * 1.6 * smoothstep(0.02, 0.25, h);
                    if (sq > 0.5)
                    {
                        // a denser layer of faint stars in warm and cool tints
                        float2 suv2 = d.xz / (h + 0.35) * 165.0;
                        float2 cell2 = floor(suv2);
                        float rnd2 = hash21(cell2 + 91.7);
                        float2 spos2 = cell2 + float2(hash21(cell2 + 7.1), hash21(cell2 + 3.3));
                        float star2 = step(0.9, rnd2) * saturate(1.0 - length(suv2 - spos2) * 4.0);
                        float3 stint = lerp(float3(1.0, 0.86, 0.74), float3(0.76, 0.86, 1.0), hash21(cell2 + 5.9));
                        col += stint * star2 * star2 * _Stars * 0.55 * smoothstep(0.05, 0.3, h);
                        if (sq > 1.5)
                        {
                            // the milky way: a soft mottled band across the night sky
                            float3 bn = float3(0.36, 0.52, 0.77);
                            float bd = dot(d, bn);
                            float2 muv = d.xz / (h + 0.3);
                            float mw = vnoise(muv * 6.0) * 0.6 + vnoise(muv * 17.0 + 5.3) * 0.4;
                            col += float3(0.55, 0.60, 0.85) * (exp(-bd * bd * 18.0) * smoothstep(0.3, 0.8, mw) * _Stars * 0.10 * smoothstep(0.05, 0.4, h));
                        }
                    }
                }
                // moon halo
                col += _SunColor.rgb * _MoonDisk * (pow(sd, 300.0) * 1.2 + pow(sd, 18.0) * 0.12);

                float cover = 0.0;
                if (sq > 1.5 && h > 0.0)
                {
                    // high cirrus streaks on fair days (under the cumulus, fade in overcast)
                    float cirAmt = saturate(_Clouds * 4.0) * saturate((0.8 - _Clouds) * 3.0);
                    if (cirAmt > 0.001)
                    {
                        float2 cuv = d.xz / (h + 0.2) * 0.35;
                        float2 cdr = _Time.y * _CloudSpeed * float2(1.4, 0.5) * 6.0;
                        float2 q = float2(cuv.x * 0.9 + cuv.y * 0.45, cuv.y * 0.9 - cuv.x * 0.45);
                        float ci = vnoise(float2(q.x * 1.2, q.y * 9.0) + cdr) * 0.65 + vnoise(float2(q.x * 2.7, q.y * 19.0) - cdr * 1.3) * 0.35;
                        float cir = smoothstep(0.55, 0.85, ci) * smoothstep(0.0, 0.3, h) * cirAmt;
                        col = lerp(col, _CloudColor.rgb * 1.05 + _SunColor.rgb * pow(sd, 6.0) * 0.3, cir * 0.35);
                    }
                }
                if (_Clouds > 0.001 && h > 0.0)
                {
                    // project onto a flat cloud deck so clouds shrink toward the horizon
                    float2 uv = d.xz / (h + 0.12) * 0.6;
                    float2 drift = _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    float n = fbm(uv + drift * 6.0);
                    float n2 = fbm(uv * 2.7 - drift * 9.0);
                    n = n * 0.75 + n2 * 0.25;
                    float thr = 1.0 - _Clouds * 0.85 - 0.08;
                    cover = smoothstep(thr - 0.06, thr + 0.20, n) * smoothstep(0.0, 0.22, h);
                    float dens = smoothstep(thr, thr + 0.45, n);
                    float3 cc = lerp(_CloudColor.rgb, _CloudShade.rgb, dens * 0.85);
                    if (sq > 1.5)
                    {
                        // self-shadowing: thinner a step towards the sun = the edge facing it catches the light
                        float2 toSun = normalize(s.xz + float2(1e-4, 0.0)) * 0.09;
                        float ns = fbm3(uv + drift * 6.0 + toSun) * 0.75 + n2 * 0.25;
                        float lit = saturate((n - ns) * 5.0 + 0.5);
                        cc = lerp(cc * 0.86, cc * 1.06 + _SunColor.rgb * 0.08, lit);
                    }
                    cc += _SunColor.rgb * pow(sd, 10.0) * 0.45 * (1.0 - dens);   // silver lining near the sun
                    // fade distant clouds into the horizon haze
                    cc = lerp(_Horizon.rgb, cc, smoothstep(0.0, 0.35, h));
                    col = lerp(col, cc, cover * 0.95);
                }
                col += glow * (1.0 - cover * 0.6) + disk * (1.0 - cover);
                return fixed4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
