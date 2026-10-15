using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Small helpers to build uGUI from code (used by the pause menu and HUD so they work in every
/// scene without editing each scene by hand).
/// </summary>
public static class RuntimeUI
{
    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button Button(Transform parent, string label, Vector2 pos, UnityAction onClick)
    {
        var img = Panel(parent, "Button_" + label, new Color(1f, 1f, 1f, 0.12f));
        Rect(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420, 70));
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(1, 1, 1, 0.12f);
        colors.highlightedColor = new Color(1, 1, 1, 0.3f);
        colors.pressedColor = new Color(1, 1, 1, 0.45f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => GameAudio.PlaySfx("ui_click"));
        btn.onClick.AddListener(onClick);

        var t = Text(img.transform, "Label", label, 34, TextAlignmentOptions.Center);
        Rect(t.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return btn;
    }

    public static Slider Slider(Transform parent, string label, Vector2 pos, float min, float max, float value, UnityAction<float> onChange)
    {
        var root = new GameObject("Slider_" + label, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Rect(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(620, 50));

        var lbl = Text(root.transform, "Label", label, 28, TextAlignmentOptions.MidlineLeft);
        Rect(lbl.gameObject, new Vector2(0, 0), new Vector2(0.4f, 1), Vector2.zero, Vector2.zero);

        var bg = Panel(root.transform, "Background", new Color(1, 1, 1, 0.15f));
        var bgRt = Rect(bg.gameObject, new Vector2(0.42f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        Rect(fillArea, new Vector2(0.42f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
        var fill = Panel(fillArea.transform, "Fill", new Color(0.85f, 0.9f, 1f, 0.8f));
        Rect(fill.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        Rect(handleArea, new Vector2(0.42f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var handle = Panel(handleArea.transform, "Handle", Color.white);
        Rect(handle.gameObject, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(24, 0));

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(onChange);
        bgRt.SetAsFirstSibling();
        return slider;
    }
}
