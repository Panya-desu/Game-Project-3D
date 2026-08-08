using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhoneNoteApp : MonoBehaviour
{
    [Header("Note Screens")]
    [SerializeField] private GameObject notePanel;
    [SerializeField] private GameObject mainPanel;

    [Header("UI Controls")]
    [SerializeField] private TMP_InputField noteInputField;
    [SerializeField] private Button openNoteAppButton;
    [SerializeField] private Button backToMainButton;

    private const string NotePrefsKey = "PhoneNoteData";

    private void Start()
    {
        // Ensure note panel is closed at start
        if (notePanel != null)
        {
            notePanel.SetActive(false);
        }

        // Add listeners to buttons
        if (openNoteAppButton != null)
        {
            openNoteAppButton.onClick.AddListener(OpenNoteApp);
        }

        if (backToMainButton != null)
        {
            backToMainButton.onClick.AddListener(CloseNoteApp);
        }

        // Add listener to save note as they type
        if (noteInputField != null)
        {
            noteInputField.onValueChanged.AddListener(OnNoteChanged);
        }

        // Load saved notes
        LoadNote();
    }

    private void Update()
    {
        // If the phone is closed or locked, make sure the note panel is closed
        if (!PhoneManager.IsPhoneOpen || (PhoneManager.Instance != null && PhoneManager.Instance.IsLocked))
        {
            if (notePanel != null && notePanel.activeSelf)
            {
                notePanel.SetActive(false);
            }
        }
    }

    private void OpenNoteApp()
    {
        if (notePanel != null)
        {
            notePanel.SetActive(true);
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        if (noteInputField != null)
        {
            noteInputField.ActivateInputField();
        }
    }

    private void CloseNoteApp()
    {
        if (notePanel != null)
        {
            notePanel.SetActive(false);
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
    }

    private void OnNoteChanged(string text)
    {
        SaveNote(text);
    }

    private void SaveNote(string text)
    {
        PlayerPrefs.SetString(NotePrefsKey, text);
        PlayerPrefs.Save();
    }

    private void LoadNote()
    {
        if (noteInputField != null)
        {
            string savedNote = PlayerPrefs.GetString(NotePrefsKey, "");
            noteInputField.text = savedNote;
        }
    }
}
