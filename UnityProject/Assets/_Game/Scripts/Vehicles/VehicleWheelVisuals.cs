// VehicleWheelVisuals.cs - drives the visual wheel transforms from WheelColliders.
// Physics owns the colliders; this script only copies their pose to the visual pivots
// (CAR-XXX_Wheel_FL/FR/RL/RR from the FBX). Never animate those pivots from Blender too.
//
// The visual pivots may carry an import rotation (the Blender exporter puts the axis
// conversion on CAR-XXX_Root), so each wheel stores its rest-rotation offset relative
// to its collider at Awake and re-applies it every frame. This makes the script
// independent of how the FBX hierarchy is oriented.
//
// Status: NOT yet compiled or tested in Unity. Review before use.
using System;
using UnityEngine;

namespace RallyGame.Vehicles
{
    [Serializable]
    public class WheelBinding
    {
        public string slot = "FL";            // FL, FR, RL, RR
        public WheelCollider collider;
        public Transform visual;              // CAR-XXX_Wheel_<slot> pivot (axle centre)
        public bool steer;
        public bool drive = true;
        [NonSerialized] public Quaternion restOffset = Quaternion.identity;
    }

    [DisallowMultipleComponent]
    public class VehicleWheelVisuals : MonoBehaviour
    {
        public WheelBinding[] wheels = new WheelBinding[4];
        [Tooltip("Move the visual wheel with suspension travel (keep off if the pivot must stay fixed).")]
        public bool applySuspensionPosition = true;

        void Awake()
        {
            foreach (var w in wheels)
            {
                if (w == null || w.collider == null || w.visual == null) continue;
                // At rest the collider pose has no steer and no spin, so this captures the
                // fixed difference between the collider frame and the imported visual frame.
                w.restOffset = Quaternion.Inverse(w.collider.transform.rotation) * w.visual.rotation;
            }
        }

        void LateUpdate()
        {
            foreach (var w in wheels)
            {
                if (w == null || w.collider == null || w.visual == null) continue;
                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                if (applySuspensionPosition) w.visual.position = pos;
                w.visual.rotation = rot * w.restOffset;
            }
        }
    }
}
