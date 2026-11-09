// Blossom Rally - stylised animated water for the lake and the river.
// SubShader LOD 300 (graphics upgrade): sky / reflection-probe reflections with fresnel, a planar
// reflection of the pagoda and trees on the lake (High / Ultra, _ReflectionTex from WaterFX), depth-tinted
// body colour and soft shoreline foam (when the camera depth texture is on, _BR_HasDepth), rain rings and
// ripples / a bow wake around the player's car in shallow water (BR_Interact globals), sun glints, fog.
// SubShader LOD 200: the original surface-shader water (used when GraphicsFX.Enhanced is off).
Shader "BlossomRally/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.55, 0.82, 0.88, 0.75)
        _Deep ("Deep", Color) = (0.20, 0.50, 0.68, 0.92)
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 0.6
        _FoamColor ("Foam", Color) = (0.97, 0.96, 0.93, 1)
        _ReflectionTex ("Planar Reflection", 2D) = "black" {}
        _PlanarAmount ("Planar Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "BR_Interact.cginc"

            fixed4 _Shallow;
            fixed4 _Deep;
            float _RippleScale;
            float _RippleSpeed;
            fixed4 _FoamColor;
            sampler2D _ReflectionTex;
            float _PlanarAmount;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _BR_HasDepth;     // 1 while the main camera renders with a valid depth texture
            float _BR_Rain;
            float4 _BR_WaterFx;     // x foam / ripples on, y water level, z detail ripples
            sampler2D _BR_Noise;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                float4 sp : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp0 = mul(unity_ObjectToWorld, v.vertex).xyz;
                v.vertex.y += sin(wp0.x * 0.35 + _Time.y * 1.2) * 0.03 + sin(wp0.z * 0.42 + _Time.y * 0.9) * 0.03;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.sp = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.sp.z);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float ripple (float2 p, float t)
            {
                return sin(p.x * 1.7 + t) * 0.5 + sin(p.y * 2.3 - t * 1.3) * 0.35 + sin((p.x + p.y) * 3.1 + t * 2.1) * 0.15;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 wp = i.wp;
                float2 p = wp.xz * _RippleScale;
                float t = _Time.y * _RippleSpeed * 4.0;
                float e = 0.08;
                float h = ripple(p, t);
                float hx = ripple(p + float2(e, 0), t);
                float hz = ripple(p + float2(0, e), t);
                float2 slope = float2(h - hx, h - hz) * 2.5;
                float fx = _BR_WaterFx.x;

                // fine wind ripples (textures sampled outside flow control: they need derivatives)
                float2 nuv = wp.xz * 0.07;
                half2 na = tex2D(_BR_Noise, nuv + _Time.y * float2(0.011, 0.007)).gb;
                half2 nb = tex2D(_BR_Noise, nuv * 1.9 - _Time.y * float2(0.006, 0.013)).bg;
                half fn = tex2D(_BR_Noise, wp.xz * 0.16 + _Time.y * float2(0.012, -0.008)).b;
                slope += (na + nb - 1.0) * 0.35 * _BR_WaterFx.z;

                // rain rings
                if (_BR_Rain > 0.01)
                {
                    float2 cellP = wp.xz / 0.8;
                    float2 cell = floor(cellP);
                    float hr = hash21(cell);
                    float2 centre = cell + 0.25 + 0.5 * float2(hash21(cell + 3.1), hash21(cell + 9.7));
                    float rt = frac(_Time.y * (0.9 + hr * 0.7) + hr);
                    float2 dv = cellP - centre;
                    float r = length(dv);
                    float ring = sin(saturate(1.0 - abs(r - rt * 0.5) * 12.0) * 3.14159) * (1.0 - rt) * _BR_Rain;
                    slope += dv / max(r, 1e-3) * ring * 0.6;
                }

                // ripples and wake from the player's car in shallow water / along the banks
                half wake = 0.0;
                if (fx > 0.0)
                {
                    float2 toCar = wp.xz - _BR_CarA.xz;
                    if (dot(toCar, toCar) < 1600.0)
                    {
                        float waterY = _BR_WaterFx.y;
                        [unroll]
                        for (int k = 0; k < 6; k++)
                        {
                            float4 tr = _BR_Trail[k];
                            float age = _Time.y - tr.w;
                            float inWater = saturate(1.0 - (tr.y - waterY - 0.35) / 0.9) * step(0.0, age);
                            float spd = _BR_TrailSpeed[k];
                            float2 dv = wp.xz - tr.xz;
                            float d = length(dv);
                            float R = 0.7 + age * (1.5 + spd * 0.07);
                            float x = d - R;
                            float env = exp(-x * x * 3.0) * exp(-age * 1.3) * inWater * saturate(spd * 0.3 + 0.12);
                            slope += dv / max(d, 1e-3) * cos(x * 8.0) * env * 0.5;
                            wake = max(wake, env * saturate(sin(x * 8.0) * 0.5 + 0.5));
                        }
                        float tseg;
                        float2 c = BR_SegClosest(wp.xz, _BR_CarA.xz, _BR_CarB.xz, tseg);
                        float dc = length(wp.xz - c);
                        float inW = saturate(1.0 - (lerp(_BR_CarA.y, _BR_CarB.y, tseg) - waterY - 0.35) / 0.9);
                        float bow = saturate(1.0 - dc / (1.4 + _BR_CarB.w * 0.05)) * inW * saturate(_BR_CarB.w * 0.15 + 0.1);
                        wake = max(wake, bow);
                    }
                }

                float3 n = normalize(float3(slope.x, 1.0, slope.y));
                float3 V = normalize(_WorldSpaceCameraPos - wp);
                float ndv = saturate(dot(n, V));
                float fres = lerp(0.10, 1.0, pow(1.0 - ndv, 4.0));

                // reflections: sky / reflection probe, planar on the lake
                float3 rd = reflect(-V, n);
                half4 probe = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, rd, 1.5);
                half3 refl = DecodeHDR(probe, unity_SpecCube0_HDR);
                if (_PlanarAmount > 0.001)
                {
                    float2 ruv = i.sp.xy / i.sp.w + n.xz * 0.035;
                    half3 pl = tex2Dlod(_ReflectionTex, float4(ruv, 0, 0)).rgb;
                    refl = lerp(refl, pl, _PlanarAmount);
                }

                // water column (depth texture) or the original stylised blend
                float wd = 3.0;
                half depthT = saturate(0.35 + 0.25 * h + fres * 0.4);
                half hasD = _BR_HasDepth;
                if (hasD > 0.5)
                {
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.sp)));
                    float thick = max(0.0, sceneZ - i.sp.z);
                    wd = thick * max(V.y, 0.2);
                    depthT = saturate(1.0 - exp(-wd * 0.8) + fres * 0.15);
                }

                half3 amb = ShadeSH9(half4(0, 1, 0, 1));
                half3 lit = amb + _LightColor0.rgb * saturate(_WorldSpaceLightPos0.y) * 0.6;
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, depthT) * lit;
                float3 H = normalize(_WorldSpaceLightPos0.xyz + V);
                float nh = saturate(dot(n, H));
                half3 spec = _LightColor0.rgb * (pow(nh, 380.0) * 3.0 + pow(nh, 48.0) * 0.12);
                half3 col = lerp(body, refl, fres * 0.85) + spec;

                // soft shoreline foam where the water meets the bank
                half foam = 0.0;
                if (fx > 0.0)
                {
                    half shore = hasD > 0.5 ? 1.0 - saturate(wd / 0.6) : 0.0;
                    half band = sin(wd * 10.0 - _Time.y * 1.4 + fn * 4.0) * 0.5 + 0.5;
                    half shoreFoam = smoothstep(0.35, 0.8, shore + fn * 0.35 - 0.12) + shore * band * 0.35;
                    foam = saturate(shoreFoam * 0.9 + wake * 0.75) * fx;
                    col = lerp(col, _FoamColor.rgb * lit, foam * 0.85);
                }

                half alpha = saturate(lerp(_Shallow.a, _Deep.a, depthT) + fres * 0.2 + foam * 0.4);
                if (hasD > 0.5) alpha *= saturate(wd / 0.1);     // soft contact with the bank
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    // ---------------------------------------------------------------- original water (upgrade off)
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        fixed4 _Shallow;
        fixed4 _Deep;
        float _RippleScale;
        float _RippleSpeed;

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            v.vertex.y += sin(wp.x * 0.35 + _Time.y * 1.2) * 0.03 + sin(wp.z * 0.42 + _Time.y * 0.9) * 0.03;
        }

        float ripple(float2 p, float t)
        {
            return sin(p.x * 1.7 + t) * 0.5 + sin(p.y * 2.3 - t * 1.3) * 0.35 + sin((p.x + p.y) * 3.1 + t * 2.1) * 0.15;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _RippleScale;
            float t = _Time.y * _RippleSpeed * 4.0;
            float e = 0.08;
            float h = ripple(p, t);
            float hx = ripple(p + float2(e, 0), t);
            float hz = ripple(p + float2(0, e), t);
            o.Normal = normalize(float3((h - hx) * 2.5, (h - hz) * 2.5, 1.0));
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 3.0);
            fixed4 c = lerp(_Shallow, _Deep, saturate(0.35 + 0.5 * h * 0.5 + fres * 0.4));
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = 0.93;
            o.Alpha = saturate(c.a + fres * 0.2);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
