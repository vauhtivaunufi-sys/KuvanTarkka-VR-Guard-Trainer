using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


// Door that swings open when used, alone or as one leaf of a double door with otherLeaf

[RequireComponent(typeof(XRSimpleInteractable))]
public class Door : MonoBehaviour
{
    [Tooltip("Pivot the door rotates around. Must be this door's own transform or a child of it. Defaults to this transform.")]
    [SerializeField] Transform hinge;

    [Tooltip("How far this leaf swings open, in degrees.")]
    [SerializeField] float openAngle = 90f;

    [Tooltip("Time to fully open or close, in seconds.")]
    [SerializeField] float swingDuration = 1f;

    [Tooltip("Second leaf of a double door, opened together with this one. Leave empty for a single door.")]
    [SerializeField] Door otherLeaf;

    [Tooltip("Marks this as a prisoner cell door. Used by CellDoorLink to decide whether it should attach to this door.")]
    [SerializeField] bool isCellDoor;

    public bool IsOpen { get; private set; }

    public bool IsCellDoor => isCellDoor;

   
    public event Action OnDoorOpened;

    Quaternion closedRotation;
    Coroutine swingRoutine;

    void Awake()
    {
        if (hinge == null)
        {
            hinge = transform;
        }
        else if (!hinge.IsChildOf(transform))
        {
            // Fail loud, a wrong Hinge once spun the player's rig instead of the door
            Debug.LogError($"Door '{name}': Hinge is set to '{hinge.name}', which is not part of this door. Fix the Hinge field - falling back to this door's own transform for now.", this);
            hinge = transform;
        }

        closedRotation = hinge.localRotation;

        EnsureCollider();
    }

    void OnEnable()
    {
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(HandleSelectEntered);
    }

    void OnDisable()
    {
        GetComponent<XRSimpleInteractable>().selectEntered.RemoveListener(HandleSelectEntered);
    }

    void HandleSelectEntered(SelectEnterEventArgs args)
    {
        Toggle();
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;

        IsOpen = open;

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);
        swingRoutine = StartCoroutine(Swing(open));

        if (otherLeaf != null)
            otherLeaf.SetOpen(open);
    }

    IEnumerator Swing(bool open)
    {
        Quaternion from = hinge.localRotation;
        Quaternion to = open ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;

        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            hinge.localRotation = Quaternion.Slerp(from, to, elapsed / swingDuration);
            yield return null;
        }

        hinge.localRotation = to;

        if (open)
            OnDoorOpened?.Invoke();
    }

    // XRSimpleInteractable needs a collider and FBX doors usually have none, so add a box fitted to the mesh
    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"Door '{name}' has no collider and no renderer to size one from - it cannot be interacted with.", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 scale = transform.lossyScale;
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
    }
}
