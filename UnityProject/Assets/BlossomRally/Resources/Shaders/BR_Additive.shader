// Blossom Rally - soft additive glow for light beams and exhaust flames.
// Vertex colour (sRGB) * alpha is the brightness along the mesh (e.g. fading down a headlight cone);
// _Soft fades surfaces seen edge-on (a cone looks like a soft volume instead of a hard shell).
// Fog fades it to black (additive), so beams disappear naturally into the distance.
Shader "BlossomRally/Additive"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Soft ("Edge Softness", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _Soft;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 wpos : TEXCOORD0;
                float3 wnrm : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                float4 c = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = GammaToLinearSpace(c.rgb);
                #endif
                o.color = c;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float facing = abs(dot(normalize(i.wnrm), V));
                float k = pow(saturate(facing), _Soft);
                fixed4 col = fixed4(i.color.rgb * i.color.a * _Color.rgb * _Intensity * k, 1.0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
