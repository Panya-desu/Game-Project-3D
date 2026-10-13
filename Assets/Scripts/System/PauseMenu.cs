using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// กด ESC เพื่อเปิด/ปิด Pause Menu
/// ปุ่ม Resume → เล่นต่อ
/// ปุ่ม Save and Exit → save เกมแล้วกลับไปหน้า Main Menu
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่ active ตลอด (เช่น Canvas) ห้ามวางบน PauseMenu เอง
/// 2. ลาก PauseMenu (panel) ใส่ช่อง pauseMenuUI
/// 3. ลากปุ่ม Resume ใส่ช่อง resumeButton และปุ่ม Back ใส่ช่อง saveExitButton
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveExitButton;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "Main";

    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool keypadWasActive = false;

    private void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(Resume);
        }

        if (saveExitButton != null)
        {
            saveExitButton.onClick.AddListener(SaveAndExit);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // ESC ตอนกดรหัส keypad ใช้สำหรับออกจาก keypad (เช็คเฟรมก่อนด้วย เพราะ keypad อาจปิดตัวเองไปแล้วในเฟรมนี้)
        if (KeypadInteractable.IsEnteringKeypad || keypadWasActive) return;

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

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
    }

    public void SaveAndExit()
    {
        SaveSystem.Save();

        IsPaused = false;
        SaveSystem.ReturnToMainMenu(mainMenuScene);
    }

    private void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}
