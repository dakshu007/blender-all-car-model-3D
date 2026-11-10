// Blossom Rally - soft additive light (bamboo-forest light shafts, fireflies).
// Texture alpha * vertex colour * _Tint, faded near / far from the camera (_Fade = near start, near end,
// far start, far end) and, for flat shaft quads, when seen edge-on (_EdgeFade). Fog fades it to black.
// _VertexGamma = 1 converts mesh vertex colours from sRGB (0 for particles, which are already linear).
Shader "BlossomRally/PropAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Fade ("Fade (near0, near1, far0, far1)", Vector) = (2, 8, 140, 200)
        _EdgeFade ("Edge-on Fade", Range(0, 1)) = 0
        _VertexGamma ("Vertex Colour is sRGB", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+3" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
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
            fixed4 _Tint;
            float4 _Fade;
            float _EdgeFade;
            float _VertexGamma;

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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 c = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_VertexGamma > 0.5) c.rgb = GammaToLinearSpace(c.rgb);
                #endif
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toCam = _WorldSpaceCameraPos - wp;
                float d = length(toCam);
                float f = smoothstep(_Fade.x, _Fade.y, d) * (1.0 - smoothstep(_Fade.z, _Fade.w, d));
                float3 n = normalize(UnityObjectToWorldNormal(v.normal) + float3(1e-4, 1e-4, 1e-4));
                float facing = abs(dot(n, toCam / max(d, 1e-3)));
                f *= lerp(1.0, smoothstep(0.08, 0.5, facing), _EdgeFade);
                c.a *= f;
                o.color = c * _Tint;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
