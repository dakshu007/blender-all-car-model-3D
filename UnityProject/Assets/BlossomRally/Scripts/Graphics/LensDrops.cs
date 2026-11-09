// LensDrops.cs - raindrops and the odd mud speck on the chase camera lens (cockpit / hood views have
// WindscreenFX instead). Drops are drawn into a small signed lens texture (rg = refraction offset,
// b = mud, a = coverage) that the PostFX composite pass reads. No per-frame allocations.
using UnityEngine;

namespace BlossomRally
{
    public class LensDrops
    {
        struct Drop
        {
            public Vector2 pos;
            public float size, life, maxLife;
            public bool mud;
            public float seed;
        }

        const int Max = 36;
        readonly Drop[] drops = new Drop[Max];
        int count;
        RenderTexture rt;
        float mudCooldown;

        public int Count { get { return count; } }

        void Add(Vector2 pos, float size, float life, bool mud)
        {
            if (count >= Max) return;
            drops[count++] = new Drop { pos = pos, size = size, life = life, maxLife = life, mud = mud, seed = Random.value * 7f };
        }

        /// <summary>Spawn / age drops. dt is unscaled; nothing happens while the game is paused.</summary>
        public void Update(float dt, bool enabled)
        {
            var gm = GameManager.Instance;
            bool chase = enabled && gm != null && gm.Car != null && gm.Rig != null && gm.IsDriving && !gm.Rig.externalControl
                         && !gm.Rig.showroom && gm.Rig.mode == CamMode.Chase && Time.timeScale > 0f;
            if (!chase)
            {
                // fade out quickly when leaving the chase view
                for (int i = 0; i < count; i++) drops[i].life = Mathf.Min(drops[i].life, 0.3f);
            }
            else
            {
                var car = gm.Car;
                float speed = car.SpeedKmh;
                float rain = Atmosphere.RainIntensity;
                if (rain > 0.02f && Random.value < rain * (1.1f + Mathf.Clamp01(speed / 120f) * 1.6f) * dt)
                    Add(new Vector2(Random.value, Random.Range(0.08f, 0.95f)), Random.Range(0.012f, 0.03f), Random.Range(2.5f, 6f), false);

                // mud: rear wheels throwing dirt / grass at speed, now and then
                mudCooldown -= dt;
                if (mudCooldown <= 0f && speed > 45f)
                {
                    int dirty = 0;
                    var ws = car.wheels;
                    for (int i = 0; i < ws.Length; i++)
                    {
                        var w = ws[i];
                        if (w == null || w.front || !w.grounded) continue;
                        if (w.surface == Surface.Dirt || w.surface == Surface.Grass || w.surface == Surface.Gravel) dirty++;
                    }
                    if (dirty > 0)
                    {
                        float chance = 0.10f * Mathf.Clamp01((speed - 45f) / 70f) * (1f + car.DriftIntensity * 2f) * (1f + Atmosphere.Wetness);
                        if (Random.value < chance * dt * 10f)
                        {
                            Add(new Vector2(Random.Range(0.08f, 0.92f), Random.Range(0.05f, 0.45f)), Random.Range(0.01f, 0.026f), Random.Range(5f, 9f), true);
                            mudCooldown = Random.Range(0.6f, 2.5f);
                        }
                        else mudCooldown = 0.1f;
                    }
                }

                // airflow pushes drops up and outwards at speed, slow drops run down
                float air = Mathf.Clamp01((speed - 25f) / 90f);
                for (int i = 0; i < count; i++)
                {
                    if (drops[i].mud) continue;
                    Vector2 p = drops[i].pos;
                    Vector2 dir = new Vector2((p.x - 0.5f) * 0.5f, 0.9f) * air + new Vector2(0f, -0.06f) * (1f - air);
                    drops[i].pos = p + dir * (0.03f + air * 0.25f) * dt;
                }
                if (rain > 0.3f)
                    for (int i = 0; i < count; i++) if (drops[i].mud) drops[i].life -= dt * rain;   // rain washes mud off
            }
            for (int i = count - 1; i >= 0; i--)
            {
                drops[i].life -= dt;
                Vector2 p = drops[i].pos;
                if (drops[i].life <= 0f || p.y > 1.1f || p.y < -0.1f || p.x < -0.1f || p.x > 1.1f)
                    drops[i] = drops[--count];
            }
        }

        /// <summary>Draw the drops into the lens texture (quarter res) using the given material pass.</summary>
        public RenderTexture Render(Material mat, int pass, int screenW, int screenH)
        {
            if (count == 0) return null;
            int w = Mathf.Max(16, screenW / 4), h = Mathf.Max(16, screenH / 4);
            if (rt == null || rt.width != w || rt.height != h)
            {
                Release();
                var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear) { name = "BR_LensDrops", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                rt.Create();
            }
            var prev = RenderTexture.active;
            Graphics.SetRenderTarget(rt);
            GL.Clear(false, true, Color.clear);
            mat.SetTexture("_DropTex", GfxTextures.Drop);
            mat.SetPass(pass);
            GL.PushMatrix();
            GL.LoadOrtho();
            GL.Begin(GL.QUADS);
            float aspect = (float)screenW / Mathf.Max(1, screenH);
            for (int i = 0; i < count; i++)
            {
                var d = drops[i];
                float age = d.maxLife - d.life;
                float a = Mathf.Clamp01(d.life / 0.6f) * Mathf.Clamp01(age / 0.12f);
                float sy = d.size * (d.mud ? 1.25f : 1f), sx = sy / aspect;
                float x0 = d.pos.x - sx, x1 = d.pos.x + sx, y0 = d.pos.y - sy, y1 = d.pos.y + sy;
                Vector3 data = new Vector3(d.mud ? 1f : 0f, a, d.seed);
                GL.MultiTexCoord(1, data); GL.MultiTexCoord2(0, 0f, 0f); GL.Vertex3(x0, y0, 0f);
                GL.MultiTexCoord(1, data); GL.MultiTexCoord2(0, 0f, 1f); GL.Vertex3(x0, y1, 0f);
                GL.MultiTexCoord(1, data); GL.MultiTexCoord2(0, 1f, 1f); GL.Vertex3(x1, y1, 0f);
                GL.MultiTexCoord(1, data); GL.MultiTexCoord2(0, 1f, 0f); GL.Vertex3(x1, y0, 0f);
            }
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = prev;
            return rt;
        }

        public void Release()
        {
            if (rt != null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                rt = null;
            }
        }
    }
}
