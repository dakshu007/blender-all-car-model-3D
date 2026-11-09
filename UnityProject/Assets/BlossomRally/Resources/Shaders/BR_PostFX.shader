// Blossom Rally - camera post effect for the built-in pipeline (driven by BlossomRally.PostFX).
// Pass 0: bloom prefilter (soft threshold) + downsample
// Pass 1: 4-tap box blur (down/up sample)
// Pass 2: final composite - (motion blur, valley mist,) bloom, (lens dirt, sun shafts, flare, lens drops,
//         speed lines,) filmic tonemap, dreamy pastel grade, vignette
//         (+ photo mode filter / letterbox / watermark when BR_PHOTO is enabled)
// Pass 3: depth of field - circle of confusion from the depth texture + half-res downsample
// Pass 4: depth of field - gather (disc) blur weighted by each sample's circle of confusion
// Pass 5: depth of field - combine sharp and blurred image by the full-res circle of confusion
// Pass 6: sun shafts - occlusion mask (sky by depth, bright, near the sun), low res
// Pass 7: sun shafts - radial blur towards the sun
// Pass 8: ink & watercolour painting
// Pass 9: sun visibility (1x1, for the flare)
// Pass 10: lens drops / mud specks (immediate-mode quads into the lens texture)
// Everything new is behind uniform switches that are 0 when unused, so the old look is unchanged.
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
    float _Overlay;        // 1 = composite draws letterbox / logo (0 when the ink pass draws them)

    // depth of field
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    sampler2D _DofTex;
    float4 _DofParams;     // x focus distance (m), y strength, z max blur radius (texels of the pass source)

    // graphics upgrade
    float _DepthOK;        // 1 = _CameraDepthTexture is valid this frame
    float4 _FrustumBL, _FrustumBR, _FrustumTL, _FrustumTR;   // world view rays for eye depth 1
    float4 _HFog;          // x extinction at the mist base (1/m), y base height, z falloff (1/m), w max fog
    float4 _HFogColor;
    float4 _HFogSun;       // forward-scattered sun colour
    float4 _HFogParams;    // xyz sun direction, w max distance
    float4 _HFogDrift;
    sampler2D _NoiseTex;
    float4 _MBlur;         // x amount (uv), yz focus (screen uv), w taps
    float4 _SpeedLines;    // x amount, y time, z aspect
    float4 _SunScreen;     // xy sun (screen uv), z radial falloff radius, w aspect
    float _ShaftThreshold;
    float _ShaftRadius;
    float _ShaftOn;
    sampler2D _ShaftTex;
    float4 _ShaftColor;
    sampler2D _SunVisTex;
    float4 _Flare;         // x intensity, yzw sun colour (linear)
    float4 _LensFx;        // x lens dirt intensity
    sampler2D _DirtTex;
    float _LensOn;
    sampler2D _LensTex;    // rg refraction (signed), b mud, a coverage
    float4 _LensParams;    // x refraction strength, yzw mud colour (linear)
    sampler2D _BleedTex;
    sampler2D _PaperTex;
    float4 _InkParams;     // x pixel scale, y aspect, z boil step, w night
    sampler2D _DropTex;

    struct v2f_img2
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 suv : TEXCOORD1;
        float3 ray : TEXCOORD2;
    };

    // uv for textures other than _MainTex (depth, bloom / blur targets, screen-anchored overlays):
    // on D3D-like platforms with anti-aliasing the source comes in flipped relative to them.
    // (The flip is its own inverse, so it also maps screen uv back to _MainTex uv.)
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

    half3 boxSample (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    float Hash11 (float n)
    {
        return frac(sin(n * 12.9898) * 43758.5453);
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
        acc += half3(1.0, 0.86, 0.82) * exp(-ring * ring) * 0.05;                       // soft halo
        acc += half3(1.0, 0.88, 0.92) * exp(-abs(ds.y) * 260.0) * exp(-abs(ds.x) * 3.2) * 0.25;   // streak
        acc += exp(-rs * 10.0) * 0.18;                                                  // glow
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

    // ---------------------------------------------------------------- composite
    half4 fragComposite (v2f_img2 i) : SV_Target
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
        half3 c = tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb;

        float eye = 1e5;
        float isSky = 1.0;
        if (_DepthOK > 0.5 && (_HFog.x > 0.0 || _MBlur.x > 0.0))
        {
            float raw = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(suv, 0, 0));
            eye = LinearEyeDepth(raw);
            isSky = Linear01Depth(raw) > 0.9995 ? 1.0 : 0.0;
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
                    float t = k / _MBlur.w;
                    acc += tex2Dlod(_MainTex, float4(ScreenUV(suv - bv * t), 0, 0)).rgb;
                    n += 1.0;
                }
                c = acc / n;
            }
        }

        // valley mist: exponential height fog integrated along the view ray, patchy, lit by the sun
        if (_HFog.x > 0.0 && _DepthOK > 0.5)
        {
            float rl = length(i.ray);
            float3 dir = i.ray / max(rl, 1e-5);
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
            c += bloom * tex2Dlod(_DirtTex, float4(i.suv, 0, 0)).rgb * _LensFx.x;     // lens dirt lit by bright lights
        if (_ShaftOn > 0.5)
            c += tex2Dlod(_ShaftTex, float4(i.suv, 0, 0)).r * _ShaftColor.rgb;
        if (_Flare.x > 0.0)
        {
            half vis = tex2Dlod(_SunVisTex, float4(0.5, 0.5, 0, 0)).r;
            c += Flare(i.suv, _SunScreen.xy, _SunScreen.w) * _Flare.yzw * (_Flare.x * vis * vis);
        }
        if (_LensOn > 0.5)
        {
            c *= 1.0 + lens.a * 0.12;                              // drops catch the light
            c = lerp(c, _LensParams.yzw, saturate(lens.b));        // mud specks
        }
        if (_SpeedLines.x > 0.0)
            c += SpeedLine(i.suv) * _SpeedLines.x;

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
        g = PhotoFilter(g, i.suv);
    #endif
        float2 v = i.uv - 0.5;
        half vig = 1.0 - dot(v, v) * _Vignette;
        g *= saturate(vig);
    #ifdef BR_PHOTO
        if (_Overlay > 0.5)
            g = PhotoOverlay(g, i.suv);
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

    // ---------------------------------------------------------------- sun shafts
    // r = shaft source (bright sky near the sun), g = raw sky visibility (for the flare)
    half4 fragShaftMask (v2f_img2 i) : SV_Target
    {
        float2 suv = ScreenUV(i.uv);
        half3 c = boxSample(i.uv, 1.0);
        half lum = max(c.r, max(c.g, c.b));
        half sky;
        if (_DepthOK > 0.5)
        {
            float raw = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(suv, 0, 0));
            sky = Linear01Depth(raw) > 0.9995 ? 1.0 : 0.0;
        }
        else
            sky = smoothstep(_ShaftThreshold, _ShaftThreshold * 2.0, lum);
        half bright = smoothstep(_ShaftThreshold * 0.6, _ShaftThreshold * 2.5, lum);
        float2 d = (suv - _SunScreen.xy) * float2(_SunScreen.w, 1.0);
        half radial = saturate(1.0 - length(d) / _SunScreen.z);
        radial *= radial;
        return half4(sky * bright * radial, sky, 0, 1);
    }

    half4 fragShaftBlur (v2f_img2 i) : SV_Target
    {
        float2 uv = i.uv;
        float2 stepUV = (_SunScreen.xy - uv) * (_ShaftRadius / 8.0);
        half acc = 0.0;
        half wsum = 0.0;
        half w = 1.0;
        [unroll]
        for (int k = 0; k < 8; k++)
        {
            acc += tex2D(_MainTex, uv + stepUV * k).r * w;
            wsum += w;
            w *= 0.9;
        }
        return half4(acc / wsum, 0, 0, 1);
    }

    half4 fragSunVis (v2f_img2 i) : SV_Target
    {
        float2 s = _SunScreen.xy;
        float2 r = float2(0.012 / _SunScreen.w, 0.012);
        half v = tex2D(_MainTex, s).g * 2.0;
        v += tex2D(_MainTex, s + float2( r.x, 0)).g + tex2D(_MainTex, s + float2(-r.x, 0)).g;
        v += tex2D(_MainTex, s + float2(0,  r.y)).g + tex2D(_MainTex, s + float2(0, -r.y)).g;
        v += tex2D(_MainTex, s + r * 0.7).g + tex2D(_MainTex, s - r * 0.7).g;
        v += tex2D(_MainTex, s + float2(r.x, -r.y) * 0.7).g + tex2D(_MainTex, s + float2(-r.x, r.y) * 0.7).g;
        // fully off screen: nothing to see
        half inside = step(0.0, s.x) * step(s.x, 1.0) * step(0.0, s.y) * step(s.y, 1.0);
        return half4(v / 10.0 * inside, 0, 0, 1);
    }

    // ---------------------------------------------------------------- ink & watercolour
    half Luma (half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

    half4 fragInk (v2f_img2 i) : SV_Target
    {
        float px = _InkParams.x;
        float aspect = _InkParams.y;
        float boil = _InkParams.z;
        float night = _InkParams.w;
        float2 suv = i.suv;
        // hand-painted wobble that "boils" a few times a second
        float2 nuv = suv * float2(aspect, 1.0) * 1.6 + boil * float2(0.371, 0.529);
        float2 wob = (tex2D(_NoiseTex, nuv).rg - 0.5) * 0.0042;
        float2 ws = suv + wob;
        float2 t = abs(_MainTex_TexelSize.xy) * px * 1.3;

        half3 c = tex2D(_MainTex, ScreenUV(ws)).rgb;
        half3 cl = tex2D(_MainTex, ScreenUV(ws + float2(-t.x, 0))).rgb;
        half3 cr = tex2D(_MainTex, ScreenUV(ws + float2( t.x, 0))).rgb;
        half3 cu = tex2D(_MainTex, ScreenUV(ws + float2(0,  t.y))).rgb;
        half3 cd = tex2D(_MainTex, ScreenUV(ws + float2(0, -t.y))).rgb;
        half el = abs(sqrt(Luma(cl)) - sqrt(Luma(cr))) + abs(sqrt(Luma(cu)) - sqrt(Luma(cd)));
        half edgeC = smoothstep(0.10, 0.32, el);

        half edgeD = 0.0;
        half far = 0.0;
        half sky = 0.0;
        if (_DepthOK > 0.5)
        {
            float rc = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(ws, 0, 0));
            float dc = LinearEyeDepth(rc);
            float il = 1.0 / LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(ws + float2(-t.x, 0), 0, 0)));
            float ir = 1.0 / LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(ws + float2( t.x, 0), 0, 0)));
            float iu = 1.0 / LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(ws + float2(0,  t.y), 0, 0)));
            float id = 1.0 / LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(ws + float2(0, -t.y), 0, 0)));
            float ic = 1.0 / dc;
            // second derivative of inverse depth: zero on flat planes, strong at silhouettes and creases
            float lap = (abs(il + ir - 2.0 * ic) + abs(iu + id - 2.0 * ic)) / max(ic, 1e-6);
            edgeD = smoothstep(0.035, 0.16, lap);
            sky = Linear01Depth(rc) > 0.9995 ? 1.0 : 0.0;
            far = saturate((dc - 45.0) / 420.0);
            far = max(far, sky);
        }

        half3 g = LinearToGammaSpace(saturate(c));
        half3 b = LinearToGammaSpace(saturate(tex2D(_BleedTex, ws + wob * 1.5).rgb));
        // colour bleeds past the lines; pigment pools where sharp and bled colour disagree
        half3 wash = lerp(g, b, 0.5);
        half pool = saturate(length(g - b) * 2.0);
        half lum = Luma(wash);
        wash = lerp(half3(lum, lum, lum), wash, 0.62);
        // airy watercolour values, distant forms fade into the paper (shan shui perspective)
        wash = 1.0 - (1.0 - wash) * 0.82;
        half3 paperTint = lerp(half3(0.96, 0.93, 0.85), half3(0.50, 0.54, 0.68), night * 0.75);
        wash = lerp(wash, paperTint, far * 0.55 + sky * 0.25);

        half4 P = tex2D(_PaperTex, suv * float2(aspect, 1.0) * 2.2);
        half3 paper = paperTint * lerp(0.93, 1.03, P.r) * lerp(0.97, 1.02, P.g);
        half3 res = paper * saturate(wash * 1.07);
        res *= lerp(1.0, lerp(0.88, 1.05, P.b), (1.0 - lum) * 0.6);       // pigment granulation
        res *= 1.0 - pool * 0.16;

        // ink line work, broken like a dry brush and lighter in the distance
        half brush = tex2D(_NoiseTex, suv * float2(aspect, 1.0) * 6.0 + boil * 0.13).b;
        half edge = max(edgeD, edgeC * 0.85);
        edge *= smoothstep(0.18, 0.55, brush + edge * 0.35);
        edge *= 1.0 - far * 0.55;
        half3 inkCol = lerp(half3(0.10, 0.09, 0.11), half3(0.05, 0.06, 0.12), night);
        res = lerp(res, inkCol, saturate(edge) * 0.88);

        // aged paper towards the edges
        float2 v = i.uv - 0.5;
        res *= 1.0 - dot(v, v) * 0.38;
    #ifdef BR_PHOTO
        res = PhotoOverlay(res, i.suv);
    #endif
        return half4(GammaToLinearSpace(saturate(res)), 1);
    }

    // ---------------------------------------------------------------- lens drops
    struct appdata_drop
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float3 data : TEXCOORD1;      // x mud, y opacity, z random seed
    };

    struct v2f_drop
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 data : TEXCOORD1;
    };

    v2f_drop vertDrop (appdata_drop v)
    {
        v2f_drop o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.data = v.data;
        return o;
    }

    half4 fragDrop (v2f_drop i) : SV_Target
    {
        half4 d = tex2D(_DropTex, i.uv);
        half2 n = d.rg * 2.0 - 1.0;
        half a = d.a * i.data.y;
        half mud = 0.0;
        if (i.data.x > 0.5)
        {
            // irregular splat with a few satellites
            half nz = tex2Dlod(_NoiseTex, float4(i.uv * 0.4 + i.data.z, 0, 0)).b;
            a = saturate((d.b * 1.7 + nz - 1.0) * 3.5) * i.data.y * 0.9;
            n *= 0.25;
            mud = 1.0;
        }
        return half4(n, mud, a);
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
            #pragma target 3.0
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
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaftMask
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaftBlur
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragInk
            #pragma target 3.0
            #pragma multi_compile_local __ BR_PHOTO
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragSunVis
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertDrop
            #pragma fragment fragDrop
            #pragma target 3.0
            ENDCG
        }
    }
    FallBack Off
}
