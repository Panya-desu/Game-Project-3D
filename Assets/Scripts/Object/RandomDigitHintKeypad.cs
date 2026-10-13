using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Keypad ที่สุ่มรหัส 4 หลัก โดยแต่ละหลักแสดงบน TMP_Text คนละจุดใน scene
/// Player ต้องไปหาตัวเลขทั้ง 4 ตัวตามจุดต่างๆ แล้วมากดรหัสที่ keypad
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่เป็น Keypad
/// 2. สร้าง TMP_Text 4 ตัว วางตามจุดต่างๆ ใน scene
/// 3. ลาก TMP_Text ทั้ง 4 ใส่ช่อง hintDigits (ลำดับ = ลำดับรหัส)
/// 4. ลาก TMP_Text จอ keypad ใส่ช่อง displayText
/// 5. ลาก InteractiveDoor ใส่ช่อง interactiveDoor
/// </summary>
public class RandomDigitHintKeypad : Interactable
{
    [Header("Digit Hints (คำใบ้ตัวเลข 4 จุด)")]
    [Tooltip("TMP_Text 4 ตัว วางตามจุดต่างๆ แต่ละตัวแสดงตัวเลข 1 หลัก (ลำดับ = ลำดับรหัส)")]
    [SerializeField] private TMP_Text[] hintDigits = new TMP_Text[4];

    [Header("Keypad Settings")]
    [SerializeField] private InteractiveDoor interactiveDoor;
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

    private void Awake()
    {
        // สุ่มตัวเลขใน Awake() เพื่อให้ TMP_Text พร้อมก่อนที่ script อื่นจะอ่านใน Start()
        GenerateRandomDigits();
    }

    private void Start()
    {
        interactText = "press E to enter code";

        // Ensure a collider is present so the raycast can hit it
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Initialize display
        ResetDisplay();
    }

    /// <summary>
    /// สุ่มตัวเลข 0-9 ให้แต่ละหลัก แล้วแสดงบน TMP_Text แต่ละจุด
    /// </summary>
    private void GenerateRandomDigits()
    {
        correctCode = "";

        for (int i = 0; i < hintDigits.Length; i++)
        {
            int digit = Random.Range(0, 10);
            correctCode += digit.ToString();

            if (hintDigits[i] != null)
            {
                hintDigits[i].text = digit.ToString();
            }
            else
            {
                Debug.LogWarning($"[RandomDigitHintKeypad] hintDigits[{i}] is not assigned!");
            }
        }

        if (showCodeInLog)
        {
            Debug.Log("[RandomDigitHintKeypad] Generated code: " + correctCode);
        }
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
