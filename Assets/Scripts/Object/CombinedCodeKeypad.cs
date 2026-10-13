using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Keypad ที่รวมรหัสจาก 2 แหล่ง:
/// 1. ค่าจาก RandomLetterCodeDisplay แปลงคำ (เช่น "EVIE" → 1031)
/// 2. ค่าจากตัวเลข 4 หลักที่สุ่มแสดงตามจุดต่างๆ (เช่น 2426)
/// รหัสคำตอบ = ผลบวกของทั้งสอง (เช่น 1031 + 2426 = 3457)
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่เป็น Keypad
/// 2. ลาก RandomLetterCodeDisplay ใส่ช่อง letterCodeDisplay
/// 3. ตั้ง codeWord (default: "EVIE")
/// 4. สร้าง TMP_Text 4 ตัว วางตามจุดต่างๆ แล้วลากใส่ช่อง hintDigits
/// 5. ลาก TMP_Text จอ keypad ใส่ช่อง displayText
/// 6. ลาก InteractiveDoor ใส่ช่อง interactiveDoor
/// </summary>
public class CombinedCodeKeypad : Interactable
{
    [Header("Letter Code (จากตาราง A-Z)")]
    [Tooltip("RandomLetterCodeDisplay ที่แปลงตัวอักษรเป็นตัวเลข")]
    [SerializeField] private RandomLetterCodeDisplay letterCodeDisplay;
    [Tooltip("คำที่ใช้แปลงเป็นตัวเลข (เช่น EVIE)")]
    [SerializeField] private string codeWord = "EVIE";

    [Header("Digit Hints (คำใบ้ตัวเลข 4 จุด)")]
    [Tooltip("TMP_Text 4 ตัว วางตามจุดต่างๆ แต่ละตัวแสดงตัวเลข 1 หลัก")]
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

    private void Start()
    {
        interactText = "press E to enter code";

        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // สุ่มตัวเลข 4 หลัก แสดงตามจุดต่างๆ
        // แล้วรวมกับค่าจาก EVIE เพื่อสร้างรหัส
        GenerateCombinedCode();

        ResetDisplay();
    }

    /// <summary>
    /// สร้างรหัสจาก: letterCode (EVIE) + hintCode (อ่านจาก TMP_Text ที่ RandomDigitHintKeypad สุ่มไว้แล้ว)
    /// </summary>
    private void GenerateCombinedCode()
    {
        // --- ส่วนที่ 1: แปลง EVIE เป็นตัวเลขจากตาราง ---
        int letterValue = 0;
        string letterCode = "0000";

        if (letterCodeDisplay != null)
        {
            letterCode = letterCodeDisplay.ConvertWordToCode(codeWord);
            int.TryParse(letterCode, out letterValue);
        }
        else
        {
            Debug.LogWarning("[CombinedCodeKeypad] RandomLetterCodeDisplay is not assigned!");
        }

        // --- ส่วนที่ 2: อ่านตัวเลขจาก TMP_Text ที่ RandomDigitHintKeypad สุ่มไว้แล้ว ---
        string hintCode = "";
        for (int i = 0; i < hintDigits.Length; i++)
        {
            if (hintDigits[i] != null)
            {
                hintCode += hintDigits[i].text.Trim();
            }
            else
            {
                Debug.LogWarning($"[CombinedCodeKeypad] hintDigits[{i}] is not assigned!");
                hintCode += "0";
            }
        }

        int hintValue = 0;
        int.TryParse(hintCode, out hintValue);

        // --- รวมผลบวก (รองรับได้ถึง 5 หลัก) ---
        int combined = letterValue + hintValue;
        correctCode = combined.ToString();

        if (showCodeInLog)
        {
            Debug.Log($"[CombinedCodeKeypad] {codeWord.ToUpper()} = {letterCode} + Hints = {hintCode} → Code: {letterValue} + {hintValue} = {correctCode}");
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StopCodeEntry()
    {
        isEnteringCode = false;
        KeypadInteractable.IsEnteringKeypad = false;
        ResetDisplay();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isEnteringCode || isProcessingFeedback) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopCodeEntry();
            return;
        }

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
            else if (c == '\b')
            {
                if (currentInput.Length > 0)
                {
                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                    UpdateDisplay();
                }
            }
            else if (c == '\r' || c == '\n')
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

            if (interactiveDoor != null)
            {
                interactiveDoor.Unlock();
            }

            yield return new WaitForSeconds(feedbackDelay);

            canInteract = false;
            StopCodeEntry();

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
