using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restyles the scene's dialogue box at runtime so every scene gets the same look:
/// a night-blue band that fades out at the sides, the speaker's name in gold over a fading gold line,
/// the text in pearl white with a soft shadow, letters that fade and rise in as they are typed,
/// a gentle open animation and a pulsing diamond when the line can be continued.
/// </summary>
public class DialogueSkin : MonoBehaviour
{
    DialogueManager manager;
    RectTransform panel;
    TextMeshProUGUI nameText, bodyText;
    Image marker;
    RectTransform markerRect;
    CanvasGroup group;
    float openedAt;

    public static void Apply(DialogueManager manager, GameObject panel, TextMeshProUGUI nameText, TextMeshProUGUI bodyText)
    {
        if (panel == null || panel.GetComponent<DialogueSkin>() != null) return;
        var skin = panel.AddComponent<DialogueSkin>();
        skin.manager = manager;
        skin.nameText = nameText;
        skin.bodyText = bodyText;
        skin.Build();
    }

    void Build()
    {
        panel = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) panel.SetParent(canvas.rootCanvas.transform, false); // screen-relative layout
        panel.localScale = Vector3.one; // some scenes enlarged the old box with its scale
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchorMin = new Vector2(0.06f, 0.03f);
        panel.anchorMax = new Vector2(0.94f, 0.27f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = Vector2.zero;

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.sprite = UiTheme.DialogueBand();
        bg.type = Image.Type.Simple;
        bg.color = new Color(UiTheme.Night.r, UiTheme.Night.g, UiTheme.Night.b, 0.86f);
        bg.raycastTarget = false;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var line = new GameObject("NameLine", typeof(RectTransform)).AddComponent<Image>();
        line.transform.SetParent(transform, false);
        line.sprite = UiTheme.FadeLine();
        line.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.95f);
        line.raycastTarget = false;
        // fixed thickness (not a fraction of the box) so the line never thins out to nothing in a small window
        var lr = line.rectTransform;
        lr.localScale = Vector3.one;
        lr.pivot = new Vector2(0f, 0.5f);
        lr.anchorMin = new Vector2(0.1f, 0.69f);
        lr.anchorMax = new Vector2(0.9f, 0.69f); // as wide as the text area, centred in the band
        lr.offsetMin = new Vector2(0f, -1.5f);
        lr.offsetMax = new Vector2(0f, 1.5f);

        if (nameText != null)
        {
            UiTheme.Style(nameText, true, 30, UiTheme.Gold, 0.5f);
            nameText.characterSpacing = 4f;
            nameText.alignment = TextAlignmentOptions.BottomLeft;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            Place(nameText.rectTransform, 0.11f, 0.71f, 0.7f, 0.93f);
        }
        if (bodyText != null)
        {
            UiTheme.Style(bodyText, false, 28, UiTheme.Pearl, 0.6f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 6f;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            Place(bodyText.rectTransform, 0.11f, 0.1f, 0.89f, 0.63f);
            if (bodyText.GetComponent<TypewriterFade>() == null) bodyText.gameObject.AddComponent<TypewriterFade>();
        }

        marker = new GameObject("ContinueMarker", typeof(RectTransform)).AddComponent<Image>();
        marker.transform.SetParent(transform, false);
        marker.sprite = UiTheme.Diamond();
        marker.color = UiTheme.Gold;
        marker.raycastTarget = false;
        markerRect = marker.rectTransform;
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.885f, 0.14f);
        markerRect.sizeDelta = new Vector2(18f, 18f);
        FitFonts();
    }

    static void Place(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.localScale = Vector3.one;
        var tmp = r.GetComponent<TMP_Text>();
        if (tmp != null) tmp.margin = Vector4.zero; // the old scene texts had large negative margins
        r.pivot = new Vector2(0f, 0.5f);
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // font sizes follow the box height, so the box looks the same whatever the canvas scaler does
    void OnRectTransformDimensionsChange() => FitFonts();

    void FitFonts()
    {
        if (panel == null) return;
        float h = panel.rect.height;
        if (h <= 1f) return;
        if (nameText != null) nameText.fontSize = h * 0.135f;
        if (bodyText != null) bodyText.fontSize = h * 0.125f;
        if (markerRect != null) markerRect.sizeDelta = Vector2.one * h * 0.075f;
    }

    void OnEnable()
    {
        openedAt = Time.unscaledTime;
        if (group != null) group.alpha = 0f;
        FitFonts();
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.25f);
        float e = 1f - (1f - t) * (1f - t);
        if (group != null) group.alpha = e;
        if (panel != null) panel.anchoredPosition = new Vector2(0f, (1f - e) * -14f);

        if (marker != null)
        {
            bool ready = manager == null || manager.LineFullyShown;
            float a = ready ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            marker.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, a);
            markerRect.anchoredPosition = new Vector2(0f, ready ? Mathf.Sin(Time.unscaledTime * 4f) * 3f : 0f);
        }
    }
}

/// <summary>
/// Makes each letter fade and rise in when the typewriter reveals it (works with TMP maxVisibleCharacters).
/// </summary>
public class TypewriterFade : MonoBehaviour
{
    const float FadeTime = 0.2f;
    const float Rise = 6f;

    TMP_Text text;
    float[] revealAt = new float[0];
    string lastText;
    int lastVisible = -1;
    bool animating;

    void Awake() => text = GetComponent<TMP_Text>();

    void LateUpdate()
    {
        if (text == null) return;
        if (text.text != lastText)
        {
            lastText = text.text;
            revealAt = new float[text.text.Length + 16];
            for (int i = 0; i < revealAt.Length; i++) revealAt[i] = -1f;
            lastVisible = -1;
        }
        if (!animating && text.maxVisibleCharacters == lastVisible) return;
        lastVisible = text.maxVisibleCharacters;

        text.ForceMeshUpdate();
        var info = text.textInfo;
        int visible = Mathf.Min(text.maxVisibleCharacters, info.characterCount);
        float now = Time.unscaledTime;
        animating = false;
        for (int i = 0; i < visible && i < revealAt.Length; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            if (revealAt[i] < 0f) revealAt[i] = now;
            float k = Mathf.Clamp01((now - revealAt[i]) / FadeTime);
            if (k >= 1f) continue;
            animating = true;
            int mi = ch.materialReferenceIndex, vi = ch.vertexIndex;
            var cols = info.meshInfo[mi].colors32;
            var verts = info.meshInfo[mi].vertices;
            float ease = 1f - (1f - k) * (1f - k);
            for (int v = 0; v < 4; v++)
            {
                cols[vi + v].a = (byte)(cols[vi + v].a * ease);
                verts[vi + v].y -= (1f - ease) * Rise;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
    }
}
