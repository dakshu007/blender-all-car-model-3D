// Blossom Rally - cheap replacement shader for the planar lake reflection camera (WaterFX).
// Opaque things draw as vertex colour x texture with a simple sun + ambient light and fog; far-tree
// impostors keep their alpha test. No shadows, no wind - the reflection is low-res and rippled anyway.
Shader "Hidden/BlossomRally/ReflectionReplace"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Cutoff", Float) = 0.5
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    fixed _Cutoff;
    float4 _BR_SceneLight;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        half3 col : TEXCOORD1;
        UNITY_FOG_COORDS(2)
    };

    v2f vertLit (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        float3 n = UnityObjectToWorldNormal(v.normal);
        half3 c = v.color.rgb;
    #ifndef UNITY_COLORSPACE_GAMMA
        c = GammaToLinearSpace(c);
    #endif
        half3 light = ShadeSH9(half4(n, 1)) + _LightColor0.rgb * saturate(dot(n, _WorldSpaceLightPos0.xyz));
        o.col = c * light;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    fixed4 fragLit (v2f i) : SV_Target
    {
        fixed4 c = fixed4(i.col * tex2D(_MainTex, i.uv).rgb, 1);
        UNITY_APPLY_FOG(i.fogCoord, c);
        return c;
    }

    v2f vertImpostor (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.col = _BR_SceneLight.rgb;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    fixed4 fragImpostor (v2f i) : SV_Target
    {
        fixed4 t = tex2D(_MainTex, i.uv);
        clip(t.a - _Cutoff);
        fixed4 c = fixed4(t.rgb * i.col, 1);
        UNITY_APPLY_FOG(i.fogCoord, c);
        return c;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vertLit
            #pragma fragment fragLit
            #pragma multi_compile_fog
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma vertex vertImpostor
            #pragma fragment fragImpostor
            #pragma multi_compile_fog
            ENDCG
        }
    }
    FallBack Off
}
