using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Button on a VR panel: the pointer ray hovers it, the trigger clicks it
public class VrButton : MonoBehaviour
{
    Image background;
    TextMeshProUGUI label;
    Action onClick;
    Color normalColor;
    bool hovered;
    bool selected;

    public TextMeshProUGUI Label => label;

    // Stays highlighted, used for the chosen item in a list
    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            Refresh();
        }
    }

    public void Init(Image backgroundImage, TextMeshProUGUI text, Action click)
    {
        background = backgroundImage;
        label = text;
        onClick = click;
        normalColor = background.color;

        // Trigger collider a little in front of the panel surface, so the ray hits the button first
        Rect r = background.rectTransform.rect;
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(r.center.x, r.center.y, -5f);
        box.size = new Vector3(r.width, r.height, 10f);

        Refresh();
    }

    public void SetHovered(bool value)
    {
        if (hovered == value)
            return;
        hovered = value;
        Refresh();
    }

    public void Click()
    {
        onClick?.Invoke();
    }

    void Refresh()
    {
        if (background == null)
            return;

        Color c = normalColor;
        if (selected)
            c = VrUi.AccentColor;
        else if (hovered)
            c = Color.Lerp(normalColor, Color.white, 0.25f);
        background.color = c;

        if (label != null && normalColor != VrUi.AccentColor)
            label.color = selected ? new Color(0.08f, 0.06f, 0.03f) : VrUi.TextColor;
    }
}
