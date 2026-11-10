// Blossom Rally - tinted car glass.
// Optional per-car extras (all zero / unset = plain tinted glass, so the shared material is unchanged):
//  * _Dirt / _DirtColor: dust film on every window (follows the car's mud level)
//  * windscreen frame in car space (_CarW2L world -> car matrix, _ScreenO bottom-centre, _ScreenU up-the-glass
//    direction + slant height in w, _ScreenN outward normal + half width in w) maps the windscreen to a 0..1 UV:
//      _WipeTex  R = windscreen dirt (wiper arcs are cleaned by the script), G = freshly wiped (rain drops gone)
//      _CrackTex R = crack lines (painted by the damage script)
//  * _Rain: rain drops on the glass (hidden where the wipers just passed)
// Two passes: the inside faces (seen from the cockpit) show only dirt / cracks / drops, so the cockpit view is
// not darkened by the tint; the outside faces show tint + everything.
Shader "BlossomRally/Glass"
{
    Properties
    {
        _Color ("Tint", Color) = (0.04, 0.06, 0.08, 0.82)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _Dirt ("Dirt", Range(0, 1)) = 0
        _DirtColor ("Dirt Colour", Color) = (0.45, 0.36, 0.26, 1)
        _Rain ("Rain Drops", Range(0, 1)) = 0
        _WipeTex ("Windscreen Dirt (R) Fresh Wipe (G)", 2D) = "black" {}
        _CrackTex ("Windscreen Cracks (R)", 2D) = "black" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _ScreenO ("Screen Origin", Vector) = (0, 0, 0, 0)
        _ScreenU ("Screen Up / Height", Vector) = (0, 0, 0, 0)
        _ScreenN ("Screen Normal / Half Width", Vector) = (0, 0, 0, 0)
    }

    CGINCLUDE
    fixed4 _Color;
    half _Glossiness;
    half _Dirt;
    fixed4 _DirtColor;
    half _Rain;
    sampler2D _WipeTex;
    sampler2D _CrackTex;
    sampler2D _NoiseTex;
    float4 _ScreenO;
    float4 _ScreenU;
    float4 _ScreenN;
    float4x4 _CarW2L;

    struct Input
    {
        float3 viewDir;
        float3 worldNormal;
        float3 worldPos;
    };

    // dirt amount, crack amount and rain-drop amount at this point of the glass
    void GlassFX (Input IN, out half dirt, out half crack, out half drops)
    {
        float3 p = mul(_CarW2L, float4(IN.worldPos, 1.0)).xyz;
        float3 n = mul((float3x3)_CarW2L, IN.worldNormal);
        float nl = length(n);
        n = nl > 1e-4 ? n / nl : float3(0, 0, 0);
        float valid = step(0.001, _ScreenU.w) * step(0.001, _ScreenN.w);
        float2 suv = float2(p.x / max(2.0 * _ScreenN.w, 0.01) + 0.5,
                            dot(p - _ScreenO.xyz, _ScreenU.xyz) / max(_ScreenU.w, 0.01));
        float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
        float onScreen = valid * inside * saturate((abs(dot(n, _ScreenN.xyz)) - 0.7) * 8.0);
        half4 w = tex2D(_WipeTex, suv);
        half4 nz = tex2D(_NoiseTex, p.xy * 2.3 + p.zz * 1.7);
        // windscreen: per-pixel dirt from the wiper mask; other windows: the car's general dust film
        half film = _Dirt * 0.75 * (1.15 - saturate(p.y * 0.4));
        dirt = lerp(film, w.r, onScreen);
        dirt = saturate(dirt * (0.65 + 0.7 * nz.r));
        crack = tex2D(_CrackTex, suv).r * onScreen;
        half4 dn = tex2D(_NoiseTex, p.xy * 7.0 + float2(p.z * 5.0, _Time.y * 0.02));
        drops = _Rain * smoothstep(0.66, 0.74, dn.g) * (1.0 - w.g * onScreen);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        // ---- inside faces: only what sits on the glass (seen from the cockpit camera)
        Cull Front
        CGPROGRAM
        #pragma surface surfInner Standard alpha:fade
        #pragma target 3.0

        void surfInner (Input IN, inout SurfaceOutputStandard o)
        {
            half dirt, crack, drops;
            GlassFX(IN, dirt, crack, drops);
            o.Albedo = lerp(_DirtColor.rgb * 0.8, float3(0.85, 0.88, 0.9), saturate(crack + drops * 0.5));
            o.Metallic = 0.0;
            o.Smoothness = lerp(0.2, 0.9, saturate(crack + drops));
            o.Alpha = saturate(0.03 + dirt * 0.62 + crack * 0.55 + drops * 0.18);
        }
        ENDCG

        // ---- outside faces: tint + reflections + dirt / cracks / drops
        Cull Back
        CGPROGRAM
        #pragma surface surfOuter Standard alpha:fade
        #pragma target 3.0

        void surfOuter (Input IN, inout SurfaceOutputStandard o)
        {
            half dirt, crack, drops;
            GlassFX(IN, dirt, crack, drops);
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), 4.0);
            float3 c = lerp(_Color.rgb, _DirtColor.rgb, saturate(dirt * 1.3));
            c = lerp(c, float3(0.8, 0.84, 0.88), saturate(crack * 0.8 + drops * 0.25));
            o.Albedo = c;
            o.Metallic = 0.1;
            o.Smoothness = lerp(_Glossiness, 0.3, saturate(dirt * 1.5)) ;
            o.Alpha = saturate(max(_Color.a + fres * 0.15, dirt * 0.92) + crack * 0.5 + drops * 0.12);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
