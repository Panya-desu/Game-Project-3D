using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ปริศนาคันโยก 4 อัน ต้องดึงตามลำดับรูปให้ถูก ประตูถึงจะโผล่ออกมา
/// ลำดับคำตอบ = ลำดับของรูปในช่อง symbolsInOrder (เช่น พระอาทิตย์ → พระจันทร์ → ดาว → พระอาทิตย์ขึ้น)
/// ตอนเริ่มเกม รูปจะถูกสุ่มติดบนคันโยกแต่ละอัน ผู้เล่นต้องดูรูป ไม่ใช่ตำแหน่ง
///
/// วิธีใช้:
/// 1. สร้าง Empty object วาง script นี้
/// 2. ลาก Lever ทั้ง 4 อันใส่ช่อง levers
/// 3. ใส่รูป (Texture) ในช่อง symbolsInOrder เรียงตามคำตอบ
/// 4. ลาก object ประตูใส่ช่อง doorObject (จะถูกซ่อนตอนเริ่มเกม)
/// </summary>
public class LeverPuzzle : MonoBehaviour, ISaveable
{
    [Header("Levers")]
    [SerializeField] private Lever[] levers = new Lever[4];

    [Header("Symbols (เรียงตามลำดับคำตอบ)")]
    [Tooltip("รูปที่ 0 = ต้องดึงก่อน, รูปสุดท้าย = ดึงหลังสุด")]
    [SerializeField] private Texture[] symbolsInOrder = new Texture[4];
    [Tooltip("สุ่มรูปบนคันโยกตอนเริ่มเกม")]
    [SerializeField] private bool shuffleSymbols = true;

    [Header("Power (Fuse)")]
    [Tooltip("ช่องใส่ fuse ทั้งหมดที่ต้องวางครบก่อนถึงจะดึงคันโยกได้ (เว้นว่าง = ดึงได้เลย)")]
    [SerializeField] private PlaceItemInteractable[] requiredFuses;

    [Header("Result")]
    [Tooltip("Object ประตูที่จะโผล่ออกมาเมื่อดึงถูกครบ")]
    [SerializeField] private GameObject doorObject;
    [Tooltip("เวลารอก่อนคันโยกเด้งกลับเมื่อดึงผิด")]
    [SerializeField] private float resetDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool showAnswerInLog = true;

    [System.Serializable]
    private class SaveState
    {
        public int[] order;
        public bool solved;
    }

    private int nextStep = 0;
    private bool solved = false;

    public bool IsSolved => solved;
    private int[] symbolOrder;

    private void Start()
    {
        if (doorObject != null)
        {
            doorObject.SetActive(false);
        }

        SaveSystem.TryLoad(this, out SaveState state);

        AssignSymbols(state != null ? state.order : null);

        if (state != null && state.solved)
        {
            foreach (Lever lever in levers)
            {
                if (lever != null) lever.SetPulled();
            }
            Solve();
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { order = symbolOrder, solved = solved });
    }

    /// <summary>
    /// สุ่มว่าคันโยกไหนได้รูปอะไร (ถ้ามี savedOrder จาก save จะใช้ลำดับนั้นแทน)
    /// </summary>
    private void AssignSymbols(int[] savedOrder)
    {
        if (levers.Length != symbolsInOrder.Length)
        {
            Debug.LogWarning("[LeverPuzzle] จำนวน levers กับ symbolsInOrder ไม่เท่ากัน!");
        }

        List<int> order = new List<int>();
        bool useSaved = savedOrder != null && savedOrder.Length == symbolsInOrder.Length;

        if (useSaved)
        {
            order.AddRange(savedOrder);
        }
        else
        {
            for (int i = 0; i < symbolsInOrder.Length; i++)
            {
                order.Add(i);
            }
        }

        symbolOrder = order.ToArray();

        if (shuffleSymbols && !useSaved)
        {
            // Fisher-Yates shuffle
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            symbolOrder = order.ToArray();
        }

        string log = "";
        for (int i = 0; i < levers.Length && i < order.Count; i++)
        {
            if (levers[i] == null)
            {
                Debug.LogWarning($"[LeverPuzzle] levers[{i}] is not assigned!");
                continue;
            }

            int symbol = order[i];
            levers[i].puzzle = this;
            levers[i].SetSymbol(symbol, symbolsInOrder[symbol]);

            string textureName = symbolsInOrder[symbol] != null ? symbolsInOrder[symbol].name : "none";
            log += $"\n  {levers[i].name}: {textureName} (ลำดับที่ {symbol + 1})";
        }

        if (showAnswerInLog)
        {
            Debug.Log("[LeverPuzzle] Symbols:" + log);
        }
    }

    /// <summary>
    /// วาง fuse ครบทุกช่องแล้วหรือยัง
    /// </summary>
    public bool IsPowered
    {
        get
        {
            if (requiredFuses == null) return true;

            foreach (PlaceItemInteractable fuse in requiredFuses)
            {
                if (fuse != null && !fuse.IsPlaced) return false;
            }
            return true;
        }
    }

    public void OnLeverPulled(Lever lever)
    {
        if (solved) return;

        if (lever.symbolIndex == nextStep)
        {
            nextStep++;

            if (nextStep >= symbolsInOrder.Length)
            {
                Solve();
            }
        }
        else
        {
            StartCoroutine(ResetAll());
        }
    }

    private void Solve()
    {
        solved = true;

        foreach (Lever lever in levers)
        {
            if (lever != null) lever.Lock();
        }

        if (doorObject != null)
        {
            doorObject.SetActive(true);
        }

        if (Time.timeSinceLevelLoad > 1f) GameAudio.PlaySfx("puzzle_solved");
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteObjectiveByTarget(gameObject);
        Debug.Log("[LeverPuzzle] Solved!");
    }

    private IEnumerator ResetAll()
    {
        nextStep = 0;
        GameAudio.PlaySfx("error");

        // ล็อกทุกอันระหว่างรอ กันผู้เล่นกดต่อ
        foreach (Lever lever in levers)
        {
            if (lever != null) lever.Lock();
        }

        yield return new WaitForSeconds(resetDelay);

        foreach (Lever lever in levers)
        {
            if (lever != null) lever.ResetLever();
        }
    }
}
