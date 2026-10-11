using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Head-locked feedback for the officer, built at runtime on a small world-space canvas in front of the HMD camera
// (screen-space overlays don't render in XR):
//   - red flash when hit
//   - orange haze + watery-eye flicker when the officer contaminates himself with OC spray
//   - fade to black (scenario end / restart)
//   - one-line subtitle for what the officer just said
// Uses only the built-in UI shader, so nothing extra has to be included in the Quest build.
[RequireComponent(typeof(PlayerRig))]
public class PlayerFeedbackOverlay : MonoBehaviour
{
    [Tooltip("Distance of the overlay from the eyes, in meters. Must be beyond the camera's near clip plane.")]
    [SerializeField] float distance = 0.25f;

    [SerializeField] Color damageColor = new Color(0.8f, 0f, 0f, 0.55f);

    [SerializeField] Color contaminationColor = new Color(1f, 0.55f, 0.1f, 0.45f);

    Image damageImage;
    Image contaminationImage;
    Image fadeImage;
    TextMeshProUGUI subtitle;

    float damageAlpha;
    float contaminationUntil;
    float contaminationStrength;
    float fadeTarget;
    float fadeAlpha;
    float fadeSpeed = 1f;
    float subtitleUntil;

    public bool IsContaminated => Time.time < contaminationUntil;

    void Start()
    {
        Build();
    }

    void Build()
    {
        if (!PlayerRig.TryGetHead(out Transform head))
        {
            Debug.LogWarning("PlayerFeedbackOverlay: no head camera found - overlay disabled.", this);
            enabled = false;
            return;
        }

        var canvasGo = new GameObject("PlayerFeedbackOverlay", typeof(Canvas));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, distance);
        canvasGo.transform.localRotation = Quaternion.identity;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 32000;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 1000f);
        // 1000 units -> wide enough to cover the whole field of view at this distance.
        float worldSize = distance * 4f;
        canvasGo.transform.localScale = Vector3.one * (worldSize / 1000f);

        contaminationImage = CreateImage(canvasGo.transform, "Contamination", contaminationColor);
        damageImage = CreateImage(canvasGo.transform, "Damage", damageColor);
        fadeImage = CreateImage(canvasGo.transform, "Fade", Color.black);

        var textGo = new GameObject("Subtitle", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        subtitle = textGo.AddComponent<TextMeshProUGUI>();
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.fontSize = 22f;
        subtitle.color = new Color(1f, 1f, 0.85f);
        subtitle.outlineWidth = 0.25f;
        subtitle.outlineColor = Color.black;
        subtitle.raycastTarget = false;
        var textRect = subtitle.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(360f, 80f);
        textRect.anchoredPosition = new Vector2(0f, -70f);
        subtitle.text = string.Empty;

        SetAlpha(damageImage, 0f);
        SetAlpha(contaminationImage, 0f);
        SetAlpha(fadeImage, 0f);
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    static void SetAlpha(Image image, float a)
    {
        if (image == null)
            return;
        Color c = image.color;
        c.a = a;
        image.color = c;
        image.enabled = a > 0.001f;
    }

    public void FlashDamage(float severity)
    {
        damageAlpha = Mathf.Clamp(0.35f + severity * 1.5f, 0.35f, damageColor.a);
    }

    public void Contaminate(float seconds, float strength)
    {
        contaminationUntil = Mathf.Max(contaminationUntil, Time.time + seconds);
        contaminationStrength = Mathf.Clamp01(Mathf.Max(contaminationStrength, strength));
    }

    public void FadeTo(float alpha, float seconds)
    {
        fadeTarget = Mathf.Clamp01(alpha);
        fadeSpeed = seconds > 0f ? 1f / seconds : 1000f;
    }

    public void ShowSubtitle(string text, float seconds = 2.5f)
    {
        if (subtitle == null)
            return;
        subtitle.text = text;
        subtitleUntil = Time.time + seconds;
    }

    void Update()
    {
        if (damageImage == null)
            return;

        damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.deltaTime * 1.2f);
        SetAlpha(damageImage, damageAlpha);

        if (IsContaminated)
        {
            float remaining = contaminationUntil - Time.time;
            float fade = Mathf.Clamp01(remaining / 2f);
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 3f, 0.5f);
            SetAlpha(contaminationImage, contaminationColor.a * contaminationStrength * fade * flicker);
        }
        else
        {
            contaminationStrength = 0f;
            SetAlpha(contaminationImage, 0f);
        }

        fadeAlpha = Mathf.MoveTowards(fadeAlpha, fadeTarget, Time.deltaTime * fadeSpeed);
        SetAlpha(fadeImage, fadeAlpha);

        if (subtitle != null && subtitle.text.Length > 0 && Time.time >= subtitleUntil)
            subtitle.text = string.Empty;
    }
}
