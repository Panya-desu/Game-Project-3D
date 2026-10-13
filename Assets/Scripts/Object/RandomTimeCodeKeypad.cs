using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Keypad ที่สร้างรหัสจากเวลาแบบสุ่ม (00:00 - 23:59)
/// เวลาจะแสดงในรูปแบบ HH:MM บน TextMeshPro (คำใบ้)
/// รหัสคือตัวเลขของเวลา เช่น 14:59 → รหัส "1459"
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่ต้องการทำเป็น Keypad
/// 2. ลาก TMP_Text ที่จะแสดงคำใบ้เวลา ใส่ช่อง hintTimeText
/// 3. ลาก TMP_Text ที่เป็นจอ keypad ใส่ช่อง displayText
/// 4. ลาก InteractiveDoor ที่จะ unlock ใส่ช่อง interactiveDoor
/// </summary>
public class RandomTimeCodeKeypad : Interactable, ISaveable
{
    [Header("Time Hint (คำใบ้เวลา)")]
    [Tooltip("TMP_Text ที่จะแสดงเวลาแบบสุ่ม เช่น 14:59")]
    [SerializeField] private TMP_Text hintTimeText;

    [Header("Keypad Settings")]
    [SerializeField] private InteractiveDoor interactiveDoor;
    [Tooltip("TMP_Text ที่เป็นจอ keypad สำหรับกดรหัส")]
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private float feedbackDelay = 1.5f;
    [SerializeField] private string errorText = "ERROR";

    [Header("Debug")]
    [Tooltip("แสดงรหัสที่ถูกต้องใน Console")]
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

        // สุ่มเวลาและสร้างรหัส
        GenerateRandomTimeCode();

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
    /// สุ่มเวลา 00:00 - 23:59 แล้วสร้างรหัสจากตัวเลข
    /// </summary>
    private void GenerateRandomTimeCode()
    {
        int hour = Random.Range(0, 24);     // 0-23
        int minute = Random.Range(0, 60);   // 0-59

        // ใช้เวลาเดิมจาก save
        if (SaveSystem.TryLoad(this, out KeypadSaveState state) && state.code != null && state.code.Length == 4)
        {
            hour = int.Parse(state.code.Substring(0, 2));
            minute = int.Parse(state.code.Substring(2, 2));
        }

        // สร้างรหัส 4 หลัก เช่น 14:59 → "1459", 02:05 → "0205"
        correctCode = hour.ToString("D2") + minute.ToString("D2");

        // แสดงเวลาในรูปแบบ HH:MM บน TMP_Text คำใบ้
        string timeDisplay = hour.ToString("D2") + ":" + minute.ToString("D2");

        if (hintTimeText != null)
        {
            hintTimeText.text = timeDisplay;
        }
        else
        {
            Debug.LogWarning("[RandomTimeCodeKeypad] hintTimeText is not assigned!");
        }

        if (showCodeInLog)
        {
            Debug.Log("[RandomTimeCodeKeypad] Generated time: " + timeDisplay + " → Code: " + correctCode);
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
