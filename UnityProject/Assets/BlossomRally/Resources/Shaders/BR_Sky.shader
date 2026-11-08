// Blossom Rally - soft pastel gradient sky with a hazy sun glow and drifting procedural clouds
// (skybox material). Cloud coverage, colour and speed are driven by Atmosphere's weather presets.
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

                float cover = 0.0;
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
