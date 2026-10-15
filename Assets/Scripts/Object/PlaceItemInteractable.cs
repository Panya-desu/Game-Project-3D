using UnityEngine;

/// <summary>
/// วางบน จุดวางของ (เช่น ช่องใส่ fuse)
/// targetVisual คือ object ที่จะแสดง/ซ่อน (เช่น model ของ fuse ที่อยู่ในช่อง)
/// 
/// Flow:
/// 1. เริ่มเกม → ซ่อน targetVisual (ช่องว่างเปล่า)
/// 2. Player เก็บ fuse จาก ItemPickup (object อื่นใน scene)
/// 3. Player มาที่จุดวาง กด interact → แสดง targetVisual (fuse อยู่ในช่อง)
///
/// สำคัญ: ต้อง assign targetVisual ใน Inspector!
/// targetVisual ควรเป็น child object ที่เป็น model ของไอเทมที่จะวาง
/// </summary>
public class PlaceItemInteractable : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool placed;
    }

    [Header("Item Settings")]
    [Tooltip("ID ของไอเทมที่ต้องเก็บมาก่อน (ต้องตรงกับ ItemPickup)")]
    [SerializeField] private string requiredItemID = "item_default";

    [Header("Text Settings")]
    [Tooltip("ข้อความเมื่อยังไม่มีไอเทม")]
    [SerializeField] private string missingItemText = "Something is missing...";
    [Tooltip("ข้อความเมื่อมีไอเทมพร้อมวาง")]
    [SerializeField] private string placeItemText = "press E to place item";

    [Header("Options")]
    [Tooltip("ใช้ไอเทมหมดไปหลังจากวาง (ลบออกจาก inventory)")]
    [SerializeField] private bool consumeItemOnPlace = true;
    [Tooltip("ปิดการ interact หลังวางของเรียบร้อย")]
    [SerializeField] private bool disableAfterPlace = true;

    [Header("Visual Settings")]
    [Tooltip("Object ที่จะแสดง/ซ่อน (ต้องกำหนด! เช่น child model ของ fuse)")]
    [SerializeField] private GameObject targetVisual;

    private bool isPlaced = false;

    /// <summary>
    /// วางไอเทมแล้วหรือยัง (ใช้เช็คจาก script อื่น เช่น LeverPuzzle)
    /// </summary>
    public bool IsPlaced => isPlaced;

    private void Awake()
    {
        if (SaveSystem.TryLoad(this, out SaveState state) && state.placed)
        {
            isPlaced = true;
        GameAudio.PlaySfx("fuse_insert");
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteObjectiveByTarget(gameObject);
            if (disableAfterPlace)
            {
                canInteract = false;
            }
        }
    }

    private void Start()
    {
        if (targetVisual == null)
        {
            Debug.LogWarning("[PlaceItemInteractable] targetVisual ไม่ได้กำหนด! กรุณาลาก object ที่ต้องการแสดง/ซ่อนใส่ช่อง targetVisual ใน Inspector");
            return;
        }

        // ซ่อน targetVisual ตอนเริ่มเกม (จุดวางว่างเปล่า) ยกเว้นวางไว้แล้วใน save
        targetVisual.SetActive(isPlaced);

        UpdateInteractText();
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { placed = isPlaced });
    }

    public override void Interact()
    {
        if (!canInteract || isPlaced) return;

        // เช็คว่ามีไอเทมหรือยัง
        if (!ItemPickup.HasItem(requiredItemID))
        {
            Debug.Log("[PlaceItemInteractable] Player doesn't have item: " + requiredItemID);
            GameAudio.PlaySfx("door_locked", 0.6f);
            return;
        }

        // มีไอเทม → วางของ!
        isPlaced = true;
        Debug.Log("[PlaceItemInteractable] Placed item: " + requiredItemID);

        // แสดง targetVisual (fuse ปรากฏในช่อง)
        if (targetVisual != null)
        {
            targetVisual.SetActive(true);
        }

        // ใช้ไอเทมหมดไป
        if (consumeItemOnPlace)
        {
            ItemPickup.RemoveItem(requiredItemID);
        }

        if (disableAfterPlace)
        {
            canInteract = false;
        }

        UpdateInteractText();
    }

    private void UpdateInteractText()
    {
        if (isPlaced)
        {
            interactText = "";
        }
        else if (ItemPickup.HasItem(requiredItemID))
        {
            interactText = placeItemText;
        }
        else
        {
            interactText = missingItemText;
        }
    }

    private void Update()
    {
        // อัพเดท text ตลอดเวลา เพื่อเปลี่ยนข้อความเมื่อ player เก็บไอเทมได้
        if (!isPlaced)
        {
            UpdateInteractText();
        }
    }
}
