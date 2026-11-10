// Blossom Rally - heat shimmer behind the exhaust: a camera-facing quad that refracts what is behind it.
// One shared named GrabPass ("_BR_HazeGrab") is taken once per frame for all haze quads; the distortion
// fades to zero towards the quad's edges, so the quad itself is invisible.
Shader "BlossomRally/HeatHaze"
{
    Properties
    {
        _Strength ("Strength", Range(0, 2)) = 0
        _NoiseTex ("Noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        GrabPass { "_BR_HazeGrab" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BR_HazeGrab;
            sampler2D _NoiseTex;
            float _Strength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grab : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grab = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 q = i.uv * 2.0 - 1.0;
                float mask = saturate(1.0 - dot(q, q));
                mask *= mask * saturate(i.uv.y * 3.0);
                float2 n = tex2D(_NoiseTex, i.uv * float2(1.3, 0.9) + float2(0.0, -_Time.y * 1.7)).rg * 2.0 - 1.0;
                n += (tex2D(_NoiseTex, i.uv * float2(2.7, 1.9) + float2(_Time.y * 0.3, -_Time.y * 2.9)).gb * 2.0 - 1.0) * 0.5;
                float4 g = i.grab;
                g.xy += n * (_Strength * mask * 0.012) * g.w;
                fixed4 c = tex2Dproj(_BR_HazeGrab, UNITY_PROJ_COORD(g));
                c.a = saturate(mask * 4.0) * step(0.001, _Strength);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
