using UnityEngine;

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

    public enum DoorSound { Auto, Wood, Cabinet, Metal }

    [Header("Sound")]
    [Tooltip("Auto = ดูจากชื่อ Animator Controller (ตู้/ชั้นวาง = Cabinet, ตู้ไฟ = Metal, นอกนั้น = Wood)")]
    [SerializeField] private DoorSound sound = DoorSound.Auto;
    [Tooltip("หน่วงเสียงกระแทกหลังบานถึงวงกบ (วินาที) ถ้ายังได้ยินก่อนเห็นให้เพิ่มค่านี้")]
    [SerializeField] private float slamDelay = 0.03f;

    private bool isOpen = false;

    private void Awake()
    {
        if (doorController == null)
        {
            doorController = GetComponentInChildren<DoorController>();
        }
        if (doorController == null && GetComponentInChildren<Animator>() != null)
        {
            doorController = gameObject.AddComponent<DoorController>();
        }

        if (sound == DoorSound.Auto)
        {
            sound = DetectSound();
        }

        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            isLocked = state.locked;
            isOpen = state.open;
        }
    }

    private void Start()
    {
        // ประตูที่เปิดค้างไว้ใน save ให้อยู่ในท่าเปิดทันที ไม่ต้องเล่นแอนิเมชัน
        if (doorController != null)
        {
            doorController.SetOpenImmediate(isOpen);
            doorController.Closed += OnDoorClosed;
        }

        UpdateInteractText();
    }

    private string SoundPrefix => sound == DoorSound.Cabinet ? "cabinet" : sound == DoorSound.Metal ? "metal" : "door";

    private void OnDoorClosed()
    {
        StartCoroutine(PlaySlam());
    }

    // หน่วงเสียงกระแทกเล็กน้อยให้ตรงกับภาพที่บานกระแทกวงกบ (ถ้ากดเปิดกลับระหว่างรอ จะไม่เล่น)
    private System.Collections.IEnumerator PlaySlam()
    {
        yield return new WaitForSeconds(slamDelay);
        if (!isOpen) GameAudio.PlaySfx(SoundPrefix + "_close");
    }

    private void OnDestroy()
    {
        if (doorController != null) doorController.Closed -= OnDoorClosed;
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
            GameAudio.PlaySfx("door_locked");
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
        // ตอนปิด: เสียงบานพับเล่นทันที ส่วนเสียงกระแทกเล่นตอนบานถึงวงกบจริง (OnDoorClosed)
        GameAudio.PlaySfx(SoundPrefix + (isOpen ? "_open" : "_creak"));
        // DoorController หมุนต่อจากมุมปัจจุบัน ถ้ากดกลับระหว่างที่ยังหมุนไม่เสร็จ ประตูจะค่อยๆ หมุนกลับ
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
            Debug.LogWarning("[InteractiveDoor] No DoorController or Animator found!");
        }

        UpdateInteractText();
        Debug.Log("[InteractiveDoor] Door is now " + (isOpen ? "Open" : "Closed"));
    }

    private DoorSound DetectSound()
    {
        Animator anim = GetComponentInChildren<Animator>();
        string id = (anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "") + " " + name;
        id = id.ToLowerInvariant();
        if (id.Contains("fuse") || id.Contains("pivot")) return DoorSound.Metal;
        if (id.Contains("shelf") || id.Contains("realdoor") || id.Contains("cabinet")) return DoorSound.Cabinet;
        return DoorSound.Wood;
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
