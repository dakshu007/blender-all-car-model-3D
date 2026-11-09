// Blossom Rally - dappled sunlight under the blossom trees (DappledLight decals). 2x multiply blend:
// 0.5 = no change, above brightens (sun flecks through the canopy gaps), below darkens (leaf mottle).
// uv0 = ellipse coords (-1..1), uv1.x = per-tree random. Fades with distance, night, cloud and rain
// (_BR_DappleStrength) and sways gently with the wind gusts.
Shader "BlossomRally/Dapple"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _FadeDist ("Fade Distance", Float) = 90
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-120" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend DstColor SrcColor
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
            float _FadeDist;
            float _BR_DappleStrength;
            float4 _BR_WindGust;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float2 data : TEXCOORD2; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                float dist = distance(o.wp, _WorldSpaceCameraPos);
                o.data = float2(v.uv1.x, 1.0 - saturate((dist - _FadeDist * 0.6) / (_FadeDist * 0.4)));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv);
                half mask = 1.0 - smoothstep(0.55, 1.0, d);
                float seed = i.data.x;
                float2 sway = float2(sin(_Time.y * 1.1 + seed * 6.3), cos(_Time.y * 0.83 + seed * 4.1)) * 0.12 * (1.0 + _BR_WindGust.x);
                float2 p = i.wp.xz + sway;
                half n1 = tex2D(_NoiseTex, p * 0.13 + seed).a;
                half n2 = tex2D(_NoiseTex, p * 0.31 - seed * 0.7).a;
                half n = n1 * 0.6 + n2 * 0.4;
                half fleck = smoothstep(0.58, 0.82, n);
                half mottle = 1.0 - smoothstep(0.18, 0.45, n);
                half k = mask * i.data.y * _BR_DappleStrength;
                half delta = (fleck * 0.30 - mottle * 0.07) * k;
                fixed4 col = fixed4(0.5 + delta * half3(1.0, 0.95, 0.84), 1);   // warm sun flecks
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0.5, 0.5, 0.5, 0.5));
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
