using System;
using UnityEngine;
using UnityEngine.XR;

// Curls the hand's fingers from grip and trigger (joints found by XRHand_ names) and turns the hand round held grips
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

    [Header("Hand placement")]
    [Tooltip("Turn the hand round the controller's handle, and round the grip of a held item, instead of lying palm down")]
    [SerializeField] bool alignHand = true;

    [Tooltip("Lean of the controller handle, degrees: the empty hand holds it like a real Touch controller")]
    [SerializeField] float controllerHandleRake = 30f;

    [Tooltip("How closed the fingers are round a held grip, 1 is a tight fist that cuts into a thick handle")]
    [Range(0f, 1f)]
    [SerializeField] float heldGrip = 0.75f;

    [Tooltip("Shift of the hand on a held grip, metres: x right, y up the handle, z forward")]
    [SerializeField] Vector3 heldOffset;

    [Tooltip("Extra turn of the hand on a held grip, degrees")]
    [SerializeField] Vector3 heldRotation;

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

    // Hand frame in its own space (forward up the handle, up along the fingers) and where a handle sits in the fist
    bool canAlign;
    Quaternion handFrame;
    Vector3 handCentre;
    Vector3 emptyCentre;

    bool IsRight => controllerNode != XRNode.LeftHand;

    void Awake()
    {
        indexJoints = FindFinger("Index");
        middleJoints = FindFinger("Middle");
        ringJoints = FindFinger("Ring");
        littleJoints = FindFinger("Little");
        thumbJoints = FindFinger("Thumb");
        CacheHandFrame();
    }

    // Measured once from the rest pose, curling never moves the knuckles so it stays valid
    void CacheHandFrame()
    {
        Transform index = indexJoints[0].transform;
        Transform middle = middleJoints[0].transform;
        Transform little = littleJoints[0].transform;
        if (!alignHand || transform.parent == null || index == null || middle == null || little == null)
            return;

        Transform wrist = FindJoint("Wrist", false);
        Vector3 w = wrist != null ? wrist.position : transform.position;
        Vector3 fingers = middle.position - w;
        Vector3 across = index.position - little.position;
        Vector3 palm = Vector3.Cross(fingers, across).normalized * (IsRight ? 1f : -1f);
        // A handle lies under the knuckles, a few cm out of the palm
        Vector3 centre = Vector3.Lerp(w, middle.position, 0.75f) + palm * 0.035f;

        handFrame = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(across, fingers);
        handCentre = transform.InverseTransformPoint(centre);
        emptyCentre = transform.parent.InverseTransformPoint(centre);
        canAlign = true;
    }

    void Update()
    {
        Hand hand = controllerNode == XRNode.LeftHand ? Hand.Left : Hand.Right;
        float targetGrip = ControllerInput.GetAxis(hand, ControllerAxis.Grip);
        float targetTrigger = ControllerInput.GetAxis(hand, ControllerAxis.Trigger);

        // Holding something: the hand stays round it even when grip is let go (keyboard testing toggles grabs)
        if (GrabbableWeapon.HeldIn(hand) != null)
        {
            targetGrip = heldGrip;
            // Index finger rests on the trigger
            targetTrigger = Mathf.Max(targetTrigger, 0.35f);
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        grip = Mathf.Lerp(grip, targetGrip, t);
        trigger = Mathf.Lerp(trigger, targetTrigger, t);

        Curl(indexJoints, trigger);
        Curl(middleJoints, grip);
        Curl(ringJoints, grip);
        Curl(littleJoints, grip);
        Curl(thumbJoints, grip * thumbGripInfluence);
    }

    // After the controller and the held item have moved this frame
    void LateUpdate()
    {
        if (!canAlign)
            return;

        Transform parent = transform.parent;
        Quaternion frame;
        Vector3 centre;
        GrabbableWeapon item = GrabbableWeapon.HeldIn(IsRight ? Hand.Right : Hand.Left);
        if (item != null && item.TryGetHandGrip(out Vector3 gripCentre, out Vector3 handleUp, out Vector3 forward))
        {
            // Everything in the controller's space, so the hand stays glued when tracking updates late
            Vector3 right = Vector3.Cross(handleUp, forward);
            Vector3 shifted = gripCentre + right * heldOffset.x + handleUp * heldOffset.y + forward * heldOffset.z;
            Quaternion toParent = Quaternion.Inverse(parent.rotation);
            frame = Quaternion.LookRotation(toParent * handleUp, toParent * forward) * Quaternion.Euler(heldRotation);
            centre = parent.InverseTransformPoint(shifted);
        }
        else
        {
            // Empty hand round the controller handle, palm facing in, turned about where the handle sits
            Quaternion rake = Quaternion.AngleAxis(controllerHandleRake, Vector3.right);
            frame = Quaternion.LookRotation(rake * Vector3.up, rake * Vector3.forward);
            centre = emptyCentre;
        }

        Quaternion targetRotation = frame * Quaternion.Inverse(handFrame);
        Vector3 targetPosition = centre - targetRotation * Vector3.Scale(handCentre, transform.localScale);
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, t);
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

    Transform FindJoint(string nameContains, bool warn = true)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        if (warn)
            Debug.LogWarning($"ProceduralHandPose on '{name}' could not find a joint containing '{nameContains}'.", this);
        return null;
    }
}
