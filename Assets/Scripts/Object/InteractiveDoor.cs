using UnityEngine;
using System.Collections;

public class InteractiveDoor : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool locked;
        public bool open;
    }

    [Header("Door Lock Settings")]
    [SerializeField] private bool isLocked = true;
    [SerializeField] private string lockedText = "Door is locked";
    [SerializeField] private string openText = "press E to open";
    [SerializeField] private string closeText = "press E to close";

    [Header("References")]
    [SerializeField] private DoorController doorController;

    private bool isOpen = false;
    private bool openOnLoad = false;

    private void Awake()
    {
        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            isLocked = state.locked;
            openOnLoad = state.open;
        }
    }

    private void Start()
    {
        UpdateInteractText();

        if (openOnLoad)
        {
            StartCoroutine(OpenAfterLoad());
        }
    }

    private IEnumerator OpenAfterLoad()
    {
        // รอ 1 เฟรมให้ DoorController.Start() ปิด Animator ก่อน แล้วค่อยเปิดประตู
        yield return null;

        if (!isOpen)
        {
            ToggleDoor();
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { locked = isLocked, open = isOpen });
    }

    public override void Interact()
    {
        if (isLocked)
        {
            Debug.Log("[InteractiveDoor] Locked!");
            return;
        }

        ToggleDoor();
    }

    public void Unlock()
    {
        isLocked = false;
        UpdateInteractText();
        Debug.Log("[InteractiveDoor] Unlocked: " + gameObject.name);
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public void Lock()
    {
        isLocked = true;
        UpdateInteractText();
        Debug.Log("[InteractiveDoor] Locked: " + gameObject.name);
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        if (doorController != null)
        {
            if (isOpen)
            {
                doorController.OpenDoor();
            }
            else
            {
                doorController.CloseDoor();
            }
        }
        else
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.SetBool("isOpen", isOpen);
            }
            else
            {
                Debug.LogWarning("[InteractiveDoor] No DoorController or Animator found!");
            }
        }

        UpdateInteractText();
        Debug.Log("[InteractiveDoor] Door is now " + (isOpen ? "Open" : "Closed"));
    }

    private void UpdateInteractText()
    {
        if (isLocked)
        {
            interactText = lockedText;
        }
        else
        {
            interactText = isOpen ? closeText : openText;
        }
    }
}
