using TMPro;
using UnityEngine;

// Spoken line floating above a character, gameplay UI that stays on with debug off
public class SpeechBubble : MonoBehaviour
{
    [Tooltip("Gap above the top of the collider, m")]
    [SerializeField] float heightAboveHead = 0.9f;

    [Tooltip("Line height up close, m")]
    [SerializeField] float textHeight = 0.08f;

    [Tooltip("From this distance the text grows to keep the same size on screen, m")]
    [SerializeField] float constantSizeFrom = 3f;

    [Tooltip("Largest growth factor at long range")]
    [SerializeField] float maxScale = 4f;

    [SerializeField] Color color = new Color(1f, 0.95f, 0.75f);

    TextMeshPro text;
    Collider bodyCollider;
    float hideTime;

    void Awake()
    {
        bodyCollider = GetComponent<Collider>();

        // Standalone object, so the character's scale doesn't distort the text
        var go = new GameObject($"{name} Speech", typeof(RectTransform));
        RuntimeRoot.Adopt(go);
        text = go.AddComponent<TextMeshPro>();
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.rectTransform.sizeDelta = new Vector2(1.4f, 0.4f);
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.Normal;
        // 3D TextMeshPro: font size 1 is 0.1 m
        text.fontSize = textHeight * 10f;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        go.SetActive(false);
    }

    void OnDestroy()
    {
        if (text != null)
            Destroy(text.gameObject);
    }

    public void Say(string line, float duration = 2.5f)
    {
        if (text == null || string.IsNullOrEmpty(line))
            return;

        text.text = line;
        hideTime = Time.time + duration;
        text.gameObject.SetActive(true);
        LateUpdate();
    }

    void LateUpdate()
    {
        if (text == null || !text.gameObject.activeSelf)
            return;

        if (Time.time >= hideTime)
        {
            text.gameObject.SetActive(false);
            return;
        }

        float top = bodyCollider != null ? bodyCollider.bounds.max.y : transform.position.y + 1.8f;
        Transform t = text.transform;
        t.position = new Vector3(transform.position.x, top + heightAboveHead, transform.position.z);

        if (PlayerRig.TryGetHead(out Transform head))
        {
            Vector3 fromHead = t.position - head.position;
            float distance = fromHead.magnitude;
            if (distance > 0.01f)
                t.rotation = Quaternion.LookRotation(fromHead, Vector3.up);
            t.localScale = Vector3.one * Mathf.Clamp(distance / constantSizeFrom, 1f, maxScale);
        }
    }
}
