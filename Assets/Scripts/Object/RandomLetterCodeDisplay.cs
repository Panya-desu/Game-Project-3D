using UnityEngine;
using TMPro;

/// <summary>
/// สุ่มตัวเลข 0-9 ให้กับตัวอักษร A-Z แล้วแสดงบน TextMeshPro
/// รูปแบบการแสดงผล:
/// A = 3    B = 7
/// C = 1    D = 9
/// ...
/// Y = 5    Z = 2
///
/// วิธีใช้:
/// 1. วาง script นี้บน object ที่มี TMP_Text หรือลาก TMP_Text ใส่ช่อง displayText
/// 2. เมื่อเริ่มเกมจะสุ่มตัวเลขให้ทุกตัวอักษรอัตโนมัติ
/// </summary>
public class RandomLetterCodeDisplay : MonoBehaviour, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public int[] digits;
    }

    [Header("Display")]
    [Tooltip("TMP_Text ที่จะแสดงตาราง A-Z = ตัวเลข")]
    [SerializeField] private TMP_Text displayText;

    [Header("Settings")]
    [Tooltip("จำนวนคอลัมน์ที่แสดง (เช่น 2 = แสดง 2 คู่ต่อบรรทัด)")]
    [SerializeField] private int columns = 2;
    [Tooltip("ช่องว่างระหว่างคอลัมน์ (จำนวน space)")]
    [SerializeField] private int columnSpacing = 4;

    [Header("Debug")]
    [SerializeField] private bool showInLog = true;

    /// <summary>
    /// เก็บตัวเลขที่สุ่มให้แต่ละตัวอักษร (index 0 = A, 1 = B, ...)
    /// ใช้ GetDigitForLetter() เพื่อดึงค่า
    /// </summary>
    private int[] letterDigits = new int[26];

    private void Awake()
    {
        if (displayText == null)
        {
            displayText = GetComponent<TMP_Text>();
        }

        // สุ่มตัวเลขใน Awake() เพื่อให้พร้อมก่อนที่ script อื่นจะเรียกใช้ใน Start()
        if (SaveSystem.TryLoad(this, out SaveState state) && state.digits != null && state.digits.Length == 26)
        {
            letterDigits = state.digits;
        }
        else
        {
            GenerateRandomDigits();
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { digits = letterDigits });
    }

    private void Start()
    {
        UpdateDisplay();
    }

    /// <summary>
    /// สุ่มตัวเลข 0-9 ให้กับตัวอักษร A-Z
    /// </summary>
    private void GenerateRandomDigits()
    {
        for (int i = 0; i < 26; i++)
        {
            letterDigits[i] = Random.Range(0, 10);
        }

        if (showInLog)
        {
            string log = "[RandomLetterCodeDisplay] Generated: ";
            for (int i = 0; i < 26; i++)
            {
                log += (char)('A' + i) + "=" + letterDigits[i] + " ";
            }
            Debug.Log(log);
        }
    }

    /// <summary>
    /// แสดงตารางบน TMP_Text
    /// </summary>
    private void UpdateDisplay()
    {
        if (displayText == null)
        {
            Debug.LogWarning("[RandomLetterCodeDisplay] No TMP_Text assigned!");
            return;
        }

        string spacing = new string(' ', columnSpacing);
        string result = "";
        int count = 0;

        for (int i = 0; i < 26; i++)
        {
            char letter = (char)('A' + i);
            result += letter + " = " + letterDigits[i];
            count++;

            if (count >= columns || i == 25)
            {
                result += "\n";
                count = 0;
            }
            else
            {
                result += spacing;
            }
        }

        displayText.text = result.TrimEnd();
    }

    /// <summary>
    /// ดึงตัวเลขที่สุ่มให้ตัวอักษรนั้น (เช่น 'A' → 3)
    /// ใช้จาก script อื่นได้ เช่น เอาไปสร้างรหัสจากคำ
    /// </summary>
    public int GetDigitForLetter(char letter)
    {
        letter = char.ToUpper(letter);
        if (letter < 'A' || letter > 'Z') return -1;
        return letterDigits[letter - 'A'];
    }

    /// <summary>
    /// แปลงคำเป็นรหัสตัวเลข เช่น "CAT" → "193" (ถ้า C=1, A=9, T=3)
    /// </summary>
    public string ConvertWordToCode(string word)
    {
        string code = "";
        foreach (char c in word.ToUpper())
        {
            int digit = GetDigitForLetter(c);
            if (digit >= 0)
            {
                code += digit.ToString();
            }
        }
        return code;
    }
}
