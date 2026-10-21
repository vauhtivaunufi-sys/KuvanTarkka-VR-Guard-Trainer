using UnityEngine;

// Clicks VR panel buttons with the trigger: controller rays in the headset, the crosshair in desktop testing
[RequireComponent(typeof(PlayerRig))]
public class VrPointer : MonoBehaviour
{
    [Tooltip("How far the ray reaches, m")]
    [SerializeField] float maxDistance = 8f;

    [SerializeField] Color rayColor = new Color(1f, 1f, 1f, 0.6f);

    PlayerRig rig;
    readonly LineRenderer[] lines = new LineRenderer[2];
    VrButton hovered;
    Hand hoveredHand;

    void Awake()
    {
        rig = GetComponent<PlayerRig>();
    }

    void OnDisable()
    {
        SetHovered(null, Hand.Right);
        HideLines();
    }

    void Update()
    {
        VrButton target = null;
        Hand hand = Hand.Right;

        if (ControllerInput.Override != null)
        {
            // Desktop testing: aim with the view, left mouse is the right trigger
            HideLines();
            if (PlayerRig.TryGetHead(out Transform head))
                Cast(head.position, head.forward, out target, out _);
        }
        else
        {
            bool rightHit = TryHand(Hand.Right, out VrButton rightTarget);
            bool leftHit = TryHand(Hand.Left, out VrButton leftTarget);
            // The right hand wins when both point at a button
            if (rightTarget != null)
                target = rightTarget;
            else if (leftTarget != null)
            {
                target = leftTarget;
                hand = Hand.Left;
            }
            if (!rightHit && !leftHit)
                HideLines();
        }

        SetHovered(target, hand);

        if (hovered != null && ControllerInput.GetButtonDown(hoveredHand, ControllerButton.Trigger))
        {
            VrButton clicked = hovered;
            Haptics.MenuTick(hoveredHand);
            clicked.Click();
        }
    }

    // Casts from one controller and draws its ray when it hits a panel
    bool TryHand(Hand hand, out VrButton target)
    {
        target = null;
        Transform controller = rig.GetController(hand);
        if (controller == null)
        {
            ShowLine(hand, false, default, default);
            return false;
        }

        bool hit = Cast(controller.position, controller.forward, out target, out Vector3 point);
        // Checked only on a hit, finding items every frame would be wasted work
        if (hit && HandHoldsItem(hand))
        {
            hit = false;
            target = null;
        }
        ShowLine(hand, hit, controller.position, point);
        return hit;
    }

    bool Cast(Vector3 origin, Vector3 direction, out VrButton target, out Vector3 point)
    {
        target = null;
        point = default;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, 1 << VrUi.UiLayer, QueryTriggerInteraction.Collide))
            return false;

        point = hit.point;
        target = hit.collider.GetComponent<VrButton>();
        return true;
    }

    // A hand holding the taser or spray keeps its trigger for the item
    static bool HandHoldsItem(Hand hand)
    {
        return GrabbableWeapon.HeldIn(hand) != null;
    }

    void SetHovered(VrButton target, Hand hand)
    {
        if (target == hovered && hand == hoveredHand)
            return;

        if (hovered != null)
            hovered.SetHovered(false);

        bool changed = target != hovered;
        hovered = target;
        hoveredHand = hand;

        if (hovered != null)
        {
            hovered.SetHovered(true);
            if (changed && ControllerInput.Override == null)
                Haptics.MenuTick(hand);
        }
    }

    void ShowLine(Hand hand, bool visible, Vector3 from, Vector3 to)
    {
        LineRenderer line = lines[(int)hand];
        if (!visible)
        {
            if (line != null)
                line.enabled = false;
            return;
        }

        if (line == null)
            line = lines[(int)hand] = CreateLine(hand);
        line.enabled = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }

    void HideLines()
    {
        foreach (LineRenderer line in lines)
        {
            if (line != null)
                line.enabled = false;
        }
    }

    LineRenderer CreateLine(Hand hand)
    {
        LineRenderer line = LineVisuals.Create($"{hand} Pointer Ray", 0.004f, rayColor);
        RuntimeRoot.Adopt(line.gameObject);
        line.endWidth = 0.002f;
        line.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0.15f);
        return line;
    }
}
