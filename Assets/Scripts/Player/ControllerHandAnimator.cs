using UnityEngine;
using UnityEngine.XR;


/// Drives a hand model's Animator from the physical controller's grip/trigger axes,
/// so squeezing a button visibly curls the corresponding fingers.

public class ControllerHandAnimator : MonoBehaviour
{
    [Tooltip("Which physical controller this hand mirrors.")]
    [SerializeField] XRNode controllerNode = XRNode.RightHand;

    [Tooltip("Animator with float parameters \"Grip\" and \"Trigger\" driving the finger pose.")]
    [SerializeField] Animator handAnimator;

    static readonly int GripHash = Animator.StringToHash("Grip");
    static readonly int TriggerHash = Animator.StringToHash("Trigger");

    InputDevice device;

    void Awake()
    {
        if (handAnimator == null)
            handAnimator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
    }

    void Update()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(controllerNode);

        if (!device.isValid || handAnimator == null)
            return;

        if (device.TryGetFeatureValue(CommonUsages.grip, out float grip))
            handAnimator.SetFloat(GripHash, grip);

        if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigger))
            handAnimator.SetFloat(TriggerHash, trigger);
    }
}
