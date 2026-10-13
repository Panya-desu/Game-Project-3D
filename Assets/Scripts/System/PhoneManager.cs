using System.Collections;
using TMPro;
using UnityEngine;

public class PhoneManager : MonoBehaviour, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool hasPhone;
        public bool isLocked;
    }

    public static PhoneManager Instance { get; private set; }

    [Header("Phone UI & Animation")]
    [SerializeField] private GameObject phonePanel;
    [SerializeField] private Animator phoneAnimator;
    [SerializeField] private float closeAnimationDuration = 0.5f;

    [Header("Notifications")]
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private float notificationDuration = 2.0f;

    [Header("Lock System")]
    [SerializeField] private bool isLocked = true;
    [SerializeField] private string lockPassword = "1234";
    [SerializeField] private GameObject lockScreenPanel;
    [SerializeField] private GameObject mainScreenPanel;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_Text lockFeedbackText;

    [Header("Time & Date Settings")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text dateText;

    private bool hasPhone = false;
    private bool isPhoneOpen = false;
    private Coroutine notificationCoroutine;

    public static bool IsPhoneOpen => Instance != null && Instance.isPhoneOpen;
    public bool IsLocked => isLocked;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            hasPhone = state.hasPhone;
            isLocked = state.isLocked;
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { hasPhone = hasPhone, isLocked = isLocked });
    }

    private void Start()
    {
        // Ensure closeAnimationDuration matches the actual PhoneDown clip duration (1.0s)
        closeAnimationDuration = 1.0f;

        // Disable the Animator so no animation plays at start (same pattern as DoorController)
        if (phoneAnimator != null)
        {
            phoneAnimator.enabled = false;
        }

        // Also hide the phone panel entirely
        if (phonePanel != null)
        {
            phonePanel.SetActive(false);
        }

        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }

        if (passwordInputField != null)
        {
            passwordInputField.onSubmit.AddListener(OnPasswordSubmitted);
            passwordInputField.onValueChanged.AddListener(OnPasswordValueChanged);
        }
    }

    private void Update()
    {
        // Don't allow using phone during dialogue or keypad code entries
        if (KeypadInteractable.IsEnteringKeypad || PauseMenu.IsPaused)
            return;

        if (isPhoneOpen)
        {
            UpdateTimeAndDate();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (hasPhone)
            {
                TogglePhone();
            }
            else
            {
                ShowNotification("dont have phone");
            }
        }
    }

    public void CollectPhone()
    {
        hasPhone = true;
        ShowNotification("Collected Phone! Press F to use.");
    }

    private void TogglePhone()
    {
        if (!isPhoneOpen)
        {
            OpenPhone();
        }
        else
        {
            ClosePhone();
        }
    }

    private void OpenPhone()
    {
        isPhoneOpen = true;

        // Show the panel and enable the Animator, then trigger the open animation
        if (phonePanel != null)
        {
            phonePanel.SetActive(true);
        }
        if (phoneAnimator != null)
        {
            phoneAnimator.enabled = true;
            phoneAnimator.SetBool("isPhone", true);
        }

        UpdateScreenState();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ClosePhone()
    {
        isPhoneOpen = false;

        // Trigger the close animation
        if (phoneAnimator != null)
        {
            phoneAnimator.enabled = true;
            phoneAnimator.SetBool("isPhone", false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Wait for close animation to finish, then hide the panel and disable the Animator
        StartCoroutine(HidePanelAfterClose());
    }

    private IEnumerator HidePanelAfterClose()
    {
        yield return new WaitForSeconds(closeAnimationDuration);

        // Only hide if still closed (player didn't re-open during the delay)
        if (!isPhoneOpen)
        {
            if (phonePanel != null)
            {
                phonePanel.SetActive(false);
            }
            if (phoneAnimator != null)
            {
                phoneAnimator.enabled = false;
            }
        }
    }

    public void ShowNotification(string message)
    {
        if (notificationText == null) return;

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        notificationCoroutine = StartCoroutine(ShowNotificationCR(message));
    }

    private IEnumerator ShowNotificationCR(string message)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.gameObject.SetActive(false);
    }

    private void UpdateScreenState()
    {
        if (isLocked)
        {
            if (lockScreenPanel != null) lockScreenPanel.SetActive(true);
            if (mainScreenPanel != null) mainScreenPanel.SetActive(false);
            if (passwordInputField != null)
            {
                passwordInputField.text = "";
                passwordInputField.ActivateInputField();
            }
            if (lockFeedbackText != null)
            {
                lockFeedbackText.text = "";
            }
        }
        else
        {
            if (lockScreenPanel != null) lockScreenPanel.SetActive(false);
            if (mainScreenPanel != null) mainScreenPanel.SetActive(true);
        }
    }

    private void OnPasswordSubmitted(string input)
    {
        if (!isPhoneOpen || !isLocked) return;

        if (input == lockPassword)
        {
            isLocked = false;
            ShowNotification("Phone unlocked");
            UpdateScreenState();
        }
        else
        {
            if (lockFeedbackText != null)
            {
                lockFeedbackText.text = "Incorrect Password";
            }
            else
            {
                ShowNotification("incorrect password");
            }

            if (passwordInputField != null)
            {
                passwordInputField.text = "";
                passwordInputField.ActivateInputField(); // Refocus
            }
        }
    }

    private void OnPasswordValueChanged(string text)
    {
        if (!string.IsNullOrEmpty(text) && lockFeedbackText != null)
        {
            lockFeedbackText.text = "";
        }
    }

    private void UpdateTimeAndDate()
    {
        if (timeText != null)
        {
            timeText.text = System.DateTime.Now.ToString("HH:mm");
        }
        if (dateText != null)
        {
            dateText.text = System.DateTime.Now.ToString("dd/MM/yy");
        }
    }
}
