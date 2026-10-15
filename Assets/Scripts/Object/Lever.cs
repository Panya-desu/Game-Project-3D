using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// คันโยก 1 อัน ใช้คู่กับ LeverPuzzle
/// กด E เพื่อดึง แล้วจะแจ้ง LeverPuzzle ว่าดึงคันนี้
///
/// วิธีใช้:
/// 1. วาง script นี้บน object คันโยก (ต้องมี Collider)
/// 2. ลาก Animator ของคันโยก (Lever.controller) ใส่ช่อง animator
/// 3. ลาก RawImage ที่แสดงรูปบนคันโยกใส่ช่อง symbolImage
/// </summary>
public class Lever : Interactable
{
    [Header("Visual")]
    [Tooltip("Animator ของคันโยก (ใช้ bool isTurn)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string turnParameter = "isTurn";
    [Tooltip("RawImage ที่แสดงรูปบนคันโยก (รูปจะถูกสุ่มโดย LeverPuzzle)")]
    [SerializeField] private RawImage symbolImage;

    [Header("Text")]
    [SerializeField] private string pullText = "press E to pull";
    [Tooltip("ข้อความเมื่อยังวาง fuse ไม่ครบ")]
    [SerializeField] private string noPowerText = "No power...";

    [HideInInspector] public LeverPuzzle puzzle;
    [HideInInspector] public int symbolIndex;

    public bool IsPulled { get; private set; }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    private void Start()
    {
        interactText = pullText;
    }

    private void Update()
    {
        // อัพเดท text ตลอดเวลา เพื่อเปลี่ยนข้อความเมื่อวาง fuse ครบ
        interactText = IsPowered() ? pullText : noPowerText;
    }

    private bool IsPowered()
    {
        return puzzle == null || puzzle.IsPowered;
    }

    public void SetSymbol(int index, Texture texture)
    {
        symbolIndex = index;

        if (symbolImage != null)
        {
            symbolImage.texture = texture;
        }
        else
        {
            Debug.LogWarning($"[Lever] {name}: symbolImage is not assigned!");
        }
    }

    public override void Interact()
    {
        if (!canInteract || IsPulled) return;

        if (!IsPowered())
        {
            Debug.Log($"[Lever] {name}: No power, place all fuses first");
            GameAudio.PlaySfx("door_locked", 0.6f);
            return;
        }

        IsPulled = true;
        canInteract = false;
        SetTurn(true);
        GameAudio.PlaySfx("lever_pull");

        if (puzzle != null)
        {
            puzzle.OnLeverPulled(this);
        }
    }

    /// <summary>
    /// ตั้งเป็นดึงแล้ว โดยไม่แจ้ง LeverPuzzle (ใช้ตอนโหลด save)
    /// </summary>
    public void SetPulled()
    {
        IsPulled = true;
        canInteract = false;
        SetTurn(true);
    }

    /// <summary>
    /// ดันคันโยกกลับขึ้น (เรียกจาก LeverPuzzle ตอนดึงผิด)
    /// </summary>
    public void ResetLever()
    {
        IsPulled = false;
        canInteract = true;
        SetTurn(false);
    }

    /// <summary>
    /// ล็อกคันโยกไว้ (เรียกจาก LeverPuzzle ตอนไขปริศนาสำเร็จ หรือระหว่างรอ reset)
    /// </summary>
    public void Lock()
    {
        canInteract = false;
    }

    private void SetTurn(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(turnParameter, value);
        }
        else
        {
            Debug.LogWarning($"[Lever] {name}: animator is not assigned!");
        }
    }
}
