// EngineAudio.cs - procedural engine note + tyre squeal (no audio files needed).
using UnityEngine;

namespace BlossomRally
{
    /// <summary>Procedural engine note + tyre squeal (no audio files needed).</summary>
    [RequireComponent(typeof(ArcadeCar))]
    public class EngineAudio : MonoBehaviour
    {
        ArcadeCar car;
        AudioSource engine, squeal;

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            int rate = 44100;
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = MakeEngineClip(rate);
            engine.loop = true;
            engine.spatialBlend = 0.6f;
            engine.volume = 0.25f;
            engine.dopplerLevel = 0f;
            engine.Play();
            squeal = gameObject.AddComponent<AudioSource>();
            squeal.clip = MakeNoiseClip(rate);
            squeal.loop = true;
            squeal.spatialBlend = 0.6f;
            squeal.volume = 0f;
            squeal.dopplerLevel = 0f;
            squeal.Play();
        }

        static AudioClip MakeEngineClip(int rate)
        {
            // 1 second loop, 55 Hz fundamental: rich, slightly gritty four-cylinder buzz
            var data = new float[rate];
            var rnd = new System.Random(3);
            for (int i = 0; i < rate; i++)
            {
                double t = i / (double)rate;
                double s = 0.55 * System.Math.Sin(2 * System.Math.PI * 55 * t) + 0.30 * System.Math.Sin(2 * System.Math.PI * 110 * t + 0.5)
                           + 0.18 * System.Math.Sin(2 * System.Math.PI * 165 * t + 1.1) + 0.12 * System.Math.Sin(2 * System.Math.PI * 220 * t + 0.2)
                           + 0.06 * System.Math.Sin(2 * System.Math.PI * 330 * t);
                double pulse = 0.75 + 0.25 * System.Math.Sin(2 * System.Math.PI * 27.5 * t);
                data[i] = (float)(s * pulse * 0.45 + (rnd.NextDouble() - 0.5) * 0.04);
            }
            var clip = AudioClip.Create("BR_Engine", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoiseClip(int rate)
        {
            var data = new float[rate];
            var rnd = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < rate; i++)
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.55f + n * 0.45f;
                double t = i / (double)rate;
                data[i] = lp * 0.5f * (float)(0.8 + 0.2 * System.Math.Sin(2 * System.Math.PI * 880 * t));
            }
            var clip = AudioClip.Create("BR_Squeal", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Update()
        {
            if (car == null || engine == null) return;
            engine.pitch = Mathf.Lerp(0.55f, 2.6f, Mathf.InverseLerp(850f, 7800f, car.EngineRpm));
            engine.volume = 0.16f + 0.16f * car.throttle;
            bool onRoad = false;
            foreach (var w in car.wheels) if (w.grounded && (w.surface == Surface.Asphalt || w.surface == Surface.Paving)) onRoad = true;
            float target = onRoad ? car.DriftIntensity * 0.32f : car.DriftIntensity * 0.08f;
            squeal.volume = Mathf.Lerp(squeal.volume, target, Time.deltaTime * 8f);
            squeal.pitch = 0.9f + car.DriftIntensity * 0.25f;
        }
    }
}
