// Blossom Rally - cinematic post effects (driven by BlossomRally.PostFX, next to Hidden/BlossomRally/PostFX).
// Kept in its own shader on purpose: if this one is not supported, PostFX falls back to BR_PostFX's own
// passes and the game looks exactly as before. Low (lite grade) only ever uses pass 1, and only while the
// render scale is below 100%.
// Pass 0: composite+ - BR_PostFX's composite (motion blur, valley mist, bloom, lens dirt, sun shafts, flare,
//         lens drops, speed lines, pastel grade, vignette, photo mode) plus, each behind a uniform switch:
//         ambient occlusion, aerial perspective (sky-coloured distance haze), desert heat shimmer,
//         upscale sharpening, filmic highlight roll-off, film grain
// Pass 1: lite composite + sharpening (Low below 100% render scale)
// Pass 2: screen-space ambient occlusion from the depth texture (half res)
// Pass 3: depth-aware (bilateral) blur of the AO, one axis per pass
// Pass 4: bloom prefilter, 13-tap downsample with Karis average (no fireflies) + soft threshold
// Pass 5: bloom downsample, 13 taps
// Pass 6: bloom upsample, 9-tap tent blended with the sharper level (_Scatter)
Shader "Hidden/BlossomRally/PostFXPlus"
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
    float4 _Lift;
    float4 _Tint;
    float4 _ShadowTint;
    float _Vignette;
    float _BlurOffset;

    // photo mode
    float4 _FParams;
    float4 _FTint;
    float4 _FShadow;
    float4 _FLift;
    float _Letterbox;
    sampler2D _LogoTex;
    float4 _LogoRect;
    float _LogoAlpha;
    float _Overlay;

    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float _DepthOK;
    float4 _FrustumBL, _FrustumBR, _FrustumTL, _FrustumTR;
    float4 _HFog;
    float4 _HFogColor;
    float4 _HFogSun;
    float4 _HFogParams;
    float4 _HFogDrift;
    sampler2D _NoiseTex;
    float4 _MBlur;
    float4 _SpeedLines;
    float4 _SunScreen;
    float _ShaftOn;
    sampler2D _ShaftTex;
    float4 _ShaftColor;
    sampler2D _SunVisTex;
    float4 _Flare;
    float4 _LensFx;
    sampler2D _DirtTex;
    float _LensOn;
    sampler2D _LensTex;
    float4 _LensParams;

    // ---- cinematic additions
    sampler2D _AOTex;
    float _AOOn;           // 1 = the composite multiplies by _AOTex
    float4 _AOTint;        // rgb: what fully occluded areas keep (linear, a touch of cool ambient)
    float4 _AOParams;      // x radius (m), y intensity, z max distance (m), w sample count (<= 12)
    float4 _AOProj;        // xy: 1 / projection scale (view xy per ndc unit at eye depth 1), zw: projection scale * 0.5
    float4 _AOTexel;       // xy: texel size of the depth texture
    float4 _AODir;         // xy: blur step (uv of the AO target)
    float4 _Aerial;        // x sky recolour of the scene fog, y extra haze extinction (1/m), z extra haze cap, w fog density
    float4 _AerialFog;     // rgb: scene fog colour (linear)
    float4 _AerialHaze;    // rgb: air colour away from the sun (linear)
    float4 _AerialSun;     // rgb: forward-scattered sun (linear)
    float4 _AerialSunDir;  // xyz: direction towards the sun
    float4 _Heat;          // x amount, y time, z aspect
    float4 _Grain;         // x amount, y seed
    float _Sharpen;        // 0 = off
    float _Filmic;         // 0 = BR_PostFX's curve, 1 = filmic highlight roll-off
    float _Scatter;        // bloom upsample: share of the wider level
    sampler2D _BloomHigh;  // bloom upsample: the sharper level being blended into

    struct v2f_img2
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 suv : TEXCOORD1;
        float3 ray : TEXCOORD2;
    };

    // uv for textures other than _MainTex (same convention as BR_PostFX)
    float2 ScreenUV (float2 uv)
    {
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            uv.y = 1.0 - uv.y;
    #endif
        return uv;
    }

    v2f_img2 vert (appdata_img v)
    {
        v2f_img2 o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.suv = ScreenUV(v.texcoord);
        o.ray = lerp(lerp(_FrustumBL.xyz, _FrustumBR.xyz, o.suv.x), lerp(_FrustumTL.xyz, _FrustumTR.xyz, o.suv.x), o.suv.y);
        return o;
    }

    float Hash11 (float n)
    {
        return frac(sin(n * 12.9898) * 43758.5453);
    }

    // sine-free hash (stable on every GPU)
    float Hash12 (float2 p)
    {
        float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
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

    float tonemapACES1 (float x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    // Filmic highlight roll-off. Mid tones keep the per-channel curve above (today's look); highlights move
    // to a hue-preserving curve whose over-range colours roll towards white like film, instead of skewing
    // (an orange sunset sky stays orange rather than turning yellow, lamps bloom to a warm white).
    half3 tonemapFilmic (half3 x)
    {
        half3 ch = tonemapACES(x);
        if (_Filmic <= 0.0)
            return ch;
        float3 xf = max((float3)x, 0.0);
        float L = dot(xf, float3(0.2126, 0.7152, 0.0722));
        float Lt = tonemapACES1(L);
        float3 hp = xf * (Lt / max(L, 1e-4));
        float m = max(hp.r, max(hp.g, hp.b));
        float w = saturate((m - 1.0) / max(m, 1e-4));
        hp = lerp(hp / max(m, 1.0), float3(1.0, 1.0, 1.0), w);
        float t = smoothstep(0.35, 0.95, Lt) * _Filmic;
        return (half3)saturate(lerp((float3)ch, hp, t));
    }

    // unsharp mask against the 4 neighbours, limited so edges never ring
    half3 SharpenColor (half3 c, half3 nb)
    {
        half3 d = c - nb * 0.25;
        half lim = 0.04 + dot(c, half3(0.33, 0.33, 0.33)) * 0.2;
        return max(c + clamp(d * _Sharpen, -lim, lim), 0.0);
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

    // letterbox bars + watermark (perceptual space)
    half3 PhotoOverlay (half3 g, float2 suv)
    {
        g *= step(_Letterbox, suv.y) * step(suv.y, 1.0 - _Letterbox);
        float2 luv = (suv - _LogoRect.xy) / _LogoRect.zw;
        half4 lg = tex2Dlod(_LogoTex, float4(saturate(luv), 0, 0));
        half inside = step(0.0, luv.x) * step(luv.x, 1.0) * step(0.0, luv.y) * step(luv.y, 1.0);
        return lerp(saturate(g), lg.rgb, saturate(lg.a * inside * _LogoAlpha));
    }

    // ---------------------------------------------------------------- lens flare
    half FlareGhost (float2 p, float2 c, float r, float2 asp)
    {
        float d = length((p - c) * asp) / r;
        half disc = saturate(1.0 - d);
        half rim = saturate(1.0 - abs(d - 0.92) * 9.0);
        return disc * disc * 0.10 + rim * 0.07;
    }

    half3 Flare (float2 p, float2 sun, float aspect)
    {
        float2 asp = float2(aspect, 1.0);
        float2 axis = 0.5 - sun;
        half3 acc = FlareGhost(p, sun + axis * 0.55, 0.035, asp) * half3(1.0, 0.72, 0.84);
        acc += FlareGhost(p, sun + axis * 0.92, 0.065, asp) * half3(0.72, 0.88, 1.0) * 0.7;
        acc += FlareGhost(p, sun + axis * 1.30, 0.022, asp) * half3(1.0, 0.90, 0.68);
        acc += FlareGhost(p, sun + axis * 1.65, 0.095, asp) * half3(0.78, 1.0, 0.86) * 0.45;
        acc += FlareGhost(p, sun + axis * 2.05, 0.045, asp) * half3(1.0, 0.78, 1.0) * 0.6;
        float2 ds = (p - sun) * asp;
        float rs = length(ds);
        float ring = (rs - 0.30) * 14.0;
        acc += half3(1.0, 0.86, 0.82) * exp(-ring * ring) * 0.05;
        acc += half3(1.0, 0.88, 0.92) * exp(-abs(ds.y) * 260.0) * exp(-abs(ds.x) * 3.2) * 0.25;
        acc += exp(-rs * 10.0) * 0.18;
        return acc;
    }

    // ---------------------------------------------------------------- speed lines
    half SpeedLine (float2 p)
    {
        float2 d = (p - _MBlur.yz) * float2(_SpeedLines.z, 1.0);
        float r = length(d);
        float a = atan2(d.y, d.x) * 0.15915494 + 0.5;
        float seg = a * 120.0;
        float id = floor(seg);
        float h = Hash11(id + 3.7);
        float lane = abs(frac(seg) - 0.5) * 2.0;
        float on = step(0.74, h) * saturate(1.0 - lane / (0.16 + h * 0.18));
        float ph = frac(h * 17.31 + _SpeedLines.y * (1.1 + h * 1.7));
        float head = lerp(0.3, 1.15, ph);
        float len = 0.10 + h * 0.2;
        float body = saturate(1.0 - abs(r - head) / len);
        return (half)(on * body * smoothstep(0.32, 0.55, r));
    }

    // ---------------------------------------------------------------- pastel grade (shared by both composites)
    half3 PastelGrade (half3 c)
    {
        half3 g = LinearToGammaSpace(c);
        half lum = dot(g, half3(0.299, 0.587, 0.114));
        g = lerp(half3(lum, lum, lum), g, _Saturation);
        g = (g - 0.5) * _Contrast + 0.5;
        g *= _Tint.rgb;
        half shadowMask = saturate(1.0 - lum * 1.6);
        g = lerp(g, g * _ShadowTint.rgb, shadowMask * _ShadowTint.a);
        g = lerp(g, _Lift.rgb, _Lift.a * (1.0 - lum * 0.7));
        return g;
    }

    // ---------------------------------------------------------------- pass 1: lite composite + sharpening
    half4 fragLiteSharp (v2f_img2 i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        half3 c = tex2D(_MainTex, i.uv).rgb;
        half3 nb = tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb + tex2D(_MainTex, i.uv - float2(t.x, 0)).rgb
                 + tex2D(_MainTex, i.uv + float2(0, t.y)).rgb + tex2D(_MainTex, i.uv - float2(0, t.y)).rgb;
        c = SharpenColor(c, nb);
        c += tex2D(_BloomTex, i.suv).rgb * _BloomIntensity;
        c *= _Exposure;
        c = tonemapACES(c);
        half3 g = PastelGrade(c);
        float2 v = i.uv - 0.5;
        g *= saturate(1.0 - dot(v, v) * _Vignette);
        return half4(GammaToLinearSpace(saturate(g)), 1);
    }

    // ---------------------------------------------------------------- pass 0: composite+
    half4 fragCompositePlus (v2f_img2 i) : SV_Target
    {
        float2 suv = i.suv;
        float2 uv = i.uv;
        half4 lens = half4(0, 0, 0, 0);
        if (_LensOn > 0.5)
        {
            lens = tex2Dlod(_LensTex, float4(suv, 0, 0));
            suv += lens.rg * _LensParams.x;
            uv = ScreenUV(suv);
        }

        float eye = 1e5;
        float isSky = 1.0;
        if (_DepthOK > 0.5 && (_HFog.x > 0.0 || _MBlur.x > 0.0 || _Aerial.x > 0.0 || _Heat.x > 0.0))
        {
            float raw = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(suv, 0, 0));
            eye = LinearEyeDepth(raw);
            isSky = Linear01Depth(raw) > 0.9995 ? 1.0 : 0.0;
        }
        float rl = length(i.ray);
        float3 dir = i.ray / max(rl, 1e-5);

        // desert heat shimmer: far, near-horizontal rays wobble (hot ground and the sky just above it)
        if (_Heat.x > 0.0 && _DepthOK > 0.5)
        {
            float hk = _Heat.x * saturate((eye - 35.0) / 140.0) * saturate(1.0 - abs(dir.y) * 9.0);
            if (hk > 0.001)
            {
                float2 huv = float2(suv.x * _Heat.z * 1.8, suv.y * 6.5 - _Heat.y * 0.45);
                half2 hn = tex2Dlod(_NoiseTex, float4(huv, 0, 0)).rg - 0.5;
                suv += hn * float2(0.0007, 0.0024) * hk;
                uv = ScreenUV(suv);
            }
        }

        half3 c = tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb;

        // sharpening before the display scaler (render scale below 100%)
        if (_Sharpen > 0.0)
        {
            float2 t = _MainTex_TexelSize.xy;
            half3 nb = tex2Dlod(_MainTex, float4(uv + float2(t.x, 0), 0, 0)).rgb + tex2Dlod(_MainTex, float4(uv - float2(t.x, 0), 0, 0)).rgb
                     + tex2Dlod(_MainTex, float4(uv + float2(0, t.y), 0, 0)).rgb + tex2Dlod(_MainTex, float4(uv - float2(0, t.y), 0, 0)).rgb;
            c = SharpenColor(c, nb);
        }

        // ambient occlusion (bright lamps / emissive surfaces keep their glow)
        if (_AOOn > 0.5)
        {
            half ao = tex2Dlod(_AOTex, float4(suv, 0, 0)).r;
            ao = lerp(ao, 1.0, saturate(dot(c, half3(0.3, 0.59, 0.11)) - 1.0));
            c *= lerp(_AOTint.rgb, half3(1, 1, 1), ao);
        }

        // speed: zoom blur away from the direction of travel; the car and the road at its wheels stay sharp
        if (_MBlur.x > 0.0)
        {
            float2 d = suv - _MBlur.yz;
            float edge = saturate(length(d) * 2.4 - 0.22);
            float nearMask = _DepthOK > 0.5 ? saturate((eye - 7.0) / 16.0) : 1.0;
            float2 bv = d * (_MBlur.x * edge * nearMask);
            if (dot(bv, bv) > 1e-9)
            {
                int taps = (int)_MBlur.w;
                half3 acc = c;
                half n = 1.0;
                [loop]
                for (int k = 1; k <= 8; k++)
                {
                    if (k > taps) break;
                    float tk = k / _MBlur.w;
                    acc += tex2Dlod(_MainTex, float4(ScreenUV(suv - bv * tk), 0, 0)).rgb;
                    n += 1.0;
                }
                c = acc / n;
            }
        }

        // aerial perspective: the scene fog is one flat colour; give it the sky's direction (warm towards the
        // sun, cool away from it) and let distant forms fade into the air a little more (shan shui layering)
        if (_Aerial.x > 0.0 && _DepthOK > 0.5 && isSky < 0.5)
        {
            float dist = eye * rl;
            float keep = saturate((1500.0 - dist) / 300.0);     // the horizon rings keep their own painted haze
            float fd = _Aerial.w * eye;
            float fogAmt = 1.0 - exp(-fd * fd);
            half mie = pow(saturate(dot(dir, _AerialSunDir.xyz)), 8.0);
            half3 scatter = _AerialHaze.rgb + _AerialSun.rgb * mie;
            c = max(c + (scatter - _AerialFog.rgb) * (half)(fogAmt * _Aerial.x * keep), 0.0);
            half haze = (half)(min(1.0 - exp(-dist * _Aerial.y), _Aerial.z) * keep);
            c = lerp(c, scatter, haze);
        }

        // valley mist: exponential height fog integrated along the view ray, patchy, lit by the sun
        if (_HFog.x > 0.0 && _DepthOK > 0.5)
        {
            float dist = isSky > 0.5 ? _HFogParams.w : min(eye * rl, _HFogParams.w);
            float k = _HFog.z;
            float h0 = max(_WorldSpaceCameraPos.y - _HFog.y, -1.0);
            float h1 = max(h0 + dir.y * dist, -1.0);
            float dh = h1 - h0;
            float od = abs(dh) > 0.01 ? _HFog.x * dist * (exp(-k * h0) - exp(-k * h1)) / (k * dh)
                                      : _HFog.x * dist * exp(-k * h0);
            float2 wxz = _WorldSpaceCameraPos.xz + dir.xz * min(dist, 600.0);
            float2 nuv = wxz * 0.0042 + _HFogDrift.xy;
            half nz = tex2Dlod(_NoiseTex, float4(nuv, 0, 0)).r * 0.75 + tex2Dlod(_NoiseTex, float4(nuv * 3.1 - _HFogDrift.yx * 2.0, 0, 0)).g * 0.5;
            od *= lerp(0.45, 1.55, saturate(nz - 0.1));
            half fog = min(1.0 - exp(-od), _HFog.w);
            half sunAmt = pow(saturate(dot(dir, _HFogParams.xyz)), 6.0);
            half3 mist = _HFogColor.rgb + _HFogSun.rgb * sunAmt;
            c = lerp(c, mist, fog);
        }

        half3 bloom = tex2D(_BloomTex, i.suv).rgb;
        c += bloom * _BloomIntensity;
        if (_LensFx.x > 0.0)
            c += bloom * tex2Dlod(_DirtTex, float4(i.suv, 0, 0)).rgb * _LensFx.x;
        if (_ShaftOn > 0.5)
            c += tex2Dlod(_ShaftTex, float4(i.suv, 0, 0)).r * _ShaftColor.rgb;
        if (_Flare.x > 0.0)
        {
            half vis = tex2Dlod(_SunVisTex, float4(0.5, 0.5, 0, 0)).r;
            c += Flare(i.suv, _SunScreen.xy, _SunScreen.w) * _Flare.yzw * (_Flare.x * vis * vis);
        }
        if (_LensOn > 0.5)
        {
            c *= 1.0 + lens.a * 0.12;
            c = lerp(c, _LensParams.yzw, saturate(lens.b));
        }
        if (_SpeedLines.x > 0.0)
            c += SpeedLine(i.suv) * _SpeedLines.x;

        c *= _Exposure;
        c = tonemapFilmic(c);
        half3 g = PastelGrade(c);
    #ifdef BR_PHOTO
        g = PhotoFilter(g, i.suv);
    #endif
        float2 v = i.uv - 0.5;
        half vig = 1.0 - dot(v, v) * _Vignette;
        g *= saturate(vig);
        // film grain, strongest in the mid tones
        if (_Grain.x > 0.0)
        {
            half gn = (half)Hash12(floor(i.suv * abs(_MainTex_TexelSize.zw)) + _Grain.y) - 0.5;
            half gl = dot(g, half3(0.299, 0.587, 0.114));
            g += gn * _Grain.x * saturate(1.0 - abs(gl - 0.45) * 1.1);
        }
    #ifdef BR_PHOTO
        if (_Overlay > 0.5)
            g = PhotoOverlay(g, i.suv);
    #endif
        c = GammaToLinearSpace(saturate(g));
        return half4(c, 1);
    }

    // ---------------------------------------------------------------- pass 2 / 3: ambient occlusion
    float AOEye (float2 suv)
    {
        return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(suv, 0, 0)));
    }

    float3 AOViewPos (float2 suv, float eye)
    {
        return float3((suv * 2.0 - 1.0) * _AOProj.xy, 1.0) * eye;
    }

    half4 fragAO (v2f_img2 i) : SV_Target
    {
        float2 suv = i.suv;
        float eye = AOEye(suv);
        if (eye > _AOParams.z)
            return half4(1, 1, 1, 1);
        float3 P = AOViewPos(suv, eye);
        // normal from the neighbour with the smaller depth step on each axis (no halos at silhouettes)
        float2 tx = float2(_AOTexel.x, 0.0);
        float2 ty = float2(0.0, _AOTexel.y);
        float3 pr = AOViewPos(suv + tx, AOEye(suv + tx)) - P;
        float3 pl = P - AOViewPos(suv - tx, AOEye(suv - tx));
        float3 pu = AOViewPos(suv + ty, AOEye(suv + ty)) - P;
        float3 pd = P - AOViewPos(suv - ty, AOEye(suv - ty));
        float3 ddxP = abs(pr.z) < abs(pl.z) ? pr : pl;
        float3 ddyP = abs(pu.z) < abs(pd.z) ? pu : pd;
        float3 N = cross(ddyP, ddxP);
        N *= rsqrt(max(dot(N, N), 1e-12));
        if (dot(N, P) > 0.0)
            N = -N;

        float radius = _AOParams.x;
        float2 uvR = (radius / eye) * _AOProj.zw;          // sampling disc radius in uv
        float rmax = max(uvR.x, uvR.y);
        if (rmax < _AOTexel.y * 2.5)
            return half4(1, 1, 1, 1);                       // a couple of pixels wide: nothing to find
        uvR *= min(1.0, 0.1 / rmax);                         // cap the screen radius right in front of the camera
        float rot = Hash12(floor(suv / _AOTexel.xy)) * 6.2831853;
        float count = max(_AOParams.w, 1.0);
        float r2 = radius * radius;
        float occ = 0.0;
        [loop]
        for (int k = 0; k < 12; k++)
        {
            if ((float)k >= count) break;
            float fk = ((float)k + 0.5) / count;
            float ang = rot + (float)k * 2.3999632;           // golden-angle spiral
            float2 s = suv + float2(cos(ang), sin(ang)) * (sqrt(fk) * uvR);
            float3 S = AOViewPos(s, AOEye(s));
            float3 dv = S - P;
            float vv = dot(dv, dv);
            float vn = dot(dv, N) * rsqrt(vv + 1e-4);
            occ += saturate(vn - 0.12) * saturate(1.0 - vv / r2);
        }
        float ao = saturate(1.0 - occ / count * _AOParams.y);
        ao = lerp(ao, 1.0, saturate((eye - _AOParams.z * 0.6) / (_AOParams.z * 0.4)));
        return half4(ao, ao, ao, 1);
    }

    half4 fragAOBlur (v2f_img2 i) : SV_Target
    {
        float2 uv = i.uv;                                   // AO target -> AO target: never flipped
        float e0 = AOEye(uv);
        float tol = e0 * 0.06 + 0.05;
        half acc = tex2D(_MainTex, uv).r * 0.3;
        half ws = 0.3;
        [unroll]
        for (int k = 1; k <= 2; k++)
        {
            float w0 = k == 1 ? 0.24 : 0.11;
            float2 o = _AODir.xy * (float)k;
            float wa = w0 * saturate(1.0 - abs(AOEye(uv + o) - e0) / tol);
            float wb = w0 * saturate(1.0 - abs(AOEye(uv - o) - e0) / tol);
            acc += tex2D(_MainTex, uv + o).r * (half)wa + tex2D(_MainTex, uv - o).r * (half)wb;
            ws += (half)(wa + wb);
        }
        half r = acc / ws;
        return half4(r, r, r, 1);
    }

    // ---------------------------------------------------------------- pass 4 / 5 / 6: HQ bloom
    half3 Down13 (float2 uv, float2 t, bool karis)
    {
        half3 A = tex2D(_MainTex, uv + t * float2(-1, -1)).rgb;
        half3 B = tex2D(_MainTex, uv + t * float2( 1, -1)).rgb;
        half3 C = tex2D(_MainTex, uv + t * float2(-1,  1)).rgb;
        half3 D = tex2D(_MainTex, uv + t * float2( 1,  1)).rgb;
        half3 E = tex2D(_MainTex, uv + t * float2(-2, -2)).rgb;
        half3 F = tex2D(_MainTex, uv + t * float2( 0, -2)).rgb;
        half3 G = tex2D(_MainTex, uv + t * float2( 2, -2)).rgb;
        half3 H = tex2D(_MainTex, uv + t * float2(-2,  0)).rgb;
        half3 I = tex2D(_MainTex, uv).rgb;
        half3 J = tex2D(_MainTex, uv + t * float2( 2,  0)).rgb;
        half3 K = tex2D(_MainTex, uv + t * float2(-2,  2)).rgb;
        half3 L = tex2D(_MainTex, uv + t * float2( 0,  2)).rgb;
        half3 M = tex2D(_MainTex, uv + t * float2( 2,  2)).rgb;
        half3 g0 = (A + B + C + D) * 0.25;
        half3 g1 = (E + F + H + I) * 0.25;
        half3 g2 = (F + G + I + J) * 0.25;
        half3 g3 = (H + I + K + L) * 0.25;
        half3 g4 = (I + J + L + M) * 0.25;
        if (karis)
        {
            // Karis average: each block weighted by 1 / (1 + luma) so single hot pixels cannot flicker
            half3 lw = half3(0.2126, 0.7152, 0.0722);
            half w0 = 0.5 / (1.0 + dot(g0, lw));
            half w1 = 0.125 / (1.0 + dot(g1, lw));
            half w2 = 0.125 / (1.0 + dot(g2, lw));
            half w3 = 0.125 / (1.0 + dot(g3, lw));
            half w4 = 0.125 / (1.0 + dot(g4, lw));
            return (g0 * w0 + g1 * w1 + g2 * w2 + g3 * w3 + g4 * w4) / (w0 + w1 + w2 + w3 + w4);
        }
        return g0 * 0.5 + (g1 + g2 + g3 + g4) * 0.125;
    }

    half4 fragPrefilterHQ (v2f_img2 i) : SV_Target
    {
        half3 c = min(Down13(i.uv, _MainTex_TexelSize.xy, true), 64.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 0.00001);
        half contrib = max(soft, br - _Threshold) / max(br, 0.00001);
        return half4(c * contrib, 1);
    }

    half4 fragDownHQ (v2f_img2 i) : SV_Target
    {
        return half4(Down13(i.uv, _MainTex_TexelSize.xy, false), 1);
    }

    half4 fragUpHQ (v2f_img2 i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy * _BlurOffset;     // _MainTex = the smaller, wider level
        half3 corners = tex2D(_MainTex, i.uv + t * float2(-1, -1)).rgb + tex2D(_MainTex, i.uv + t * float2(1, -1)).rgb
                      + tex2D(_MainTex, i.uv + t * float2(-1, 1)).rgb + tex2D(_MainTex, i.uv + t * float2(1, 1)).rgb;
        half3 edges = tex2D(_MainTex, i.uv + t * float2(0, -1)).rgb + tex2D(_MainTex, i.uv + t * float2(-1, 0)).rgb
                    + tex2D(_MainTex, i.uv + t * float2(1, 0)).rgb + tex2D(_MainTex, i.uv + t * float2(0, 1)).rgb;
        half3 up = (corners + edges * 2.0 + tex2D(_MainTex, i.uv).rgb * 4.0) * (1.0 / 16.0);
        half3 high = tex2D(_BloomHigh, i.uv).rgb;
        return half4(lerp(high, up, _Scatter), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCompositePlus
            #pragma target 3.0
            #pragma multi_compile_local __ BR_PHOTO
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragLiteSharp
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAO
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAOBlur
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilterHQ
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDownHQ
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUpHQ
            #pragma target 3.0
            ENDCG
        }
    }
    FallBack Off
}
