// Blossom Rally - glowing paper lanterns (sky lanterns, floating lotus lanterns).
// Vertex colour RGB (sRGB) drives both the lit paper colour and the emission (_Glow, raised at night
// by script so bloom picks it up); vertex colour ALPHA fades a lantern in / out.
Shader "BlossomRally/LifeGlow"
{
    Properties
    {
        _Glow ("Glow", Float) = 0.6
        _Lit ("Sun Lit Amount", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Glow;
            float _Lit;

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
                float shade : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float4 c = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = GammaToLinearSpace(c.rgb);
                #endif
                o.color = c;
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.shade = 0.8 + 0.2 * n.y;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 amb = unity_AmbientSky.rgb;
                float3 lit = i.color.rgb * lerp(1.0, amb * 0.8 + 0.45, _Lit) * i.shade;
                fixed4 c = fixed4(lit * 0.65 + i.color.rgb * _Glow, i.color.a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
