using UnityEngine;

/// <summary>
/// วางบน object กุญแจที่อยู่ใน scene
/// เมื่อ player interact จะเก็บกุญแจ (set hasKey = true) แล้วกุญแจจะหายไป
/// </summary>
public class KeyPickup : Interactable
{
    [Header("Key Settings")]
    [Tooltip("ID ของกุญแจ ใช้จับคู่กับ KeyDoorInteractable")]
    [SerializeField] private string keyID = "key_default";

    /// <summary>
    /// เก็บ key ที่ player เก็บได้แล้ว (เช็คด้วย keyID)
    /// ใช้ static HashSet เพื่อให้เช็คข้าม script ได้ง่าย
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> collectedKeys
        = new System.Collections.Generic.HashSet<string>();

    private void Start()
    {
        interactText = "press E to pick up key";
    }

    public override void Interact()
    {
        if (!canInteract) return;

        // เก็บกุญแจ
        collectedKeys.Add(keyID);
        Debug.Log("[KeyPickup] Collected key: " + keyID);

        // หายไปจาก scene
        gameObject.SetActive(false);
    }

    /// <summary>
    /// เช็คว่า player มีกุญแจ ID นี้หรือยัง
    /// </summary>
    public static bool HasKey(string id)
    {
        return collectedKeys.Contains(id);
    }

    /// <summary>
    /// ล้าง key ทั้งหมด (เช่น ตอนเริ่มเกมใหม่)
    /// </summary>
    public static void ClearAllKeys()
    {
        collectedKeys.Clear();
    }
}
