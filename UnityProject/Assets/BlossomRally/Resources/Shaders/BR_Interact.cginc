// Blossom Rally - shared car <-> vegetation interaction (set by BlossomRally.CarFX every frame).
//   _BR_CarA        xyz = front axle centre on the ground, w = push radius (m)
//   _BR_CarB        xyz = rear axle centre on the ground,  w = car speed (m/s)
//   _BR_Trail[6]    xyz = recent car positions (0 = now), w = time stamp (_Time.y)
//   _BR_TrailSpeed  car speed at each trail point
// Plants inside the car's footprint are pushed aside and flattened; plants the car has just
// driven through keep shaking for a second or so, then settle.
#ifndef BR_INTERACT_INCLUDED
#define BR_INTERACT_INCLUDED

float4 _BR_CarA;
float4 _BR_CarB;
float4 _BR_Trail[6];
float _BR_TrailSpeed[6];

float2 BR_SegClosest(float2 p, float2 a, float2 b, out float t)
{
    float2 ab = b - a;
    t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
    return a + ab * t;
}

// root: plant (or blade root) world position, h: 0 at the root .. 1 at the tip
float3 BR_Interact(float3 root, float h)
{
    float3 off = float3(0, 0, 0);
    // cheap early out: nothing happens far away from the car
    float2 toCar = root.xz - _BR_CarA.xz;
    if (dot(toCar, toCar) > 4900.0) return off;
    float r = max(_BR_CarA.w, 0.01);
    float t;
    float2 c = BR_SegClosest(root.xz, _BR_CarA.xz, _BR_CarB.xz, t);
    float2 d = root.xz - c;
    float dist = length(d);
    float yGap = abs(root.y - lerp(_BR_CarA.y, _BR_CarB.y, t));
    float infl = saturate(1.0 - dist / r) * saturate(2.2 - yGap);
    infl = infl * infl * (3.0 - 2.0 * infl);
    float2 dir = d / max(dist, 1e-3);
    off.xz += dir * infl * 0.6 * h;
    off.y -= infl * 0.5 * h;

    // shaking wake behind the car
    float shake = 0.0;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float tt;
        float2 q = BR_SegClosest(root.xz, _BR_Trail[i].xz, _BR_Trail[i + 1].xz, tt);
        float dd = length(root.xz - q);
        float age = _Time.y - lerp(_BR_Trail[i].w, _BR_Trail[i + 1].w, tt);
        float spd = lerp(_BR_TrailSpeed[i], _BR_TrailSpeed[i + 1], tt);
        float yg = abs(root.y - lerp(_BR_Trail[i].y, _BR_Trail[i + 1].y, tt));
        float fall = saturate(1.0 - dd / 2.6) * saturate(2.2 - yg) * saturate(spd / 12.0);
        shake = max(shake, fall * exp(-max(age, 0.0) * 2.2));
    }
    float ph = root.x * 3.1 + root.z * 2.7;
    off.x += sin(_Time.y * 19.0 + ph) * shake * 0.17 * h;
    off.z += cos(_Time.y * 15.0 + ph * 1.3) * shake * 0.17 * h;
    off.y -= shake * 0.06 * h;
    return off;
}
#endif
