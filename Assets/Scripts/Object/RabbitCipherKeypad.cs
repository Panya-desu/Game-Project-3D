using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Keypad ปริศนา "LIKE A RABBIT, JUMP BACK AND FORTH BETWEEN THE LETTERS"
/// สุ่มรหัส 4 หลัก แล้วสร้างประโยค เช่น "thefirstisnine"
/// จากนั้นแยกตัวอักษรตำแหน่งคู่/คี่ ไปไว้คนละกระดาษ:
///   กระดาษ A (ตัวที่ 1,3,5,...): teisinn
///   กระดาษ B (ตัวที่ 2,4,6,...): hfrtsie
/// Player ต้องอ่านสลับไปมาระหว่าง 2 กระดาษ (เหมือนกระต่ายกระโดด) เพื่อได้ประโยคคืน
/// ลำดับบรรทัดจะถูกสุ่ม แต่ทั้ง 2 กระดาษใช้ลำดับเดียวกัน
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่เป็น Keypad
/// 2. ลาก TMP_Text กระดาษแผ่นที่ 1 ใส่ช่อง evenLettersText
/// 3. ลาก TMP_Text กระดาษแผ่นที่ 2 ใส่ช่อง oddLettersText
/// 4. (ไม่บังคับ) ลาก TMP_Text คำใบ้ใส่ช่อง riddleText
/// 5. ลาก TMP_Text จอ keypad ใส่ช่อง displayText
/// 6. ลาก InteractiveDoor ใส่ช่อง interactiveDoor
/// </summary>
public class RabbitCipherKeypad : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool solved;
        public string code;
        public string evenText;
        public string oddText;
    }

    private static readonly string[] NumberWords =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"
    };

    [Header("Cipher Notes (กระดาษ 2 แผ่น)")]
    [Tooltip("TMP_Text แสดงตัวอักษรตำแหน่งที่ 1,3,5,... ของแต่ละประโยค")]
    [SerializeField] private TMP_Text evenLettersText;
    [Tooltip("TMP_Text แสดงตัวอักษรตำแหน่งที่ 2,4,6,... ของแต่ละประโยค")]
    [SerializeField] private TMP_Text oddLettersText;
    [Tooltip("TMP_Text แสดงคำใบ้ (ไม่บังคับ)")]
    [SerializeField] private TMP_Text riddleText;
    [TextArea(3, 6)]
    [SerializeField] private string riddle = "LIKE A RABBIT\nJUMP BACK AND FORTH\nBETWEEN THE LETTERS\nAND THE CODE YOU WILL FIND";

    [Header("Sentence Templates (หลักที่ 1-4, {0} = ตัวเลขเป็นคำ)")]
    [SerializeField] private string[] sentenceTemplates =
    {
        "the first is {0}",
        "second is {0}",
        "third is {0}",
        "last is {0}"
    };
    [Tooltip("สุ่มลำดับบรรทัดบนกระดาษ")]
    [SerializeField] private bool shuffleLines = true;
    [Tooltip("สุ่มสลับครึ่งคู่/คี่ระหว่าง 2 กระดาษในแต่ละบรรทัด (เช่น บรรทัดนี้กระดาษ 1 ได้ตัวคี่แทน)")]
    [SerializeField] private bool randomSwapHalves = true;

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

    private SaveState loadedState;

    private void Awake()
    {
        // ใช้รหัสและข้อความเดิมจาก save
        if (SaveSystem.TryLoad(this, out loadedState) && !string.IsNullOrEmpty(loadedState.code))
        {
            correctCode = loadedState.code;
            if (evenLettersText != null) evenLettersText.text = loadedState.evenText;
            if (oddLettersText != null) oddLettersText.text = loadedState.oddText;
        }
        else
        {
            GenerateCipher();
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState
        {
            solved = !canInteract,
            code = correctCode,
            evenText = evenLettersText != null ? evenLettersText.text : "",
            oddText = oddLettersText != null ? oddLettersText.text : ""
        });
    }

    private void Start()
    {
        interactText = "press E to enter code";

        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        if (riddleText != null)
        {
            riddleText.text = riddle;
        }

        ResetDisplay();

        // ไขรหัสไปแล้วใน save
        if (loadedState != null && loadedState.solved)
        {
            canInteract = false;
            if (displayText != null)
            {
                displayText.text = "UNLOCKED";
            }
        }
    }

    /// <summary>
    /// สุ่มตัวเลขแต่ละหลัก สร้างประโยค แล้วแยกตัวอักษรคู่/คี่ลงกระดาษ 2 แผ่น
    /// </summary>
    private void GenerateCipher()
    {
        correctCode = "";
        List<string> sentences = new List<string>();

        for (int i = 0; i < sentenceTemplates.Length; i++)
        {
            int digit = Random.Range(0, 10);
            correctCode += digit.ToString();

            string sentence = string.Format(sentenceTemplates[i], NumberWords[digit]);
            sentences.Add(sentence.Replace(" ", "").ToLower());
        }

        if (shuffleLines)
        {
            // Fisher-Yates shuffle
            for (int i = sentences.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (sentences[i], sentences[j]) = (sentences[j], sentences[i]);
            }
        }

        List<string> evenLines = new List<string>();
        List<string> oddLines = new List<string>();

        foreach (string sentence in sentences)
        {
            string even = "";
            string odd = "";
            for (int i = 0; i < sentence.Length; i++)
            {
                if (i % 2 == 0) even += sentence[i];
                else odd += sentence[i];
            }

            if (randomSwapHalves && Random.value < 0.5f)
            {
                (even, odd) = (odd, even);
            }

            evenLines.Add(even);
            oddLines.Add(odd);
        }

        if (evenLettersText != null)
        {
            evenLettersText.text = string.Join("\n", evenLines);
        }
        else
        {
            Debug.LogWarning("[RabbitCipherKeypad] evenLettersText is not assigned!");
        }

        if (oddLettersText != null)
        {
            oddLettersText.text = string.Join("\n", oddLines);
        }
        else
        {
            Debug.LogWarning("[RabbitCipherKeypad] oddLettersText is not assigned!");
        }

        if (showCodeInLog)
        {
            Debug.Log($"[RabbitCipherKeypad] Code: {correctCode}\n{string.Join("\n", sentences)}");
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
        GameHud.SetHint("Type the code with the number keys  |  Enter = confirm  |  Backspace = delete  |  Esc = cancel");
        currentInput = "";
        UpdateDisplay();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StopCodeEntry()
    {
        isEnteringCode = false;
        KeypadInteractable.IsEnteringKeypad = false;
        GameHud.SetHint("");
        ResetDisplay();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isEnteringCode || isProcessingFeedback) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameState.ConsumeEscape();
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
                    GameAudio.PlaySfx("keypad_beep", 0.7f, 0.05f);
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
                GameAudio.PlaySfx("unlock");
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
