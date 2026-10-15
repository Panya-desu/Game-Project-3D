using UnityEngine;
using TMPro;
using System.Collections;

public class KeypadInteractable : Interactable, ISaveable
{
    public static bool IsEnteringKeypad = false;

    [Header("Keypad Settings")]
    [SerializeField] private string correctCode = "1234";
    [SerializeField] private InteractiveDoor interactiveDoor;
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private float feedbackDelay = 1.5f;
    [SerializeField] private string errorText = "ERROR";

    private string currentInput = "";
    private bool isEnteringCode = false;
    private bool isProcessingFeedback = false;

    public string CorrectCode => correctCode;

    private void Start()
    {
        // Set default interact text
        interactText = "press E to enter code";
        
        // Ensure a collider is present so the raycast can hit it
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

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
        IsEnteringKeypad = true;
        GameHud.SetHint("Type the code with the number keys  |  Enter = confirm  |  Backspace = delete  |  Esc = cancel");
        currentInput = "";
        UpdateDisplay();
        
        // Unlock cursor to allow escaping or UI interaction if needed
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StopCodeEntry()
    {
        isEnteringCode = false;
        IsEnteringKeypad = false;
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

        // Capture keyboard numbers and special characters
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
