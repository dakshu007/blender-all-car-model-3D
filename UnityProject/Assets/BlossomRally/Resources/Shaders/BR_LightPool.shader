// Blossom Rally - warm lantern light pools on the ground (NightGlow). Additive, no real lights.
// uv0 = offset from the pool centre (m), uv1 = (ground centre xyz, lantern height), uv2 = (radius,
// intensity, road / wet weight, flicker phase), colour = warm tint. On wet ground each pool also draws
// the lantern's mirror reflection as a soft streak running towards the camera.
Shader "BlossomRally/LightPool"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _WetStreaks ("Wet Streaks", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Offset -2, -2

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            float _WetStreaks;
            float _BR_Night;
            float _BR_Wetness;
            float4 _BR_LightPoolColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 local : TEXCOORD0;
                float4 centre : TEXCOORD1;
                float4 data : TEXCOORD2;
                float3 wp : TEXCOORD3;
                half3 col : TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.local = v.uv;
                o.centre = v.uv1;
                o.data = v.uv2;
                half3 c = v.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
            #endif
                o.col = c;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = i.data.x;
                float inten = i.data.y;
                float ph = i.data.w;
                float d = length(i.local) / r;
                half pool = saturate(1.0 - d * d);
                pool *= pool;
                half n = tex2D(_NoiseTex, i.wp.xz * 0.21).g;
                pool *= lerp(0.8, 1.15, n);
                half flick = 1.0 + sin(_Time.y * (7.0 + ph) + ph * 10.0) * 0.04 + sin(_Time.y * 13.1 + ph) * 0.03;
                half3 col = i.col * pool * inten * flick * 0.55;

                // wet ground: mirror image of the lantern smeared into a streak towards the camera
                float wet = saturate(_BR_Wetness) * _WetStreaks * i.data.z;
                if (wet > 0.01)
                {
                    float3 cam = _WorldSpaceCameraPos;
                    float H = i.centre.w;
                    float gy = i.centre.y;
                    float camH = max(cam.y - gy, 0.2);
                    float t = camH / (camH + H);
                    float2 P = lerp(cam.xz, i.centre.xz, t);
                    float2 toCam = cam.xz - P;
                    float lc = length(toCam);
                    toCam = lc > 1e-3 ? toCam / lc : float2(0, 1);
                    float2 q = i.wp.xz - P;
                    float along = dot(q, toCam);
                    float across = dot(q, float2(-toCam.y, toCam.x));
                    float w = 0.22 + max(along, 0.0) * 0.05;
                    half streak = exp(-across * across / (w * w)) * (along > 0.0 ? exp(-along / (1.8 + H * 0.5)) : exp(-along * along * 3.0));
                    streak *= lerp(0.7, 1.2, n) * saturate(1.0 - length(i.local) / 8.0) * 4.0;
                    col += i.col * streak * inten * wet * flick;
                    col += i.col * pool * inten * wet * 0.35;     // wet ground reflects more of the pool
                }
                col *= smoothstep(0.2, 0.75, _BR_Night);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
