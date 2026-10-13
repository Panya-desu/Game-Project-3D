using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Keypad ที่สร้างรหัสผ่านจากจำนวน object ใน 3 กอง (ObjectPileHint)
/// รหัส = จำนวนกอง1 + จำนวนกอง2 + จำนวนกอง3 (เช่น 3, 7, 1 → "371")
/// 
/// วิธีใช้:
/// 1. สร้าง 3 กอง object โดยแต่ละกองมี ObjectPileHint
/// 2. ลาก 3 กอง ใส่ช่อง pileHints ใน Inspector
/// 3. Keypad จะอ่านจำนวนจากแต่ละกองตอนเริ่มเกมเพื่อสร้างรหัส
/// </summary>
public class RandomCodeKeypadFormPile : Interactable, ISaveable
{
    [Header("Pile Hints (คำใบ้ 3 กอง)")]
    [Tooltip("ลาก ObjectPileHint 3 กองเข้ามา ลำดับ = ลำดับตัวเลขรหัส")]
    [SerializeField] private ObjectPileHint[] pileHints = new ObjectPileHint[3];

    [Header("Keypad Settings")]
    [SerializeField] private InteractiveDoor interactiveDoor;
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

        // สร้างรหัสจากกอง
        GenerateCodeFromPiles();

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
    /// สร้างรหัสจากจำนวน object ในแต่ละกอง
    /// </summary>
    private void GenerateCodeFromPiles()
    {
        correctCode = "";

        for (int i = 0; i < pileHints.Length; i++)
        {
            if (pileHints[i] != null)
            {
                correctCode += pileHints[i].VisibleCount.ToString();
            }
            else
            {
                Debug.LogWarning($"[RandomCodeKeypadFormPile] Pile hint [{i}] is not assigned! Using 0.");
                correctCode += "0";
            }
        }

        if (showCodeInLog)
        {
            Debug.Log("[RandomCodeKeypadFormPile] Generated code: " + correctCode);
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
