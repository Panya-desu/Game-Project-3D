using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The end screen of the prototype (DemoEnd), in the game's own look instead of the old grey panel:
/// a night-blue backdrop with a soft light in the middle and slow drifting motes (the White Space
/// sparkles), the closing lines fading in one after another, the run time, and the menu buttons.
/// Built over the scene's original canvas, which is hidden.
/// </summary>
public class DemoEndScreen : MonoBehaviour
{
    const string SceneName = "DemoEnd";

    RectTransform motesRoot;
    readonly System.Collections.Generic.List<(RectTransform rt, Image img, Vector2 basePos, float speed, float phase, float alpha)> motes = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName || FindAnyObjectByType<DemoEndScreen>() != null) return;
        var go = new GameObject("[DemoEndScreen]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<DemoEndScreen>();
    }

    void Start()
    {
        var old = GameObject.Find("Canvas");
        if (old != null) old.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        var canvas = RuntimeUI.CreateCanvas(transform, "EndCanvas", 100);
        var root = canvas.transform;

        var bg = RuntimeUI.Panel(root, "Backdrop", Color.white);
        bg.sprite = Gradient();
        RuntimeUI.Rect(bg.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var glow = RuntimeUI.Panel(root, "Glow", new Color(0.75f, 0.8f, 1f, 0.16f));
        glow.sprite = Glow();
        RuntimeUI.Rect(glow.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1500, 1000));
        var vign = RuntimeUI.Panel(root, "Vignette", new Color(0.01f, 0.01f, 0.04f, 0.75f));
        vign.sprite = UiTheme.EdgeGlow();
        RuntimeUI.Rect(vign.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        motesRoot = (RectTransform)new GameObject("Motes", typeof(RectTransform)).transform;
        motesRoot.SetParent(root, false);
        RuntimeUI.Rect(motesRoot.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        for (int i = 0; i < 40; i++)
        {
            var m = RuntimeUI.Panel(motesRoot, "Mote", Color.white);
            m.sprite = i % 5 == 0 ? UiTheme.Diamond() : Glow();
            m.raycastTarget = false;
            float size = Random.Range(6f, i % 5 == 0 ? 16f : 22f);
            var pos = new Vector2(Random.Range(-980f, 980f), Random.Range(-560f, 560f));
            var rt = RuntimeUI.Rect(m.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            motes.Add((rt, m, pos, Random.Range(8f, 22f), Random.Range(0f, 10f), Random.Range(0.15f, 0.55f)));
        }

        // closing lines
        var label = Line(root, "Game", "ECHO OF MEMORIES", false, 26, UiTheme.Gold, 250, 1200, 60);
        label.characterSpacing = 22f;
        var title = Line(root, "Title", "To be continued", true, 92, UiTheme.Pearl, 160, 1400, 130);
        title.characterSpacing = 6f;
        var divider = RuntimeUI.Panel(root, "Divider", UiTheme.Gold);
        divider.sprite = UiTheme.FadeLine();
        RuntimeUI.Rect(divider.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 88), new Vector2(760, 2));
        var dividerGroup = divider.gameObject.AddComponent<CanvasGroup>();
        var sub = Line(root, "Subtitle", "You were pushed out of your memories... for now.", false, 36, new Color(0.88f, 0.9f, 0.98f), 30, 1400, 60);
        var quote = Line(root, "Quote", "<i>\"Wait for me, Evie. I'm going to remember.\"</i>", false, 30, new Color(0.7f, 0.74f, 0.88f), -30, 1400, 50);

        string time = "";
        if (RunTimer.LastRunTime >= 0f)
        {
            int s = Mathf.RoundToInt(RunTimer.LastRunTime);
            time = $"You found your way out in <color=#F2D18C>{s / 60:00}:{s % 60:00}</color>";
        }
        var stat = Line(root, "Time", time, false, 28, new Color(0.8f, 0.83f, 0.92f), -110, 1200, 50);

        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(CanvasGroup));
        buttons.transform.SetParent(root, false);
        RuntimeUI.Rect(buttons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1000, 100));
        var buttonGroup = buttons.GetComponent<CanvasGroup>();
        StyleButton(RuntimeUI.Button(buttons.transform, "Main Menu", new Vector2(-170, 0), () => SaveSystem.ReturnToMainMenu("Main")));
        StyleButton(RuntimeUI.Button(buttons.transform, "Quit", new Vector2(170, 0), Application.Quit));

        var order = new CanvasGroup[]
        {
            Group(label), Group(title), dividerGroup, Group(sub), Group(quote), Group(stat), buttonGroup,
        };
        foreach (var g in order) g.alpha = 0f;
        buttonGroup.interactable = false;

        // the scene arrives through a white fade; the lines come in one by one after it
        yield return new WaitForSeconds(1.6f);
        foreach (var g in order)
        {
            StartCoroutine(Fade(g, 1.4f));
            yield return new WaitForSeconds(g == buttonGroup ? 0f : 0.55f);
        }
        buttonGroup.interactable = true;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        foreach (var m in motes)
        {
            // drift up slowly and wrap; twinkle
            float y = Mathf.Repeat(m.basePos.y + 560f + t * m.speed, 1120f) - 560f;
            float x = m.basePos.x + Mathf.Sin(t * 0.3f + m.phase) * 18f;
            m.rt.anchoredPosition = new Vector2(x, y);
            float a = m.alpha * (0.6f + 0.4f * Mathf.Sin(t * 1.3f + m.phase * 3f));
            m.img.color = new Color(0.85f, 0.9f, 1f, a);
        }
    }

    static TextMeshProUGUI Line(Transform parent, string name, string text, bool bold, float size, Color color, float y, float w, float h)
    {
        var t = RuntimeUI.Text(parent, name, text, size, TextAlignmentOptions.Center);
        UiTheme.Style(t, bold, size, color, 0.5f);
        RuntimeUI.Rect(t.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(w, h));
        return t;
    }

    static CanvasGroup Group(Component c) => c.gameObject.AddComponent<CanvasGroup>();

    static IEnumerator Fade(CanvasGroup g, float seconds)
    {
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            g.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        g.alpha = 1f;
    }

    static void StyleButton(Button b)
    {
        var rt = (RectTransform)b.transform;
        rt.sizeDelta = new Vector2(300, 66);
        var img = b.GetComponent<Image>();
        img.sprite = UiTheme.RoundedRect(20);
        img.type = Image.Type.Sliced;
        var label = b.GetComponentInChildren<TextMeshProUGUI>();
        UiTheme.Style(label, false, 30, UiTheme.Pearl, 0f);
        label.characterSpacing = 4f;
    }

    /// <summary>Night blue at the top easing into a deep ink at the bottom.</summary>
    static Sprite Gradient()
    {
        int h = 256;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color top = new Color(0.1f, 0.12f, 0.22f), mid = new Color(0.07f, 0.08f, 0.16f), bottom = new Color(0.03f, 0.035f, 0.07f);
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            tex.SetPixel(0, y, v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, mid, v * 2f));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    static Sprite glow;

    /// <summary>Soft round light (the centre glow and the motes).</summary>
    static Sprite Glow()
    {
        if (glow != null) return glow;
        int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
