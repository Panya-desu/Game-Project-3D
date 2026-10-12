using UnityEngine;

/// <summary>
/// วางบน Object B (ของที่เก็บได้ใน scene)
/// เมื่อ player interact จะเก็บของ (บันทึก itemID) แล้ว Object B จะหายไป
/// ใช้คู่กับ PlaceItemInteractable ที่วางบน Object A
/// </summary>
public class ItemPickup : Interactable
{
    [Header("Item Settings")]
    [Tooltip("ID ของไอเทม ใช้จับคู่กับ PlaceItemInteractable")]
    [SerializeField] private string itemID = "item_default";

    [Header("UI Text")]
    [Tooltip("ข้อความที่แสดงเมื่อ player มองไอเทม")]
    [SerializeField] private string pickupText = "press E to pick up";

    /// <summary>
    /// เก็บ item ที่ player เก็บได้แล้ว (เช็คด้วย itemID)
    /// ใช้ static HashSet เพื่อให้เช็คข้าม script ได้ง่าย
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> collectedItems
        = new System.Collections.Generic.HashSet<string>();

    private void Start()
    {
        interactText = pickupText;
    }

    public override void Interact()
    {
        if (!canInteract) return;

        // เก็บไอเทม
        collectedItems.Add(itemID);
        Debug.Log("[ItemPickup] Collected item: " + itemID);

        // หายไปจาก scene
        gameObject.SetActive(false);
    }

    /// <summary>
    /// เช็คว่า player มีไอเทม ID นี้หรือยัง
    /// </summary>
    public static bool HasItem(string id)
    {
        return collectedItems.Contains(id);
    }

    /// <summary>
    /// ลบไอเทมออกจาก inventory (ใช้เมื่อวางของเรียบร้อยแล้ว)
    /// </summary>
    public static bool RemoveItem(string id)
    {
        return collectedItems.Remove(id);
    }

    /// <summary>
    /// ล้าง item ทั้งหมด (เช่น ตอนเริ่มเกมใหม่)
    /// </summary>
    public static void ClearAllItems()
    {
        collectedItems.Clear();
    }
}
