using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 40-minute limit for the memory house (Level_1). The clock starts when the bedroom loads and stops
/// when the player reaches the Confrontation. The clock keeps running in the pause menu (but not during
/// story cutscenes, where the player has no control), and the
/// time used is stored in the save file, so Save and Exit / Continue keeps it.
/// In the last quarter the screen edges turn red, more and more, with a heartbeat in the last minute;
/// at zero a "Time's up" screen offers Try Again (a fresh bedroom) or Main Menu.
/// </summary>
public class RunTimer : MonoBehaviour
{
    public const float LimitSeconds = 40f * 60f;
    const float WarnFrom = 0.75f;          // red edges start after 30 minutes
    const string TimedScene = "Level_1";

    public static float Elapsed { get; private set; } = -1f;
    public static float Remaining => Mathf.Max(0f, LimitSeconds - Mathf.Max(0f, Elapsed));
    public static bool Running { get; private set; }
    public static bool TimedOut { get; private set; }
    /// <summary>Time the last finished run took (shown on the end screen), -1 if none.</summary>
    public static float LastRunTime { get; private set; } = -1f;
    public static RunTimer Instance { get; private set; }

    static float restored = -1f;

    Image edge;
    TextMeshProUGUI clock;
    CanvasGroup toast;
    TextMeshProUGUI toastText;
    GameObject gameOver;
    float nextBeat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[RunTimer]");
        DontDestroyOnLoad(go);
        go.AddComponent<RunTimer>();
    }

    /// <summary>New game / back to menu: the next visit to the house starts a fresh 40 minutes.</summary>
    public static void ResetRun()
    {
        Elapsed = -1f;
        restored = -1f;
        Running = false;
        TimedOut = false;
    }

    /// <summary>Called by SaveSystem.LoadGame with the time stored in the save.</summary>
    public static void Restore(float seconds) => restored = seconds;

    /// <summary>For tests: jump the clock.</summary>
    public static void SetElapsed(float seconds) => Elapsed = seconds;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        gameOver.SetActive(false);
        if (scene.name == TimedScene)
        {
            if (restored >= 0f) { Elapsed = restored; restored = -1f; }
            if (Elapsed < 0f)
            {
                Elapsed = 0f;
                StartCoroutine(Toast("You have 40 minutes before the memories fade."));
            }
            Running = true;
            TimedOut = false;
        }
        else
        {
            if (Running && (scene.name == "Confrontation" || scene.name == "DemoEnd")) LastRunTime = Elapsed;
            Running = false; // the end screen (DemoEndScreen) shows LastRunTime
        }
    }

    void Update()
    {
        if (Running && !TimedOut && !CutscenePlayer.IsPlaying)
        {
            // real time: pausing does not stop the clock (no hiding in the pause menu to think);
            // one long frame (a scene load hitch) counts at most one second
            Elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f);
            if (Elapsed >= LimitSeconds) StartCoroutine(TimeUp());
        }

        float k = Running || TimedOut ? Mathf.InverseLerp(LimitSeconds * WarnFrom, LimitSeconds, Elapsed) : 0f;
        float pulse = k > 0.85f ? 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.5f)) : 0f;
        // a deep red vignette that leaves the middle of the screen clear (it used to wash the whole view red
        // in the last minute, hard to search the room through)
        edge.color = new Color(0.55f, 0.02f, 0.05f, Mathf.Clamp01(k * k * 0.5f + pulse * k * 0.6f));

        bool showClock = (Running || TimedOut) && !PauseMenu.IsPaused && !PhoneManager.IsPhoneOpen; // not over the phone
        clock.gameObject.SetActive(showClock);
        if (showClock)
        {
            int s = Mathf.CeilToInt(Remaining);
            clock.text = $"{s / 60:00}:{s % 60:00}";
            // pearl most of the time, turning red as the red edges come in
            clock.color = Color.Lerp(new Color(UiTheme.Pearl.r, UiTheme.Pearl.g, UiTheme.Pearl.b, 0.85f), new Color(1f, 0.45f, 0.42f), k);
        }

        if (Running && !TimedOut && Remaining <= 60f && Time.unscaledTime >= nextBeat)
        {
            GameAudio.PlaySfx("heartbeat", 0.8f);
            nextBeat = Time.unscaledTime + Mathf.Lerp(0.6f, 1.3f, Remaining / 60f);
        }
    }

    IEnumerator TimeUp()
    {
        TimedOut = true;
        Running = false;
        Elapsed = LimitSeconds;
        GameState.IsCutscene = true;
        KeypadInteractable.IsEnteringKeypad = false;
        if (PauseMenu.IsPaused && PauseMenu.Instance != null) PauseMenu.Instance.Resume();
        GameHud.SetHint("");
        GameAudio.PlaySfx("power_down");
        // the save would only lead back to an already lost run
        SaveSystem.DeleteSave();

        var group = gameOver.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        gameOver.SetActive(true);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.6f)
        {
            group.alpha = t;
            yield return null;
        }
        group.alpha = 1f;
        GameAudio.PlaySfx("memory_whoosh");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public void TryAgain()
    {
        gameOver.SetActive(false);
        SaveSystem.StartNewGame(TimedScene); // also resets the run and all runtime state
    }

    public void MainMenu()
    {
        gameOver.SetActive(false);
        SaveSystem.ReturnToMainMenu("Main");
    }

    IEnumerator Toast(string message)
    {
        yield return new WaitForSeconds(2.5f);
        toastText.text = message;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { toast.alpha = t; yield return null; }
        toast.alpha = 1f;
        yield return new WaitForSeconds(4f);
        for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 1.2f) { toast.alpha = t; yield return null; }
        toast.alpha = 0f;
    }

    // ───────────── UI ─────────────

    void Build()
    {
        var canvas = RuntimeUI.CreateCanvas(transform, "TimerCanvas", 3500);
        canvas.GetComponent<GraphicRaycaster>().enabled = true;

        edge = RuntimeUI.Panel(canvas.transform, "RedEdge", new Color(0.75f, 0.02f, 0.04f, 0f));
        edge.sprite = UiTheme.EdgeGlow();
        edge.raycastTarget = false;
        RuntimeUI.Rect(edge.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        clock = RuntimeUI.Text(canvas.transform, "Clock", "", 40, TextAlignmentOptions.TopRight);
        UiTheme.Style(clock, true, 40, UiTheme.Pearl);
        RuntimeUI.Rect(clock.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -60), new Vector2(240, 60));

        var toastGo = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
        toastGo.transform.SetParent(canvas.transform, false);
        RuntimeUI.Rect(toastGo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1100, 70));
        toast = toastGo.GetComponent<CanvasGroup>();
        toast.alpha = 0f;
        toast.blocksRaycasts = false;
        var line = RuntimeUI.Panel(toastGo.transform, "Line", UiTheme.Gold);
        line.sprite = UiTheme.FadeLine();
        RuntimeUI.Rect(line.gameObject, new Vector2(0.1f, 0f), new Vector2(0.9f, 0f), Vector2.zero, new Vector2(0, 2));
        toastText = RuntimeUI.Text(toastGo.transform, "Text", "", 32, TextAlignmentOptions.Center);
        UiTheme.Style(toastText, false, 32, UiTheme.Pearl);
        toastText.characterSpacing = 2f;
        RuntimeUI.Rect(toastText.gameObject, Vector2.zero, Vector2.one, new Vector2(0, 8), Vector2.zero);

        // game over
        gameOver = new GameObject("TimeUp", typeof(RectTransform), typeof(CanvasGroup));
        gameOver.transform.SetParent(canvas.transform, false);
        RuntimeUI.Rect(gameOver, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dim = RuntimeUI.Panel(gameOver.transform, "Dim", new Color(0.05f, 0.0f, 0.01f, 0.88f));
        RuntimeUI.Rect(dim.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var vign = RuntimeUI.Panel(gameOver.transform, "Edge", new Color(0.6f, 0.02f, 0.04f, 0.9f));
        vign.sprite = UiTheme.EdgeGlow();
        RuntimeUI.Rect(vign.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var title = RuntimeUI.Text(gameOver.transform, "Title", "TIME'S UP", 96, TextAlignmentOptions.Center);
        UiTheme.Style(title, true, 96, new Color(1f, 0.86f, 0.84f), 0.8f);
        title.characterSpacing = 18f;
        RuntimeUI.Rect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 130));
        var divider = RuntimeUI.Panel(gameOver.transform, "Divider", UiTheme.Gold);
        divider.sprite = UiTheme.FadeLine();
        RuntimeUI.Rect(divider.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 95), new Vector2(700, 2));
        var sub = RuntimeUI.Text(gameOver.transform, "Subtitle", "The memories faded before you could reach them.", 34, TextAlignmentOptions.Center);
        UiTheme.Style(sub, false, 34, new Color(0.95f, 0.88f, 0.86f));
        RuntimeUI.Rect(sub.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(1400, 60));

        StyleButton(RuntimeUI.Button(gameOver.transform, "Try Again", new Vector2(0, -80), TryAgain));
        StyleButton(RuntimeUI.Button(gameOver.transform, "Main Menu", new Vector2(0, -170), MainMenu));
        gameOver.SetActive(false);
    }

    static void StyleButton(Button b)
    {
        var img = b.GetComponent<Image>();
        img.sprite = UiTheme.RoundedRect(20);
        img.type = Image.Type.Sliced;
        var label = b.GetComponentInChildren<TextMeshProUGUI>();
        UiTheme.Style(label, false, 32, UiTheme.Pearl, 0f);
    }
}
