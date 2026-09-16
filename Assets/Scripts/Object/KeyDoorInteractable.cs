using UnityEngine;

/// <summary>
/// วางบน object ที่ต้องใช้กุญแจเปิด (เช่น ประตู)
/// เมื่อ interact จะเช็คว่า player มีกุญแจที่ตรง keyID หรือยัง
/// ถ้ามี → unlock ประตู (InteractiveDoor) แล้วเปิดอัตโนมัติ
/// ถ้ายังไม่มี → แสดงข้อความ locked
/// </summary>
public class KeyDoorInteractable : Interactable
{
    [Header("Key Settings")]
    [Tooltip("ID ของกุญแจที่ต้องใช้ (ต้องตรงกับ KeyPickup)")]
    [SerializeField] private string requiredKeyID = "key_default";

    [Header("Text Settings")]
    [SerializeField] private string lockedText = "Need a key to open";
    [SerializeField] private string unlockText = "press E to unlock";

    [Header("References")]
    [Tooltip("InteractiveDoor ที่อยู่บน object เดียวกัน หรือ object อื่น")]
    [SerializeField] private InteractiveDoor targetDoor;

    [Header("Options")]
    [SerializeField] private bool consumeKeyOnUse = true;
    [SerializeField] private bool disableAfterUnlock = true;

    private bool isUnlocked = false;

    private void Start()
    {
        // ถ้าไม่ได้ assign ใน Inspector ลอง GetComponent จาก object เดียวกัน
        if (targetDoor == null)
        {
            targetDoor = GetComponent<InteractiveDoor>();
        }

        UpdateInteractText();
    }

    public override void Interact()
    {
        if (!canInteract || isUnlocked) return;

        // เช็คว่ามีกุญแจหรือยัง
        if (!KeyPickup.HasKey(requiredKeyID))
        {
            Debug.Log("[KeyDoorInteractable] Player doesn't have key: " + requiredKeyID);
            return;
        }

        // มีกุญแจ → unlock!
        isUnlocked = true;
        Debug.Log("[KeyDoorInteractable] Unlocked with key: " + requiredKeyID);

        // Unlock ประตู
        if (targetDoor != null)
        {
            targetDoor.Unlock();
            // เปิดประตูอัตโนมัติหลัง unlock
            targetDoor.Interact();
        }
        else
        {
            Debug.LogWarning("[KeyDoorInteractable] No InteractiveDoor assigned!");
        }

        // ถ้าต้องการใช้กุญแจแล้วหาย (consume)
        // หมายเหตุ: ตอนนี้ใช้ HasKey เช็คอย่างเดียว ถ้าต้องการ consume
        // สามารถเพิ่ม RemoveKey method ใน KeyPickup ได้

        if (disableAfterUnlock)
        {
            canInteract = false;
        }

        UpdateInteractText();
    }

    private void UpdateInteractText()
    {
        if (isUnlocked)
        {
            interactText = "";
        }
        else if (KeyPickup.HasKey(requiredKeyID))
        {
            interactText = unlockText;
        }
        else
        {
            interactText = lockedText;
        }
    }

    private void Update()
    {
        // อัพเดท text ตลอดเวลา เพื่อเปลี่ยนข้อความเมื่อ player เก็บกุญแจได้
        if (!isUnlocked)
        {
            UpdateInteractText();
        }
    }
}
