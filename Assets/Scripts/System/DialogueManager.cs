using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool firstDialogueDone;
    }

    public GameObject dialoguePanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public TaskManager taskManager;
    public QuestManager questManager;

    [Header("Typewriter")]
    [Tooltip("Characters per second. 0 = show the whole line at once.")]
    public float charactersPerSecond = 45f;

    /// <summary>True while any DialogueManager in the scene is showing a line.</summary>
    public static bool IsAnyTalking { get; private set; }

    /// <summary>Fired every time a dialogue (one array of lines) finishes.</summary>
    public event Action DialogueEnded;

    private DialogueLine[] lines;
    private int currentLine;
    private bool isFirstDialogue = true;
    private bool isTalking = false;
    private int startedOnFrame = -1;
    private float typeStartTime;
    private bool lineFullyShown;
    private readonly Queue<DialogueLine[]> pending = new Queue<DialogueLine[]>();

    public bool FirstDialogueDone => !isFirstDialogue;

    private void Awake()
    {
        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            isFirstDialogue = !state.firstDialogueDone;
        }
    }

    private void Start()
    {
        DialogueSkin.Apply(this, dialoguePanel, nameText, dialogueText);
        dialoguePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (isTalking) IsAnyTalking = false;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { firstDialogueDone = !isFirstDialogue });
    }

    void Update()
    {
        if (!isTalking || PauseMenu.IsPaused) return;

        UpdateTypewriter();

        // Ignore the key press that opened the dialogue (pressing E on an object would otherwise skip line 1)
        if (Time.frameCount == startedOnFrame) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            if (!lineFullyShown)
            {
                // First press finishes the typewriter, second press goes to the next line
                dialogueText.maxVisibleCharacters = int.MaxValue;
                lineFullyShown = true;
            }
            else
            {
                NextLine();
            }
        }
    }

    public void StartDialogue(DialogueLine[] dialogueLines)
    {
        // Empty arrays come from the Inspector all the time (Unity serializes "no lines" as [] not null)
        if (dialogueLines == null || dialogueLines.Length == 0) return;

        if (isTalking)
        {
            // Another dialogue is on screen: play this one right after it instead of cutting it off
            pending.Enqueue(dialogueLines);
            return;
        }

        dialoguePanel.SetActive(true);

        lines = dialogueLines;
        currentLine = 0;
        isTalking = true;
        IsAnyTalking = true;
        startedOnFrame = Time.frameCount;

        ShowLine();
    }

    void ShowLine()
    {
        nameText.text = lines[currentLine].characterName;
        dialogueText.text = lines[currentLine].text;
        typeStartTime = Time.unscaledTime;
        lineFullyShown = charactersPerSecond <= 0f;
        dialogueText.maxVisibleCharacters = lineFullyShown ? int.MaxValue : 0;
        GameAudio.PlaySfx("dialogue_blip", 0.5f);
    }

    void UpdateTypewriter()
    {
        if (lineFullyShown) return;
        int visible = Mathf.FloorToInt((Time.unscaledTime - typeStartTime) * charactersPerSecond);
        dialogueText.maxVisibleCharacters = visible;
        if (visible >= dialogueText.text.Length)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            lineFullyShown = true;
        }
    }

    void NextLine()
    {
        currentLine++;

        if (currentLine >= lines.Length)
        {
            EndDialogue();
            return;
        }

        ShowLine();
    }

    void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        isTalking = false;
        IsAnyTalking = false;

        if (taskManager != null)
        {
            if (isFirstDialogue)
            {
                taskManager.StartTask();
            }
            else
            {
                taskManager.LoadNextQuest();
            }
        }

        if (questManager != null)
        {
            if (isFirstDialogue)
            {
                questManager.StartQuests();
            }
        }

        if (isFirstDialogue)
        {
            isFirstDialogue = false;
        }

        DialogueEnded?.Invoke();

        if (pending.Count > 0)
        {
            StartDialogue(pending.Dequeue());
            // Keep the queued dialogue from being skipped by the same click that closed the previous one
            startedOnFrame = Time.frameCount;
        }
    }

    /// <summary>True when the current line is fully typed (the continue marker shows).</summary>
    public bool LineFullyShown => lineFullyShown;

    public bool IsTalking()
    {
        return isTalking;
    }
}
