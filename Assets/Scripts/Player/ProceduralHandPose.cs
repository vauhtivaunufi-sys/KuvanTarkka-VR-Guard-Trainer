using System;
using UnityEngine;
using UnityEngine.XR;

// Curls a rigged hand mesh's finger joints from the controller's grip/trigger axes (read through ControllerInput, so the editor's
// desktop test tool can drive it too).
// Joints are located by name at startup (expects the "XRHand_<Finger><Joint>" naming used by
// the OpenXR/Meta hand rigs, e.g. XRHand_IndexProximal), so no per-bone wiring is needed in the
// Inspector - just drop this on the hand rig's root and set which controller it mirrors.
public class ProceduralHandPose : MonoBehaviour
{
    [Tooltip("Which physical controller this hand mirrors.")]
    [SerializeField] XRNode controllerNode = XRNode.RightHand;

    [Tooltip("Local-space axis each finger joint curls around. Flip the sign or swap the axis if fingers curl backward/sideways for this rig.")]
    [SerializeField] Vector3 curlAxis = Vector3.right;

    [Tooltip("Curl angle in degrees applied to a proximal joint at full curl. Intermediate/distal joints scale off this.")]
    [SerializeField] float maxCurlAngle = 80f;

    [Tooltip("How much of grip's curl the thumb follows, since grip alone should not fully close the thumb.")]
    [Range(0f, 1f)]
    [SerializeField] float thumbGripInfluence = 0.6f;

    [Tooltip("Higher = fingers snap to the target pose faster; lower = smoother/laggier.")]
    [SerializeField] float smoothing = 18f;

    struct Joint
    {
        public Transform transform;
        public Quaternion restLocalRotation;
        public float curlWeight;

        public Joint(Transform t, float weight)
        {
            transform = t;
            restLocalRotation = t != null ? t.localRotation : Quaternion.identity;
            curlWeight = weight;
        }
    }

    Joint[] indexJoints;
    Joint[] middleJoints;
    Joint[] ringJoints;
    Joint[] littleJoints;
    Joint[] thumbJoints;

    float grip;
    float trigger;

    void Awake()
    {
        indexJoints = FindFinger("Index");
        middleJoints = FindFinger("Middle");
        ringJoints = FindFinger("Ring");
        littleJoints = FindFinger("Little");
        thumbJoints = FindFinger("Thumb");
    }

    void Update()
    {
        Hand hand = controllerNode == XRNode.LeftHand ? Hand.Left : Hand.Right;
        float targetGrip = ControllerInput.GetAxis(hand, ControllerAxis.Grip);
        float targetTrigger = ControllerInput.GetAxis(hand, ControllerAxis.Trigger);

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        grip = Mathf.Lerp(grip, targetGrip, t);
        trigger = Mathf.Lerp(trigger, targetTrigger, t);

        Curl(indexJoints, trigger);
        Curl(middleJoints, grip);
        Curl(ringJoints, grip);
        Curl(littleJoints, grip);
        Curl(thumbJoints, grip * thumbGripInfluence);
    }

    void Curl(Joint[] joints, float amount)
    {
        if (joints == null)
            return;

        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint.transform == null)
                continue;

            float angle = maxCurlAngle * amount * joint.curlWeight;
            joint.transform.localRotation = joint.restLocalRotation * Quaternion.AngleAxis(angle, curlAxis);
        }
    }

    Joint[] FindFinger(string fingerName)
    {
        if (string.Equals(fingerName, "Thumb", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new Joint(FindJoint(fingerName + "Proximal"), 1f),
                new Joint(FindJoint(fingerName + "Distal"), 0.8f),
            };
        }

        return new[]
        {
            new Joint(FindJoint(fingerName + "Proximal"), 1f),
            new Joint(FindJoint(fingerName + "Intermediate"), 1.1f),
            new Joint(FindJoint(fingerName + "Distal"), 0.8f),
        };
    }

    Transform FindJoint(string nameContains)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        Debug.LogWarning($"ProceduralHandPose on '{name}' could not find a joint containing '{nameContains}'.", this);
        return null;
    }
}
