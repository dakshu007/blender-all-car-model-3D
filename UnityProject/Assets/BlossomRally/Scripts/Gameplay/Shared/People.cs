// People.cs - SHARED CONTRACT: build a person (player, pedestrians, shopkeepers, monks, bike riders) and pose it.
// PLACEHOLDER implementation (simple capsule figure); the on-foot module replaces the internals with the real
// stylised, animated character. Keep the public API stable.
using UnityEngine;

namespace BlossomRally
{
    public enum PersonPose { Idle = 0, Walk = 1, Run = 2, Sit = 3, Drive = 4, Ride = 5, Wave = 6, Work = 7, Pray = 8 }

    /// <summary>Appearance. Use Random(seed) for crowds.</summary>
    public struct PersonLook
    {
        public Color skin, hair, top, bottom, shoes, accent;
        public int hairStyle, outfit;      // outfit: 0 casual, 1 rally suit, 2 shopkeeper/apron, 3 monk robe, 4 business, 5 winter coat, 6 hanfu
        public bool hat, female;
        public float height;               // metres, ~1.55 .. 1.9

        public static PersonLook Random(int seed)
        {
            var r = new System.Random(seed);
            System.Func<float> f = () => (float)r.NextDouble();
            var l = new PersonLook();
            l.skin = Color.Lerp(new Color(0.96f, 0.80f, 0.66f), new Color(0.62f, 0.44f, 0.32f), f());
            l.hair = Color.Lerp(new Color(0.08f, 0.07f, 0.07f), new Color(0.35f, 0.22f, 0.12f), f() * f());
            l.top = Color.HSVToRGB(f(), 0.35f + 0.4f * f(), 0.45f + 0.5f * f());
            l.bottom = Color.HSVToRGB(f(), 0.2f + 0.3f * f(), 0.2f + 0.4f * f());
            l.shoes = Color.Lerp(Color.black, Color.white, f() * f());
            l.accent = Color.HSVToRGB(f(), 0.7f, 0.9f);
            l.hairStyle = r.Next(6); l.outfit = 0; l.female = f() < 0.5f; l.hat = f() < 0.15f;
            l.height = 1.58f + 0.3f * f();
            return l;
        }

        /// <summary>The player's driver: rally suit.</summary>
        public static PersonLook Player
        {
            get
            {
                var l = Random(7);
                l.outfit = 1; l.top = new Color(0.95f, 0.95f, 0.93f); l.bottom = new Color(0.95f, 0.95f, 0.93f);
                l.accent = new Color(0.93f, 0.36f, 0.52f); l.height = 1.76f; l.hat = false;
                return l;
            }
        }
    }

    /// <summary>A built person. Root sits on the ground at its feet, facing +Z.</summary>
    public class PersonRig : MonoBehaviour
    {
        public PersonLook look;
        public PersonPose pose = PersonPose.Idle;
        /// <summary>Ground speed in m/s (drives walk / run cycle speed).</summary>
        public float speed;
        /// <summary>Named attachment points (may be null in the placeholder).</summary>
        public Transform head, handL, handR, hips;

        public virtual void SetPose(PersonPose p) { pose = p; }
    }

    public static class PersonFactory
    {
        /// <summary>Create a person at pos facing yaw (degrees). layer: render layer (default 0).</summary>
        public static PersonRig Create(PersonLook look, Vector3 pos, float yaw, Transform parent = null, int layer = 0)
        {
            var root = new GameObject("Person");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var rig = root.AddComponent<PersonRig>();
            rig.look = look;
            float h = look.height > 0.5f ? look.height : 1.75f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            body.transform.localScale = new Vector3(0.45f, h * 0.5f, 0.3f);
            body.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Standard("PersonPlaceholder", look.top, 0.2f, 0f);
            body.layer = layer;
            rig.hips = body.transform;
            rig.head = body.transform;
            return rig;
        }
    }
}
