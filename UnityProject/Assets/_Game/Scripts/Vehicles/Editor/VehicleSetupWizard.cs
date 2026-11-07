// VehicleSetupWizard.cs - Tools > Rally > Set Up Vehicle From Data
// Builds a test-drivable vehicle hierarchy around an imported car FBX, using the
// <CAR>_vehicle_data.json written by export_vehicle_fbx.py:
//
//   CAR-009 (Rigidbody, VehicleWheelVisuals, RallyTestDriveController)
//     Model           <- the FBX instance (visual only, no colliders)
//     Colliders       <- BoxColliders from the data file (never the render mesh)
//     WheelColliders  <- one per wheel, at the visual wheel pivots
//
// It only creates objects in the open scene; save the result as a prefab yourself.
// Status: NOT yet compiled or tested in Unity. Review before use.
using System;
using UnityEditor;
using UnityEngine;

namespace RallyGame.Vehicles.EditorTools
{
    [Serializable] class WheelData { public string slot; public string transform; public float radius; public float width; public bool steer; public bool drive; }
    [Serializable] class ColliderData { public string name; public float[] unity_center; public float[] unity_size; }
    [Serializable] class VehicleData { public string car_id; public string display_name; public WheelData[] wheel_list; public ColliderData[] colliders; }

    public class VehicleSetupWizard : EditorWindow
    {
        GameObject model;            // FBX asset (from the Project window)
        TextAsset vehicleData;       // CAR-XXX_vehicle_data.json
        VehicleDefinition definition;

        [MenuItem("Tools/Rally/Set Up Vehicle From Data")]
        static void Open() => GetWindow<VehicleSetupWizard>("Vehicle Setup");

        void OnGUI()
        {
            model = (GameObject)EditorGUILayout.ObjectField("Imported FBX", model, typeof(GameObject), false);
            vehicleData = (TextAsset)EditorGUILayout.ObjectField("Vehicle data JSON", vehicleData, typeof(TextAsset), false);
            definition = (VehicleDefinition)EditorGUILayout.ObjectField("Vehicle definition", definition, typeof(VehicleDefinition), false);
            using (new EditorGUI.DisabledScope(model == null || vehicleData == null))
                if (GUILayout.Button("Create in scene")) Build();
        }

        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }

        void Build()
        {
            var data = JsonUtility.FromJson<VehicleData>(vehicleData.text);
            var root = new GameObject(string.IsNullOrEmpty(data.car_id) ? model.name : data.car_id);
            Undo.RegisterCreatedObjectUndo(root, "Create vehicle");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.name = "Model";
            foreach (var mc in inst.GetComponentsInChildren<Collider>()) DestroyImmediate(mc);

            var colRoot = new GameObject("Colliders").transform;
            colRoot.SetParent(root.transform, false);
            foreach (var c in data.colliders ?? Array.Empty<ColliderData>())
            {
                var go = new GameObject(c.name);
                go.transform.SetParent(colRoot, false);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = V(c.unity_center);
                bc.size = V(c.unity_size);
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = definition ? definition.massKg : 1100f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var visuals = root.AddComponent<VehicleWheelVisuals>();
            var wcRoot = new GameObject("WheelColliders").transform;
            wcRoot.SetParent(root.transform, false);
            var bindings = new WheelBinding[data.wheel_list.Length];
            float susp = definition ? definition.suspensionDistance : 0.2f;
            float target = definition ? definition.targetPosition : 0.5f;
            for (int i = 0; i < data.wheel_list.Length; i++)
            {
                var w = data.wheel_list[i];
                var visual = FindDeep(inst.transform, w.transform);
                if (visual == null) { Debug.LogError($"Wheel transform '{w.transform}' not found in {model.name}"); continue; }
                var go = new GameObject($"WheelCollider_{w.slot}");
                go.transform.SetParent(wcRoot, false);
                // Rest wheel centre = collider position - up * suspension * (1 - targetPosition)
                go.transform.position = visual.position + root.transform.up * susp * (1f - target);
                go.transform.rotation = root.transform.rotation;
                var wc = go.AddComponent<WheelCollider>();
                wc.radius = w.radius;
                wc.suspensionDistance = susp;
                wc.mass = 20f;
                bindings[i] = new WheelBinding { slot = w.slot, collider = wc, visual = visual, steer = w.steer, drive = w.drive };
            }
            visuals.wheels = bindings;
            var ctrl = root.AddComponent<RallyTestDriveController>();
            ctrl.definition = definition;
            Selection.activeGameObject = root;
            Debug.Log($"Vehicle '{root.name}' created. Enter Play Mode to test, then save it as a prefab.");
        }
    }
}
