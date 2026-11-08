// Blossom Rally - far-tree impostor: alpha-tested captured tree image on crossed quads, tinted by the
// current scene light (_BR_SceneLight from Atmosphere, so impostors darken at night), gentle sway, fog.
Shader "BlossomRally/Impostor"
{
    Properties
    {
        _MainTex ("Captured Tree", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;
            float4 _BR_SceneLight;
            float4 _BR_WindGust;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float sway = sin(_Time.y * 1.3 + wp.x * 0.07 + wp.z * 0.05) * 0.18 * (1.0 + _BR_WindGust.x) * v.uv.y * v.uv.y;
                wp.x += sway;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);
                float3 light = _BR_SceneLight.a > 0.0 ? _BR_SceneLight.rgb : float3(1, 1, 1);
                c.rgb *= light;
                c.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
