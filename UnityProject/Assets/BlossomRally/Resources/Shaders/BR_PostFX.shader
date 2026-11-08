// Blossom Rally - camera post effect for the built-in pipeline (driven by BlossomRally.PostFX).
// Pass 0: bloom prefilter (soft threshold) + downsample
// Pass 1: 4-tap box blur (down/up sample)
// Pass 2: final composite - bloom, filmic tonemap, dreamy pastel grade, vignette
//         (+ photo mode filter / letterbox / watermark when BR_PHOTO is enabled)
// Pass 3: depth of field - circle of confusion from the depth texture + half-res downsample
// Pass 4: depth of field - gather (disc) blur weighted by each sample's circle of confusion
// Pass 5: depth of field - combine sharp and blurred image by the full-res circle of confusion
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

    // photo mode
    float4 _FParams;       // x saturation, y contrast, z monochrome, w grain
    float4 _FTint;         // rgb multiplier
    float4 _FShadow;       // shadow tint (rgb) and amount (a)
    float4 _FLift;         // lift colour (rgb) and amount (a)
    float _Letterbox;      // bar height per side (0..0.45 of the screen)
    sampler2D _LogoTex;
    float4 _LogoRect;      // xy = bottom-left, zw = size (screen uv)
    float _LogoAlpha;

    // depth of field
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    sampler2D _DofTex;
    float4 _DofParams;     // x focus distance (m), y strength, z max blur radius (texels of the pass source)

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

    // uv for textures other than _MainTex (depth, bloom / blur targets, screen-anchored overlays):
    // on D3D-like platforms with anti-aliasing the source comes in flipped relative to them.
    float2 ScreenUV (float2 uv)
    {
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            uv.y = 1.0 - uv.y;
    #endif
        return uv;
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

    // photo filter, applied in perceptual space after the base grade
    half3 PhotoFilter (half3 g, float2 suv)
    {
        half lum = dot(g, half3(0.299, 0.587, 0.114));
        g = lerp(half3(lum, lum, lum), g, _FParams.x);
        g = lerp(g, half3(lum, lum, lum), _FParams.z);
        g = (g - 0.5) * _FParams.y + 0.5;
        g *= _FTint.rgb;
        half sm = saturate(1.0 - lum * 1.8);
        g = lerp(g, g * _FShadow.rgb, sm * _FShadow.a);
        g = lerp(g, _FLift.rgb, _FLift.a * (1.0 - lum * 0.6));
        float2 px = floor(suv * abs(_MainTex_TexelSize.zw));
        float n = frac(sin(dot(px, float2(12.9898, 78.233))) * 43758.5453);
        g += (half)(n - 0.5) * _FParams.w;
        return g;
    }

    half4 fragComposite (v2f_img2 i) : SV_Target
    {
        float2 suv = ScreenUV(i.uv);
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, suv).rgb * _BloomIntensity;
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
    #ifdef BR_PHOTO
        g = PhotoFilter(g, suv);
    #endif
        float2 v = i.uv - 0.5;
        half vig = 1.0 - dot(v, v) * _Vignette;
        g *= saturate(vig);
    #ifdef BR_PHOTO
        // letterbox bars
        g *= step(_Letterbox, suv.y) * step(suv.y, 1.0 - _Letterbox);
        // watermark (texture holds straight-alpha sRGB values, blended in perceptual space)
        float2 luv = (suv - _LogoRect.xy) / _LogoRect.zw;
        half4 lg = tex2D(_LogoTex, saturate(luv));
        half inside = step(0.0, luv.x) * step(luv.x, 1.0) * step(0.0, luv.y) * step(luv.y, 1.0);
        g = lerp(saturate(g), lg.rgb, saturate(lg.a * inside * _LogoAlpha));
    #endif
        c = GammaToLinearSpace(saturate(g));
        return half4(c, 1);
    }

    // ---------------------------------------------------------------- depth of field
    // signed circle of confusion (-1 near .. 0 in focus .. +1 far)
    float CircleOfConfusion (float2 suv)
    {
        float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
        return clamp((d - _DofParams.x) / max(d, 0.001) * _DofParams.y, -1.0, 1.0);
    }

    half4 fragDofPrefilter (v2f_img2 i) : SV_Target
    {
        half3 c = min(boxSample(i.uv, 1.0), 64.0);        // tame HDR fireflies
        float coc = CircleOfConfusion(ScreenUV(i.uv));
        return half4(c, coc * 0.5 + 0.5);                 // encoded 0..1 so any target format works
    }

    static const float2 kDisk[18] =
    {
        float2( 0.5,        0.0),       float2( 0.25,       0.4330127),
        float2(-0.25,       0.4330127), float2(-0.5,        0.0),
        float2(-0.25,      -0.4330127), float2( 0.25,      -0.4330127),
        float2( 0.9659258,  0.2588190), float2( 0.7071068,  0.7071068),
        float2( 0.2588190,  0.9659258), float2(-0.2588190,  0.9659258),
        float2(-0.7071068,  0.7071068), float2(-0.9659258,  0.2588190),
        float2(-0.9659258, -0.2588190), float2(-0.7071068, -0.7071068),
        float2(-0.2588190, -0.9659258), float2( 0.2588190, -0.9659258),
        float2( 0.7071068, -0.7071068), float2( 0.9659258, -0.2588190)
    };

    half4 fragDofBlur (v2f_img2 i) : SV_Target
    {
        half4 c0 = tex2D(_MainTex, i.uv);
        float2 stepUV = _MainTex_TexelSize.xy * _DofParams.z;
        half3 acc = c0.rgb;
        half wsum = 1.0;
        [unroll]
        for (int k = 0; k < 18; k++)
        {
            float2 o = kDisk[k];
            half r = (half)length(o);
            half4 s = tex2D(_MainTex, i.uv + o * stepUV);
            half coc = abs(s.a * 2.0 - 1.0);
            // a sample spreads onto this pixel only if its own blur disc reaches this far
            half w = saturate((coc - r) * 4.0 + 1.0);
            acc += s.rgb * w;
            wsum += w;
        }
        return half4(acc / wsum, c0.a);
    }

    half4 fragDofCombine (v2f_img2 i) : SV_Target
    {
        float2 suv = ScreenUV(i.uv);
        half4 sharp = tex2D(_MainTex, i.uv);
        half3 blurred = tex2D(_DofTex, suv).rgb;
        half coc = (half)abs(CircleOfConfusion(suv));
        half t = smoothstep(0.06, 0.45, coc);
        return half4(lerp(sharp.rgb, blurred, t), sharp.a);
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
            #pragma multi_compile_local __ BR_PHOTO
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDofPrefilter
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDofBlur
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDofCombine
            #pragma target 3.0
            ENDCG
        }
    }
    FallBack Off
}
