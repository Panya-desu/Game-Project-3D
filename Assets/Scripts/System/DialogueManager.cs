using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public TaskManager taskManager;
    public QuestManager questManager;
    private DialogueLine[] lines;
    private int currentLine;
    private bool isFirstDialogue = true;
    private bool isTalking = false;

    private void Start()
    {
        dialoguePanel.SetActive(false);
    }

    void Update()
    {
        if (isTalking && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            NextLine();
        }
    }

    public void StartDialogue(DialogueLine[] dialogueLines)
    {
        dialoguePanel.SetActive(true);

        lines = dialogueLines;
        currentLine = 0;
        isTalking = true;

        ShowLine();
    }

    void ShowLine()
    {
        nameText.text = lines[currentLine].characterName;
        dialogueText.text = lines[currentLine].text;
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
    }

    public bool IsTalking()
    {
        return isTalking;
    }
}