// CarJuiceAudio.cs - extra 3D sound layers on the player car (all procedural, see JuiceAudio):
//  * gravel / dirt: pebbles pinging in the rear wheel arches and knocking the floor pan - random clicks
//    whose rate and loudness follow speed (Poisson timing so it never sounds like a loop), the odd rattle
//  * branch / tall-grass scrape loop + twig snaps when brushing past trees, bamboo or through grass
//  * doppler fly-by whoosh placed at trees / poles / walls passed close at speed (CarJuice calls Whoosh)
//  * cockpit wind roar (loud in cockpit / hood view, a faint layer in chase view), gusting with the weather
//  * water: splash one-shots and a wading churn loop
// Volumes scale with GameSettings.SfxVolume; pitches sag a little during slow motion; sources pause
// with AudioListener.pause like every other game sound.
using UnityEngine;

namespace BlossomRally
{
    [RequireComponent(typeof(ArcadeCar))]
    public class CarJuiceAudio : MonoBehaviour
    {
        ArcadeCar car;
        CarJuice juice;
        AudioSource archL, archR, floor, scrape, wind, churn, splash;
        readonly AudioSource[] whoosh = new AudioSource[3];
        int whooshIdx;
        float nextClick, nextKnock, nextTwig;
        float scrapeVol, windVol, churnVol;
        bool ready;

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            juice = GetComponent<CarJuice>();
            JuiceAudio.Ensure();
            Vector3 l = new Vector3(-0.75f, 0.45f, -1.2f), r = new Vector3(0.75f, 0.45f, -1.2f);
            foreach (var w in car.wheels)
                if (!w.front) { if (w.local.x < 0f) l = w.local + Vector3.up * w.radius * 0.8f; else r = w.local + Vector3.up * w.radius * 0.8f; }
            archL = Child("JuiceArchL", l, 0.75f);
            archR = Child("JuiceArchR", r, 0.75f);
            floor = Child("JuiceFloor", new Vector3(0f, 0.35f, 0f), 0.7f);
            splash = Child("JuiceSplash", new Vector3(0f, 0.3f, 0.6f), 0.75f);
            scrape = Loop(Child("JuiceScrape", new Vector3(0f, 0.9f, 0f), 0.7f), JuiceAudio.ScrapeLoop);
            churn = Loop(Child("JuiceChurn", new Vector3(0f, 0.3f, 0f), 0.7f), JuiceAudio.ChurnLoop);
            wind = Loop(Child("JuiceWind", new Vector3(0f, 1.1f, 0.2f), 0.5f), JuiceAudio.WindRoarLoop);
            for (int i = 0; i < whoosh.Length; i++)
            {
                var go = new GameObject("JuiceWhoosh" + i);      // world space: stays where the obstacle is
                var s = go.AddComponent<AudioSource>();
                Setup(s, 1f);
                s.dopplerLevel = 0.7f;
                s.minDistance = 3f;
                s.maxDistance = 90f;
                s.clip = JuiceAudio.Whoosh;
                whoosh[i] = s;
            }
            ready = true;
        }

        void OnDestroy()
        {
            for (int i = 0; i < whoosh.Length; i++) if (whoosh[i] != null) Destroy(whoosh[i].gameObject);
        }

        static void Setup(AudioSource s, float spatial)
        {
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 6f;
            s.maxDistance = 250f;
            s.volume = 1f;
        }

        AudioSource Child(string name, Vector3 local, float spatial)
        {
            var go = new GameObject(name);
            go.layer = CarExtras.CarLayer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var s = go.AddComponent<AudioSource>();
            Setup(s, spatial);
            return s;
        }

        static AudioSource Loop(AudioSource s, AudioClip c)
        {
            s.clip = c;
            s.loop = true;
            s.volume = 0f;
            s.Play();
            if (c != null) s.timeSamples = Random.Range(0, c.samples);
            return s;
        }

        static float SlowPitch()
        {
            // let slow motion sag the juice layers a little (AudioListener has no global pitch)
            return Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(0.3f, 1f, Time.timeScale));
        }

        void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;                      // paused: sources are paused by AudioListener.pause
            float sfx = GameSettings.SfxVolume;
            bool live = juice != null && juice.Live;
            float pk = SlowPitch();
            float kmh = live ? car.SpeedKmh : 0f;
            float k = Mathf.Clamp01(kmh / 100f);

            // ---- stones in the arches / under the floor
            if (live && kmh > 8f)
            {
                float rough = 1f + car.DriftIntensity * 0.8f;
                float clickRate = (juice.GravelFrac * (2f + 20f * k) + juice.DirtFrac * (0.6f + 6f * k)) * rough;
                nextClick -= dt;
                if (clickRate > 0.05f)
                {
                    if (nextClick <= 0f)
                    {
                        nextClick = -Mathf.Log(Random.Range(0.03f, 1f)) / clickRate;
                        var src = Random.value < 0.5f ? archL : archR;
                        src.pitch = Random.Range(0.85f, 1.25f) * pk;
                        bool rattle = Random.value < 0.06f + 0.1f * k * juice.GravelFrac;
                        AudioClip c = rattle ? JuiceAudio.StoneRattle : JuiceAudio.StoneClicks[Random.Range(0, JuiceAudio.StoneClicks.Length)];
                        src.PlayOneShot(c, Random.Range(0.25f, 0.6f) * (0.35f + 0.65f * k) * 0.55f * sfx);
                    }
                }
                else nextClick = Mathf.Max(nextClick, 0.05f);

                float knockRate = (juice.GravelFrac * 0.5f + juice.DirtFrac * 0.6f) * (0.3f + 3f * k) * rough;
                nextKnock -= dt;
                if (knockRate > 0.05f)
                {
                    if (nextKnock <= 0f)
                    {
                        nextKnock = -Mathf.Log(Random.Range(0.03f, 1f)) / knockRate;
                        floor.pitch = Random.Range(0.85f, 1.15f) * pk;
                        floor.PlayOneShot(JuiceAudio.FloorKnocks[Random.Range(0, JuiceAudio.FloorKnocks.Length)], Random.Range(0.3f, 0.65f) * (0.4f + 0.6f * k) * 0.5f * sfx);
                    }
                }
                else nextKnock = Mathf.Max(nextKnock, 0.05f);
            }

            // ---- branches / grass scraping the bodywork
            float scr = live ? Mathf.Max(juice.Brush * Mathf.Clamp01(kmh / 20f), juice.TallGrass * Mathf.Clamp01(kmh / 15f) * 0.75f) : 0f;
            scrapeVol = Mathf.MoveTowards(scrapeVol, scr, dt * 4f);
            scrape.volume = 0.42f * scrapeVol * sfx;
            scrape.pitch = (0.85f + 0.35f * k) * pk;
            if (live && juice.Brush > 0.45f && kmh > 10f)
            {
                nextTwig -= dt;
                if (nextTwig <= 0f)
                {
                    nextTwig = Random.Range(0.12f, 0.5f) / (0.6f + k);
                    var src = Random.value < 0.5f ? archL : archR;
                    src.pitch = Random.Range(0.9f, 1.2f) * pk;
                    src.PlayOneShot(JuiceAudio.TwigSnaps[Random.Range(0, JuiceAudio.TwigSnaps.Length)], Random.Range(0.25f, 0.5f) * juice.Brush * sfx);
                }
            }

            // ---- wind roar (cockpit)
            var gm = GameManager.Instance;
            bool inside = gm != null && gm.Rig != null && !gm.Rig.showroom && !gm.Rig.externalControl
                          && (gm.Rig.mode == CamMode.Cockpit || gm.Rig.mode == CamMode.Hood);
            float sf = Mathf.Clamp01(kmh / 190f);
            float wTarget = live ? (inside ? 0.05f * sf + 0.3f * sf * sf : 0.07f * sf * sf) * (1f + 0.5f * Atmosphere.Gust) : 0f;
            windVol = Mathf.MoveTowards(windVol, wTarget, dt * 0.8f);
            wind.volume = windVol * sfx;
            wind.spatialBlend = inside ? 0f : 0.5f;
            wind.pitch = (0.75f + 0.45f * sf + 0.05f * Atmosphere.Gust) * pk;

            // ---- wading
            float ch = live ? juice.Water * Mathf.Clamp01(kmh / 25f) : 0f;
            churnVol = Mathf.MoveTowards(churnVol, ch, dt * 3f);
            churn.volume = 0.4f * churnVol * sfx;
            churn.pitch = (0.85f + 0.3f * k) * pk;
        }

        /// <summary>Fly-by whoosh at an obstacle the car just passed close to.</summary>
        public void Whoosh(Vector3 pos, float kmh, float clearance)
        {
            if (!ready) return;
            var s = whoosh[whooshIdx];
            whooshIdx = (whooshIdx + 1) % whoosh.Length;
            if (s == null) return;
            s.transform.position = pos;
            s.pitch = Mathf.Lerp(0.85f, 1.3f, Mathf.Clamp01((kmh - 50f) / 130f)) * SlowPitch();
            s.volume = Mathf.Clamp01(0.35f + (2.4f - clearance) * 0.22f) * Mathf.Clamp01(kmh / 110f) * 0.8f * GameSettings.SfxVolume;
            s.Stop();
            s.Play();
        }

        /// <summary>Splash one-shot (size 0..1).</summary>
        public void Splash(Vector3 pos, float size)
        {
            if (!ready || splash == null) return;
            splash.pitch = Random.Range(0.9f, 1.1f) * SlowPitch();
            splash.PlayOneShot(size > 0.55f ? JuiceAudio.Splash : JuiceAudio.SplashSmall, Mathf.Clamp01(0.35f + 0.55f * size) * GameSettings.SfxVolume);
        }
    }
}
