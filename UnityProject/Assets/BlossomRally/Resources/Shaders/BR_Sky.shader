// Blossom Rally - soft pastel gradient sky with a hazy sun glow (skybox material).
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
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Horizon;
            fixed4 _Bottom;
            fixed4 _SunColor;
            float4 _SunDir;
            float _Exposure;

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
                col += _SunColor.rgb * (pow(sd, 6.0) * 0.35 + pow(sd, 64.0) * 0.8 + pow(sd, 900.0) * 3.0);
                return fixed4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
