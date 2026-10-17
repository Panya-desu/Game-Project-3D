using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen fade used for scene changes and dramatic moments (white flash, red hit).
/// Created automatically; call ScreenFader.LoadScene("Level_1") instead of SceneManager.LoadScene.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }
    /// <summary>True while the screen is fully covered (the start of every scene).</summary>
    public static bool Opaque => Instance != null && Instance.overlay != null && Instance.overlay.color.a > 0.95f;

    Image overlay;
    bool isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[ScreenFader]");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenFader>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // above every game UI and the pause menu

        var imgGo = new GameObject("Overlay");
        imgGo.transform.SetParent(transform, false);
        overlay = imgGo.AddComponent<Image>();
        overlay.raycastTarget = false;
        var rt = overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        overlay.color = new Color(0, 0, 0, 1);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Every scene starts black and fades in
        StopAllCoroutines();
        isLoading = false;
        Color c = overlay.color; c.a = 1f; overlay.color = c;
        StartCoroutine(Fade(c, new Color(c.r, c.g, c.b, 0f), 1.0f));
    }

    public static void LoadScene(string sceneName, float fadeSeconds = 0.8f, Color? color = null)
    {
        if (Instance == null) { SceneManager.LoadScene(sceneName); return; }
        Instance.StartLoad(sceneName, fadeSeconds, color ?? Color.black);
    }

    /// <summary>Quick colored flash (e.g. red when hit, white for a memory).</summary>
    public static void Flash(Color color, float seconds = 0.4f)
    {
        if (Instance == null || Instance.isLoading) return;
        Instance.StartCoroutine(Instance.FlashRoutine(color, seconds));
    }

    /// <summary>Fade the screen to a color and stay there (used before the ending text).</summary>
    /// <summary>Keep the screen covered (stops the scene's fade-in) until Release.</summary>
    public static void Hold()
    {
        if (Instance == null) return;
        Instance.StopAllCoroutines();
        Color c = Instance.overlay.color; c.a = 1f; Instance.overlay.color = c;
    }

    /// <summary>Fade in from a Hold, in the cover's own colour.</summary>
    public static void Release(float seconds)
    {
        if (Instance == null) return;
        Color c = Instance.overlay.color;
        Instance.StartCoroutine(Instance.Fade(c, new Color(c.r, c.g, c.b, 0f), seconds));
    }

    public static Coroutine FadeTo(Color color, float seconds)
    {
        if (Instance == null) return null;
        Color from = Instance.overlay.color;
        if (from.a <= 0.001f) from = new Color(color.r, color.g, color.b, 0f);
        return Instance.StartCoroutine(Instance.Fade(from, color, seconds));
    }

    void StartLoad(string sceneName, float seconds, Color color)
    {
        if (isLoading) return;
        isLoading = true;
        StartCoroutine(LoadRoutine(sceneName, seconds, color));
    }

    IEnumerator LoadRoutine(string sceneName, float seconds, Color color)
    {
        GameState.IsCutscene = true;
        yield return Fade(new Color(color.r, color.g, color.b, overlay.color.a), new Color(color.r, color.g, color.b, 1f), seconds);
        Time.timeScale = 1f;
        GameState.IsCutscene = false;
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FlashRoutine(Color color, float seconds)
    {
        Color on = new Color(color.r, color.g, color.b, color.a <= 0f ? 0.6f : color.a);
        Color off = new Color(color.r, color.g, color.b, 0f);
        overlay.color = on;
        yield return Fade(on, off, seconds);
    }

    IEnumerator Fade(Color from, Color to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            overlay.color = Color.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        overlay.color = to;
    }
}
