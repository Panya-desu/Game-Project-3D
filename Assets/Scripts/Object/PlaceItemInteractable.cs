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
public class PlaceItemInteractable : Interactable
{
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

    private void Start()
    {
        if (targetVisual == null)
        {
            Debug.LogWarning("[PlaceItemInteractable] targetVisual ไม่ได้กำหนด! กรุณาลาก object ที่ต้องการแสดง/ซ่อนใส่ช่อง targetVisual ใน Inspector");
            return;
        }

        // ซ่อน targetVisual ตอนเริ่มเกม (จุดวางว่างเปล่า)
        targetVisual.SetActive(false);

        UpdateInteractText();
    }

    public override void Interact()
    {
        if (!canInteract || isPlaced) return;

        // เช็คว่ามีไอเทมหรือยัง
        if (!ItemPickup.HasItem(requiredItemID))
        {
            Debug.Log("[PlaceItemInteractable] Player doesn't have item: " + requiredItemID);
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
