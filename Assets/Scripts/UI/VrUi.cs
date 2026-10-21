using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds world-space VR panels from code, all positions and sizes in millimetres from the top-left corner
public static class VrUi
{
    public const float UnitsPerMeter = 1000f;
    public const int UiLayer = 5;

    public static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.11f, 0.95f);
    public static readonly Color CardColor = new Color(0.11f, 0.14f, 0.18f, 1f);
    public static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.28f, 1f);
    public static readonly Color AccentColor = new Color(0.95f, 0.55f, 0.1f, 1f);
    public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f, 1f);
    public static readonly Color MutedColor = new Color(0.64f, 0.69f, 0.75f, 1f);

    static Sprite roundedSprite;

    // World-space canvas with a rounded background that stops the pointer ray
    public static RectTransform CreatePanel(string name, Vector2 sizeMm)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        RuntimeRoot.Adopt(go);
        go.layer = UiLayer;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)go.transform;
        root.sizeDelta = sizeMm;
        root.localScale = Vector3.one / UnitsPerMeter;

        Image background = NewImage("Background", root, PanelColor);
        RectTransform bg = background.rectTransform;
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;

        // Thin collider on the surface so the ray stops here instead of passing through
        var surface = background.gameObject.AddComponent<BoxCollider>();
        surface.isTrigger = true;
        surface.size = new Vector3(sizeMm.x, sizeMm.y, 2f);
        return root;
    }

    public static Image Card(RectTransform parent, float x, float y, float w, float h)
    {
        Image card = NewImage("Card", parent, CardColor);
        Place(card.rectTransform, x, y, w, h);
        return card;
    }

    public static TextMeshProUGUI Text(RectTransform parent, string text, float x, float y, float w, float h, float size,
        bool bold = false, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var label = NewObject("Text", parent).AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.color = color ?? TextColor;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.Normal;
        // Long texts shrink to fit their box instead of spilling out
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = size * 0.65f;
        label.raycastTarget = false;
        Place(label.rectTransform, x, y, w, h);
        return label;
    }

    public static VrButton Button(RectTransform parent, string label, float x, float y, float w, float h, float size,
        Action onClick, bool primary = false)
    {
        Image background = NewImage("Button", parent, primary ? AccentColor : ButtonColor);
        Place(background.rectTransform, x, y, w, h);

        TextMeshProUGUI text = NewObject("Label", background.rectTransform).AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = primary ? new Color(0.08f, 0.06f, 0.03f) : TextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        RectTransform t = text.rectTransform;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = new Vector2(16f, 8f);
        t.offsetMax = new Vector2(-16f, -8f);

        var button = background.gameObject.AddComponent<VrButton>();
        button.Init(background, text, onClick);
        return button;
    }

    // Puts the panel at a point and turns it to face the eyes, upright
    public static void PlaceInFrontOf(Transform panel, Vector3 position, Vector3 eyes)
    {
        panel.position = position;
        Vector3 away = position - eyes;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f)
            panel.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        // Colliders moved while the game is paused only reach physics after a sync
        Physics.SyncTransforms();
    }

    // Panel at a comfortable reading distance straight ahead, slightly below eye level
    public static void PlaceAhead(Transform panel, float distance = 1.4f, float belowEyes = 0.15f)
    {
        PlayerRig rig = PlayerRig.Instance;
        if (rig == null || rig.Head == null)
            return;

        Vector3 eyes = rig.HeadPosition;
        Vector3 position = eyes + rig.FlatForward * distance + Vector3.down * belowEyes;
        PlaceInFrontOf(panel, position, eyes);
    }

    // Top-left based placement in millimetres
    public static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    static GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var image = NewObject(name, parent).AddComponent<Image>();
        image.color = color;
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    // White rounded rectangle made in code, 9-sliced so corners keep their shape at any size
    public static Sprite RoundedSprite()
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
