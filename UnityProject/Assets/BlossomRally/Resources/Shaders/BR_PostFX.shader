// Blossom Rally - camera post effect for the built-in pipeline (driven by BlossomRally.PostFX).
// Pass 0: bloom prefilter (soft threshold) + downsample
// Pass 1: 4-tap box blur (down/up sample)
// Pass 2: final composite - bloom, filmic tonemap, dreamy pastel grade, vignette
Shader "Hidden/BlossomRally/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _Knee;
    float _BloomIntensity;
    float _Exposure;
    float _Saturation;
    float _Contrast;
    float4 _Lift;          // pastel "faded" lift colour (rgb) and amount (a)
    float4 _Tint;          // warm/pink grade multiplier
    float4 _ShadowTint;    // cool lilac shadows
    float _Vignette;
    float _BlurOffset;

    struct v2f_img2
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f_img2 vert (appdata_img v)
    {
        v2f_img2 o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 boxSample (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half4 fragPrefilter (v2f_img2 i) : SV_Target
    {
        half3 c = boxSample(i.uv, 1.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 0.00001);
        half contrib = max(soft, br - _Threshold) / max(br, 0.00001);
        return half4(c * contrib, 1);
    }

    half4 fragBlur (v2f_img2 i) : SV_Target
    {
        return half4(boxSample(i.uv, _BlurOffset), 1);
    }

    half3 tonemapACES (half3 x)
    {
        const half a = 2.51;
        const half b = 0.03;
        const half c = 2.43;
        const half d = 0.59;
        const half e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    half4 fragComposite (v2f_img2 i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure;
        c = tonemapACES(c);
        // grade in perceptual space
        half3 g = LinearToGammaSpace(c);
        half lum = dot(g, half3(0.299, 0.587, 0.114));
        g = lerp(half3(lum, lum, lum), g, _Saturation);
        g = (g - 0.5) * _Contrast + 0.5;
        g *= _Tint.rgb;
        half shadowMask = saturate(1.0 - lum * 1.6);
        g = lerp(g, g * _ShadowTint.rgb, shadowMask * _ShadowTint.a);
        g = lerp(g, _Lift.rgb, _Lift.a * (1.0 - lum * 0.7));   // faded pastel lift
        float2 v = i.uv - 0.5;
        half vig = 1.0 - dot(v, v) * _Vignette;
        g *= saturate(vig);
        c = GammaToLinearSpace(saturate(g));
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
    FallBack Off
}
