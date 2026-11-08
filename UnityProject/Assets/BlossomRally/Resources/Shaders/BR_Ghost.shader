// Blossom Rally - ghost car: translucent, unlit, pale pink-white with a soft fresnel rim.
// Pass 1 writes depth only, so pass 2 draws just the front-most surface of the car
// (no ugly overlapping of wheels / interior through the body).
Shader "BlossomRally/Ghost"
{
    Properties
    {
        _Color ("Body", Color) = (1.0, 0.90, 0.94, 1)
        _RimColor ("Rim", Color) = (1.0, 0.72, 0.84, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.2
        _Glow ("Glow", Range(0, 4)) = 1.3
        _Alpha ("Alpha", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Name "DEPTH"
            ZWrite On
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }

        Pass
        {
            Name "GHOST"
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;
            half _Glow;
            half _Alpha;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 vd = normalize(i.viewDir);
                float rim = pow(1.0 - saturate(abs(dot(n, vd))), _RimPower);
                float3 c = lerp(_Color.rgb, _RimColor.rgb * _Glow, rim);
                float a = saturate(_Alpha * (0.55 + rim * 1.2));
                fixed4 col = fixed4(c, a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
