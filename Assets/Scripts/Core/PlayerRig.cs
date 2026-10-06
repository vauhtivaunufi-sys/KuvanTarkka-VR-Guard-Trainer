using Unity.XR.CoreUtils;
using UnityEngine;

// Single point of truth for "where is the player": head, feet and facing.
// Lives on the XR Origin. Every system that used to look up Camera.main on its own (prisoner AI, attack, labels) asks this instead,
// so the answer is consistent and cheap. Falls back to Camera.main when the component is missing, so nothing hard-breaks in a test scene.

[DisallowMultipleComponent]
public class PlayerRig : MonoBehaviour
{
    [Tooltip("The HMD camera. Left empty, the MainCamera under this rig is used.")]
    [SerializeField] Transform head;

    [Tooltip("Approximate height of the chest below the eyes, in meters. Used as the aim point for attacks and line-of-sight checks.")]
    [SerializeField] float chestBelowHead = 0.35f;

    static PlayerRig instance;
    static float nextSearchTime;

    // The rig in the scene. If nobody added the component, it attaches itself to the XR Origin on first use,
    // so older scenes keep working without manual setup.
    public static PlayerRig Instance
    {
        get
        {
            if (instance == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;
                instance = FindFirstObjectByType<PlayerRig>();
                if (instance == null)
                {
                    XROrigin origin = FindFirstObjectByType<XROrigin>();
                    if (origin != null)
                        instance = origin.gameObject.AddComponent<PlayerRig>();
                }
            }
            return instance;
        }
    }

    // Head (HMD) transform. Never null while a camera exists.
    public Transform Head
    {
        get
        {
            if (head == null && Camera.main != null)
                head = Camera.main.transform;
            return head;
        }
    }

    public Vector3 HeadPosition => Head != null ? Head.position : transform.position;

    public Vector3 ChestPosition => HeadPosition + Vector3.down * chestBelowHead;

    // Head projected onto the rig's floor - the player's "feet" for distance checks.
    public Vector3 FeetPosition
    {
        get
        {
            Vector3 p = HeadPosition;
            p.y = transform.position.y;
            return p;
        }
    }

    // Horizontal facing of the head.
    public Vector3 FlatForward
    {
        get
        {
            if (Head == null)
                return transform.forward;
            Vector3 f = Head.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }
    }

    void Awake()
    {
        if (head == null)
        {
            XROrigin origin = GetComponent<XROrigin>();
            if (origin != null && origin.Camera != null)
                head = origin.Camera.transform;
        }

        if (instance != null && instance != this)
        {
            Debug.LogWarning("More than one PlayerRig in the scene - only the first one is used.", this);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // True when the transform is part of the XR rig hierarchy (head, hands, belt). Held equipment is not reparented by XRI,
    // so callers that also need to ignore held items check for GrabbableWeapon separately.
    public bool IsPartOfPlayer(Transform t)
    {
        return t != null && t.IsChildOf(transform);
    }

    // Static helpers that work even without a PlayerRig in the scene.
    public static bool TryGetHead(out Transform headTransform)
    {
        PlayerRig rig = Instance;
        if (rig != null && rig.Head != null)
        {
            headTransform = rig.Head;
            return true;
        }

        Camera cam = Camera.main;
        headTransform = cam != null ? cam.transform : null;
        return headTransform != null;
    }
}
