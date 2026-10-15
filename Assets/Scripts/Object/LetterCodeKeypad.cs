using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Keypad ที่สร้างรหัสจากตัวอักษรโดยอ้างอิง RandomLetterCodeDisplay
/// ตั้งคำที่เป็นรหัส (เช่น "EVIE") แล้วแปลงเป็นตัวเลขตามตาราง
/// เช่น ถ้า E=3, V=7, I=1 → รหัสคือ "3713"
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่ต้องการทำเป็น Keypad
/// 2. ลาก RandomLetterCodeDisplay ใส่ช่อง letterCodeDisplay
/// 3. ลาก TMP_Text จอ keypad ใส่ช่อง displayText
/// 4. ลาก InteractiveDoor ที่จะ unlock ใส่ช่อง interactiveDoor
/// 5. ตั้ง codeWord เป็นคำที่ต้องการ (default: "EVIE")
/// </summary>
public class LetterCodeKeypad : Interactable, ISaveable
{
    [Header("Code Settings")]
    [Tooltip("คำที่ใช้เป็นรหัส ตัวอักษรแต่ละตัวจะถูกแปลงเป็นตัวเลขตาม RandomLetterCodeDisplay")]
    [SerializeField] private string codeWord = "EVIE";

    [Header("References")]
    [Tooltip("RandomLetterCodeDisplay ที่ใช้แปลงตัวอักษรเป็นตัวเลข")]
    [SerializeField] private RandomLetterCodeDisplay letterCodeDisplay;
    [SerializeField] private InteractiveDoor interactiveDoor;

    [Header("Keypad Display")]
    [Tooltip("TMP_Text จอ keypad สำหรับกดรหัส")]
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private float feedbackDelay = 1.5f;
    [SerializeField] private string errorText = "ERROR";

    [Header("Debug")]
    [SerializeField] private bool showCodeInLog = true;

    private string correctCode = "";
    private string currentInput = "";
    private bool isEnteringCode = false;
    private bool isProcessingFeedback = false;

    private void Start()
    {
        interactText = "press E to enter code";

        // Ensure a collider is present so the raycast can hit it
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // สร้างรหัสจากคำ
        GenerateCodeFromWord();

        // Initialize display
        ResetDisplay();

        // ไขรหัสไปแล้วใน save
        if (SaveSystem.TryLoad(this, out KeypadSaveState state) && state.solved)
        {
            canInteract = false;
            if (displayText != null)
            {
                displayText.text = "UNLOCKED";
            }
        }
    }

    /// <summary>
    /// แปลงคำ (เช่น "EVIE") เป็นรหัสตัวเลขโดยใช้ RandomLetterCodeDisplay
    /// </summary>
    private void GenerateCodeFromWord()
    {
        if (letterCodeDisplay == null)
        {
            Debug.LogError("[LetterCodeKeypad] RandomLetterCodeDisplay is not assigned!");
            correctCode = "0000";
            return;
        }

        correctCode = letterCodeDisplay.ConvertWordToCode(codeWord);

        if (showCodeInLog)
        {
            Debug.Log("[LetterCodeKeypad] Word: " + codeWord.ToUpper() + " → Code: " + correctCode);
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new KeypadSaveState { solved = !canInteract, code = correctCode });
    }

    public override void Interact()
    {
        if (!canInteract || isEnteringCode || isProcessingFeedback) return;

        StartCodeEntry();
    }

    private void StartCodeEntry()
    {
        isEnteringCode = true;
        KeypadInteractable.IsEnteringKeypad = true;
        GameHud.SetHint("Type the code with the number keys  |  Enter = confirm  |  Backspace = delete  |  Esc = cancel");
        currentInput = "";
        UpdateDisplay();

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StopCodeEntry()
    {
        isEnteringCode = false;
        KeypadInteractable.IsEnteringKeypad = false;
        GameHud.SetHint("");
        ResetDisplay();

        // Lock cursor back to gameplay mode
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isEnteringCode || isProcessingFeedback) return;

        // Cancel with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameState.ConsumeEscape();
            StopCodeEntry();
            return;
        }

        // Capture keyboard input
        foreach (char c in Input.inputString)
        {
            if (char.IsDigit(c))
            {
                if (currentInput.Length < correctCode.Length)
                {
                    currentInput += c;
                    GameAudio.PlaySfx("keypad_beep", 0.7f, 0.05f);
                    UpdateDisplay();
                }
            }
            else if (c == '\b') // Backspace
            {
                if (currentInput.Length > 0)
                {
                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                    UpdateDisplay();
                }
            }
            else if (c == '\r' || c == '\n') // Enter key
            {
                if (currentInput.Length > 0)
                {
                    StartCoroutine(CheckCode());
                }
            }
        }
    }

    private IEnumerator CheckCode()
    {
        isProcessingFeedback = true;

        if (currentInput == correctCode)
        {
            if (displayText != null)
            {
                displayText.text = "UNLOCKED";
            }

            // Only unlock the door — player must open it themselves
            if (interactiveDoor != null)
            {
                GameAudio.PlaySfx("unlock");
                interactiveDoor.Unlock();
            }

            yield return new WaitForSeconds(feedbackDelay);

            // Turn off keypad interaction since door is unlocked
            canInteract = false;
            StopCodeEntry();

            // Keep display showing "UNLOCKED" permanently
            if (displayText != null)
            {
                displayText.text = "UNLOCKED";
            }
        }
        else
        {
            if (displayText != null)
            {
                displayText.text = errorText;
                GameAudio.PlaySfx("error");
            }

            yield return new WaitForSeconds(feedbackDelay);

            currentInput = "";
            UpdateDisplay();
            isProcessingFeedback = false;
        }
    }

    private void UpdateDisplay()
    {
        if (displayText == null) return;

        string formatted = "";
        for (int i = 0; i < correctCode.Length; i++)
        {
            if (i < currentInput.Length)
            {
                formatted += currentInput[i] + " ";
            }
            else
            {
                formatted += "_ ";
            }
        }
        displayText.text = formatted.Trim();
    }

    private void ResetDisplay()
    {
        if (displayText != null)
        {
            displayText.text = "";
            for (int i = 0; i < correctCode.Length; i++)
            {
                displayText.text += "_ ";
            }
            displayText.text = displayText.text.Trim();
        }
    }
}
