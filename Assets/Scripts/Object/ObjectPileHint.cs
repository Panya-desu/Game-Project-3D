using UnityEngine;

/// <summary>
/// วางบน parent object ของแต่ละ "กอง"
/// ตอนเริ่มเกมจะสุ่มเปิด child objects จำนวน 0-9 ชิ้น เป็นคำใบ้ให้ player นับ
/// ใช้คู่กับ RandomCodeKeypad เพื่อสร้างรหัสผ่านแบบสุ่ม
/// 
/// วิธีใช้:
/// 1. สร้าง Empty GameObject เป็น parent ของกอง
/// 2. วาง object ที่จะเป็นคำใบ้เป็น child (แนะนำ 9 ชิ้น)
/// 3. Add Component "ObjectPileHint" ลงบน parent
/// 4. Script จะสุ่มเปิด 0-9 ชิ้นจาก child ที่มี
/// </summary>
public class ObjectPileHint : MonoBehaviour
{
    [Header("Pile Settings")]
    [Tooltip("จำนวนสูงสุดที่สุ่มได้ (0 ถึง maxCount) ค่าเริ่มต้น 9")]
    [Range(1, 9)]
    [SerializeField] private int maxCount = 9;

    [Tooltip("กำหนดจำนวนเอง (ถ้า >= 0 จะไม่สุ่ม ใช้ค่านี้แทน) ตั้ง -1 เพื่อให้สุ่ม")]
    [SerializeField] private int overrideCount = -1;

    /// <summary>
    /// จำนวน object ที่แสดงอยู่ในกองนี้ (ใช้เป็นตัวเลขรหัส)
    /// </summary>
    public int VisibleCount { get; private set; }

    private void Awake()
    {
        // สุ่มจำนวนหรือใช้ค่าที่กำหนด
        if (overrideCount >= 0)
        {
            VisibleCount = Mathf.Clamp(overrideCount, 0, maxCount);
        }
        else
        {
            VisibleCount = Random.Range(0, maxCount + 1); // 0 ถึง maxCount (inclusive)
        }

        ApplyVisibility();
    }

    /// <summary>
    /// เปิด/ปิด child objects ตามจำนวนที่สุ่มได้
    /// </summary>
    private void ApplyVisibility()
    {
        int childCount = transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (i < VisibleCount)
            {
                child.gameObject.SetActive(true);
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        // ถ้าจำนวนที่สุ่มเกินจำนวน child จริง → clamp ลง
        if (VisibleCount > childCount)
        {
            Debug.LogWarning($"[ObjectPileHint] {gameObject.name}: VisibleCount ({VisibleCount}) > child count ({childCount}). Clamped to {childCount}.");
            VisibleCount = childCount;
        }

        Debug.Log($"[ObjectPileHint] {gameObject.name}: Showing {VisibleCount} objects");
    }
}
