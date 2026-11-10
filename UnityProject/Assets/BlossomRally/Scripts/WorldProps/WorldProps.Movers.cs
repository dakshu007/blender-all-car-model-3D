// WorldProps.Movers.cs - the few moving props (water wheels, the temple bell and its striker log) as one
// CPU-posed LifeBatch, plus the bell / drum towers that sound when the player drives past (3D AudioSources
// with procedurally synthesised clips).
using UnityEngine;

namespace BlossomRally
{
    public partial class WorldProps
    {
        LifeBatch movers;
        int bellPart = -1, logPart = -1;
        float logT = -1f, logAngle, bellT = -1f, bellAngle;
        bool bellStruck, bellArmed = true, drumArmed = true, moversDirty = true;
        float bellCool, drumCool;
        AudioSource bellSrc, drumSrc;
        const float RingRadius = 50f, RearmRadius = 75f;

        void FinishMovers()
        {
            if (wheels.Count == 0 && !hasBell) return;
            movers = new LifeBatch();
            if (wheels.Count > 0)
            {
                int wt = movers.AddTemplate(PropShapes.WaterWheel(WheelR));
                foreach (var w in wheels) w.part = movers.AddPart(wt);
            }
            if (hasBell)
            {
                bellPart = movers.AddPart(movers.AddTemplate(PropShapes.Bell()));
                logPart = movers.AddPart(movers.AddTemplate(PropShapes.StrikerLog()));
            }
            movers.Finish("Props_Movers", MaterialLibrary.VertexColorWind, transform, WorldBuilder.LayerTrees, true);
            Count("animated parts", movers.PartCount);
            PoseMovers(true, Vector3.zero);
        }

        void PoseMovers(bool force, Vector3 eye)
        {
            if (movers == null) return;
            foreach (var w in wheels)
                if (force || (w.hub - eye).sqrMagnitude < 220f * 220f)
                    movers.Set(w.part, Matrix4x4.TRS(w.hub, Quaternion.Euler(0f, w.yaw, 0f) * Quaternion.AngleAxis(w.angle, Vector3.right), Vector3.one));
            if (bellPart >= 0 && (force || moversDirty))
            {
                Quaternion q = Quaternion.Euler(0f, towerYaw, 0f);
                movers.Set(bellPart, Matrix4x4.TRS(bellHangW, q * Quaternion.AngleAxis(-bellAngle, Vector3.right), Vector3.one));
                movers.Set(logPart, Matrix4x4.TRS(logPivotW, q * Quaternion.AngleAxis(-logAngle, Vector3.right), Vector3.one));
            }
            moversDirty = false;
            movers.Upload();
        }

        void UpdateMovers(float dt, Vector3 eye)
        {
            if (movers == null) return;
            foreach (var w in wheels) w.angle = Mathf.Repeat(w.angle + w.spin * dt, 360f);
            // striker log: pull back, swing into the bell, bounce back
            if (logT >= 0f)
            {
                logT += dt;
                if (logT < 0.7f) logAngle = -22f * Mathf.SmoothStep(0f, 1f, logT / 0.7f);
                else if (logT < 0.95f) logAngle = Mathf.Lerp(-22f, 14f, Mathf.Pow((logT - 0.7f) / 0.25f, 2f));
                else
                {
                    if (!bellStruck) { bellStruck = true; Strike(); }
                    float u = logT - 0.95f;
                    logAngle = 14f * Mathf.Exp(-u * 2.2f) * Mathf.Cos(u * 5f);
                    if (logT > 3.5f) { logT = -1f; logAngle = 0f; }
                }
                moversDirty = true;
            }
            if (bellT >= 0f)
            {
                bellT += dt;
                bellAngle = 4f * Mathf.Exp(-bellT * 0.8f) * Mathf.Sin(bellT * 3.2f);
                if (bellT > 7f) { bellT = -1f; bellAngle = 0f; }
                moversDirty = true;
            }
            PoseMovers(false, eye);
        }

        void Strike()
        {
            bellT = 0f;
            if (bellSrc != null && bellSrc.clip != null)
            {
                bellSrc.volume = 0.9f * GameSettings.SfxVolume;
                bellSrc.Play();
            }
        }

        void UpdateTowers(float dt)
        {
            bellCool -= dt;
            drumCool -= dt;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || gm.Car == null) return;
            Vector3 car = gm.Car.transform.position;
            if (hasBell)
            {
                float d = Vector2.Distance(new Vector2(car.x, car.z), new Vector2(bellTowerPos.x, bellTowerPos.z));
                if (d > RearmRadius) bellArmed = true;
                if (bellArmed && d < RingRadius && bellCool <= 0f && logT < 0f)
                {
                    bellArmed = false;
                    bellCool = 15f;
                    logT = 0f;
                    bellStruck = false;
                }
            }
            if (hasDrum && drumSrc != null && drumSrc.clip != null)
            {
                float d = Vector2.Distance(new Vector2(car.x, car.z), new Vector2(drumTowerPos.x, drumTowerPos.z));
                if (d > RearmRadius) drumArmed = true;
                if (drumArmed && d < RingRadius && drumCool <= 0f)
                {
                    drumArmed = false;
                    drumCool = 15f;
                    drumSrc.volume = 0.85f * GameSettings.SfxVolume;
                    drumSrc.Play();
                }
            }
        }

        AudioSource Source3D(string name, Vector3 pos, AudioClip clip, float maxDist)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 18f;
            s.maxDistance = maxDist;
            s.priority = 100;
            return s;
        }

        void BuildAudio()
        {
            if (hasBell) bellSrc = Source3D("Props_BellAudio", bellHangW, PropFX.TempleBell(), 420f);
            if (hasDrum) drumSrc = Source3D("Props_DrumAudio", drumTowerPos, PropFX.TowerDrum(), 320f);
        }
    }
}
