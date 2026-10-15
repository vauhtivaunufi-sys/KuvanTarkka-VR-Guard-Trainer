using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

// Officer health bar above the left wrist, built at runtime
[RequireComponent(typeof(PlayerHealth))]
public class PlayerHealthBar : MonoBehaviour
{
    public enum Placement
    {
        Wrist,
        View
    }

    [SerializeField] Placement placement = Placement.Wrist;

    [Tooltip("Offset from the left controller (right, up, forward), m")]
    [SerializeField] Vector3 wristOffset = new Vector3(0f, 0.06f, -0.07f);

    [Tooltip("Offset from the eyes for the View placement, m")]
    [SerializeField] Vector3 viewOffset = new Vector3(-0.13f, -0.11f, 0.45f);

    [Tooltip("Panel size (width, height), m")]
    [SerializeField] Vector2 worldSize = new Vector2(0.13f, 0.026f);

    [SerializeField] Color fillColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    [SerializeField] Color lowFillColor = new Color(1f, 0.25f, 0.1f, 1f);
    [SerializeField] Color chipColor = new Color(1f, 0.85f, 0.8f, 0.9f);
    [SerializeField] Color trackColor = new Color(0.22f, 0.02f, 0.02f, 0.9f);
    [SerializeField] Color panelColor = new Color(0.03f, 0.04f, 0.06f, 0.7f);

    [Range(0f, 1f)]
    [SerializeField] float lowHealthFraction = 0.35f;

    [Tooltip("How fast the light chip catches up, bar fractions per second")]
    [SerializeField] float chipDrainSpeed = 0.5f;

    [SerializeField] int segments = 10;

    // Canvas is laid out in units and scaled down to meters
    const float UnitsPerMeter = 2000f;

    static Sprite roundedSprite;

    PlayerHealth health;
    Transform anchor;
    Transform head;
    RectTransform root;
    RectTransform content;
    RectTransform fill;
    RectTransform chip;
    Image fillImage;
    Image crossH;
    Image crossV;
    TextMeshProUGUI label;
    float chipFraction = 1f;
    int lastShownValue = -1;
    float shakeUntil;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        health.Damaged += HandleDamaged;
    }

    void OnDisable()
    {
        health.Damaged -= HandleDamaged;
    }

    void Start()
    {
        if (!PlayerRig.TryGetHead(out head))
        {
            Debug.LogWarning("PlayerHealthBar: no head camera, bar disabled", this);
            enabled = false;
            return;
        }

        anchor = placement == Placement.Wrist ? FindLeftController() : head;
        if (anchor == null)
        {
            placement = Placement.View;
            anchor = head;
        }

        Build();
    }

    void HandleDamaged(float damage, string attacker)
    {
        shakeUntil = Time.time + 0.25f;
    }

    // Child of the Camera Offset that holds the left-hand interactor
    Transform FindLeftController()
    {
        XROrigin origin = GetComponent<XROrigin>();
        Transform offset = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;

        foreach (XRBaseInteractor interactor in GetComponentsInChildren<XRBaseInteractor>(true))
        {
            if (interactor.handedness != InteractorHandedness.Left)
                continue;

            Transform t = interactor.transform;
            while (offset != null && t.parent != null && t.parent != offset)
                t = t.parent;
            return t;
        }
        return null;
    }

    void Build()
    {
        var canvasGo = new GameObject("PlayerHealthBar", typeof(Canvas));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root = canvasGo.GetComponent<RectTransform>();
        root.sizeDelta = worldSize * UnitsPerMeter;
        root.localScale = Vector3.one / UnitsPerMeter;

        float w = root.sizeDelta.x;
        float h = root.sizeDelta.y;

        // Shake moves this child, not the root, so placement stays stable
        content = NewRect("Content", root);
        Stretch(content);

        Image border = NewImage("Border", content, new Color(1f, 1f, 1f, 0.12f), true);
        Stretch(border.rectTransform);
        Image panel = NewImage("Panel", content, panelColor, true);
        Stretch(panel.rectTransform, 2f);

        // Medical cross icon
        float icon = h * 0.55f;
        float iconX = h * 0.5f;
        crossH = NewImage("CrossH", content, fillColor, false);
        Place(crossH.rectTransform, iconX, 0f, icon, icon * 0.32f);
        crossV = NewImage("CrossV", content, fillColor, false);
        Place(crossV.rectTransform, iconX, 0f, icon * 0.32f, icon);

        // Bar track between the icon and the number
        float barLeft = h;
        float barRight = w - h * 1.35f;
        float barH = h * 0.42f;
        RectTransform bar = NewRect("Bar", content);
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.anchoredPosition = new Vector2(barLeft, 0f);
        bar.sizeDelta = new Vector2(barRight - barLeft, barH);

        Stretch(NewImage("Track", bar, trackColor, true).rectTransform);
        chip = NewImage("Chip", bar, chipColor, true).rectTransform;
        Stretch(chip);
        fillImage = NewImage("Fill", bar, fillColor, true);
        fill = fillImage.rectTransform;
        Stretch(fill);

        // Soft highlight on the upper half for a glossy look
        Image shine = NewImage("Shine", bar, new Color(1f, 1f, 1f, 0.16f), true);
        RectTransform shineRect = shine.rectTransform;
        shineRect.anchorMin = new Vector2(0f, 0.5f);
        shineRect.anchorMax = Vector2.one;
        shineRect.offsetMin = new Vector2(2f, 0f);
        shineRect.offsetMax = new Vector2(-2f, -1f);

        // Segment ticks every 1/segments of the bar
        for (int i = 1; i < segments; i++)
        {
            Image tick = NewImage("Tick", bar, new Color(0f, 0f, 0f, 0.35f), false);
            RectTransform t = tick.rectTransform;
            t.anchorMin = new Vector2((float)i / segments, 0f);
            t.anchorMax = new Vector2((float)i / segments, 1f);
            t.sizeDelta = new Vector2(1.5f, 0f);
        }

        label = NewText("Value", content);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(1f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-h * 0.2f, 0f);
        labelRect.sizeDelta = new Vector2(h * 1.15f, h);
        label.fontSize = h * 0.55f;

        if (placement == Placement.View)
        {
            canvasGo.transform.SetParent(head, false);
            canvasGo.transform.localPosition = viewOffset;
            canvasGo.transform.localRotation = Quaternion.identity;
        }

        Refresh(true);
    }

    void LateUpdate()
    {
        if (root == null)
            return;

        // Follows the wrist but stays upright and faces the eyes, so wrist twists don't spin it
        if (placement == Placement.Wrist)
        {
            root.position = anchor.TransformPoint(wristOffset);
            Vector3 toBar = root.position - head.position;
            if (toBar.sqrMagnitude > 0.0001f)
                root.rotation = Quaternion.LookRotation(toBar, Vector3.up);
        }

        float shake = Time.time < shakeUntil ? (shakeUntil - Time.time) * 12f : 0f;
        content.anchoredPosition = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;

        Refresh(false);
    }

    void Refresh(bool immediate)
    {
        float fraction = health.MaxHealth > 0f ? Mathf.Clamp01(health.Health / health.MaxHealth) : 0f;

        fill.anchorMax = new Vector2(fraction, 1f);
        fillImage.enabled = fraction > 0f;

        chipFraction = immediate ? fraction : Mathf.MoveTowards(chipFraction, fraction, chipDrainSpeed * Time.deltaTime);
        chipFraction = Mathf.Max(chipFraction, fraction);
        chip.anchorMax = new Vector2(chipFraction, 1f);

        Color c = fillColor;
        if (fraction > 0f && fraction <= lowHealthFraction)
        {
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * 6f));
            c = Color.Lerp(lowFillColor * 0.5f, lowFillColor, pulse);
            c.a = 1f;
        }
        fillImage.color = c;
        crossH.color = crossV.color = c;

        int value = Mathf.CeilToInt(health.Health);
        if (value != lastShownValue)
        {
            lastShownValue = value;
            label.text = value.ToString();
        }
    }

    // UI helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color, bool rounded)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (rounded)
        {
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    static TextMeshProUGUI NewText(string name, Transform parent)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Right;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 200);
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // White rounded rectangle made in code, 9-sliced so corners keep their shape at any size
    static Sprite RoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

        const int size = 32;
        const float radius = 10f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - dist + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return roundedSprite;
    }
}
