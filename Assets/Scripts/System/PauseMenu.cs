using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// กด ESC เพื่อเปิด/ปิด Pause Menu (ใช้ได้ทุก scene ที่มีผู้เล่น)
/// ปุ่ม Resume → เล่นต่อ
/// ปุ่ม Save and Exit → save เกมแล้วกลับไปหน้า Main Menu
/// และมีแถบปรับเสียง / ความไวเมาส์ กับรายการปุ่มควบคุม
///
/// หน้าเมนูสร้างจากโค้ดตอนเริ่มเกม (DontDestroyOnLoad) จึงไม่ต้องวางในทุก scene
/// ถ้าใน scene มี PauseMenu เก่าวางไว้ (เช่นบน Canvas ของ Level_1) ตัวนั้นจะซ่อน panel ของตัวเองแล้วลบตัวเองทิ้ง
/// Runs late so the keypad and phone can use Esc to close themselves first.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static PauseMenu Instance { get; private set; }

    /// <summary>Multiplier applied by MouseLook (saved between sessions).</summary>
    public static float SensitivityScale
    {
        get => PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        set => PlayerPrefs.SetFloat("MouseSensitivity", Mathf.Clamp(value, 0.2f, 3f));
    }

    [Header("Old scene UI (hidden, the runtime menu is used instead)")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveExitButton;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "Main";

    private GameObject panel;
    private TextMeshProUGUI timeLeft;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool keypadWasActive = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[PauseMenu]");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenu>();
    }

    private void Awake()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        if (Instance != null && Instance != this)
        {
            // PauseMenu ที่วางไว้ใน scene: ให้ตัว runtime จัดการแทน
            Destroy(this);
            return;
        }

        Instance = this;
        IsPaused = false;
        Time.timeScale = 1f;
        Build();
        panel.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsPaused) Resume();
    }

    private void Build()
    {
        var canvas = RuntimeUI.CreateCanvas(transform, "PauseCanvas", 4000);
        panel = RuntimeUI.Panel(canvas.transform, "Dim", new Color(0.03f, 0.04f, 0.09f, 0.8f)).gameObject;
        RuntimeUI.Rect(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panelGroup = panel.AddComponent<CanvasGroup>();
        var edge = RuntimeUI.Panel(panel.transform, "Edge", new Color(0f, 0f, 0.02f, 0.85f));
        edge.sprite = UiTheme.EdgeGlow();
        edge.raycastTarget = false;
        RuntimeUI.Rect(edge.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var title = RuntimeUI.Text(panel.transform, "Title", "PAUSED", 64, TextAlignmentOptions.Center);
        UiTheme.Style(title, true, 64, UiTheme.Pearl, 0.4f);
        title.characterSpacing = 22f;
        RuntimeUI.Rect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(900, 100));
        var line = RuntimeUI.Panel(panel.transform, "TitleLine", new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.9f));
        line.sprite = UiTheme.FadeLine();
        line.raycastTarget = false;
        RuntimeUI.Rect(line.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 312), new Vector2(620, 3));

        timeLeft = RuntimeUI.Text(panel.transform, "TimeLeft", "", 28, TextAlignmentOptions.Center);
        UiTheme.Style(timeLeft, false, 28, UiTheme.Gold, 0.4f);
        timeLeft.characterSpacing = 2f;
        RuntimeUI.Rect(timeLeft.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 272), new Vector2(900, 50));

        StyleButton(RuntimeUI.Button(panel.transform, "Resume", new Vector2(0, 185), Resume));
        StyleButton(RuntimeUI.Button(panel.transform, "Save and Exit", new Vector2(0, 100), SaveAndExit));

        StyleSlider(RuntimeUI.Slider(panel.transform, "Volume", new Vector2(0, -40), 0f, 1f, GameAudio.MasterVolume, v => GameAudio.MasterVolume = v));
        StyleSlider(RuntimeUI.Slider(panel.transform, "Mouse", new Vector2(0, -105), 0.2f, 3f, SensitivityScale, v => SensitivityScale = v));

        var helpTitle = RuntimeUI.Text(panel.transform, "ControlsTitle", "CONTROLS", 22, TextAlignmentOptions.Center);
        UiTheme.Style(helpTitle, true, 22, UiTheme.Gold, 0f);
        helpTitle.characterSpacing = 14f;
        RuntimeUI.Rect(helpTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(900, 40));
        var help = RuntimeUI.Text(panel.transform, "Controls", ControlsBody, 26, TextAlignmentOptions.Center);
        UiTheme.Style(help, false, 26, new Color(UiTheme.Pearl.r, UiTheme.Pearl.g, UiTheme.Pearl.b, 0.75f), 0f);
        help.lineSpacing = 12f;
        RuntimeUI.Rect(help.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(1100, 150));
    }

    private const string ControlsBody =
        "W A S D   Walk          Space   Jump          Mouse   Look around\n" +
        "E   Interact / next line          F   Phone (Notes)          Esc   Close / Pause";

    private CanvasGroup panelGroup;
    private float openedAt;

    private static void StyleButton(Button b)
    {
        var img = b.GetComponent<Image>();
        img.sprite = UiTheme.RoundedRect(18);
        img.type = Image.Type.Sliced;
        var colors = b.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.07f);
        colors.highlightedColor = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.32f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.5f);
        b.colors = colors;
        ((RectTransform)b.transform).sizeDelta = new Vector2(440, 64);
        var label = b.GetComponentInChildren<TextMeshProUGUI>();
        UiTheme.Style(label, false, 30, UiTheme.Pearl, 0f);
        label.characterSpacing = 4f;
        // thin gold underline
        var underline = RuntimeUI.Panel(b.transform, "Underline", new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.55f));
        underline.sprite = UiTheme.FadeLine();
        underline.raycastTarget = false;
        RuntimeUI.Rect(underline.gameObject, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0, 1), new Vector2(0, 2));
    }

    private static void StyleSlider(Slider s)
    {
        foreach (var t in s.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            UiTheme.Style(t, false, 26, UiTheme.Pearl, 0f);
            t.characterSpacing = 3f;
        }
        foreach (var img in s.GetComponentsInChildren<Image>(true))
        {
            switch (img.name)
            {
                case "Background":
                    img.sprite = UiTheme.RoundedRect(6); img.type = Image.Type.Sliced;
                    img.color = new Color(1f, 1f, 1f, 0.14f);
                    break;
                case "Fill":
                    img.sprite = UiTheme.RoundedRect(6); img.type = Image.Type.Sliced;
                    img.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.9f);
                    break;
                case "Handle":
                    img.sprite = UiTheme.Diamond();
                    img.color = UiTheme.Pearl;
                    img.rectTransform.sizeDelta = new Vector2(28, 0);
                    break;
            }
        }
    }

    private void Update()
    {
        if (IsPaused) UpdateTimeLeft();
        if (IsPaused && panelGroup != null) panelGroup.alpha = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.2f);

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // ESC ตอนกดรหัส keypad ใช้สำหรับออกจาก keypad (เช็คเฟรมก่อนด้วย เพราะ keypad อาจปิดตัวเองไปแล้วในเฟรมนี้)
        if (KeypadInteractable.IsEnteringKeypad || keypadWasActive) return;
        if (GameState.EscapeConsumedThisFrame || PhoneManager.IsPhoneOpen || GameState.IsCutscene) return;
        // หน้า Main Menu / ฉากจบ ไม่มีผู้เล่น จึงไม่มี pause
        if (!IsPaused && FindAnyObjectByType<PlayerInteract>() == null) return;

        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void LateUpdate()
    {
        keypadWasActive = KeypadInteractable.IsEnteringKeypad;
    }

    // the 40-minute clock keeps running while paused, so the menu shows it live
    private void UpdateTimeLeft()
    {
        if (timeLeft == null) return;
        if (RunTimer.Running)
        {
            int s = Mathf.CeilToInt(RunTimer.Remaining);
            timeLeft.text = $"TIME LEFT   {s / 60:00}:{s % 60:00}   ·   the clock keeps running";
        }
        else
        {
            timeLeft.text = "";
        }
    }

    public void Pause()
    {
        UpdateTimeLeft();
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ปุ่มต้องมี EventSystem ถึงจะกดได้
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        panel.SetActive(true);
        openedAt = Time.unscaledTime;
        if (panelGroup != null) panelGroup.alpha = 0f;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;

        panel.SetActive(false);
    }

    public void SaveAndExit()
    {
        SaveSystem.Save();
        Unpause();
        SaveSystem.ReturnToMainMenu(mainMenuScene);
    }

    private void Unpause()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
