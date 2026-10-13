using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Tooltip("ปุ่ม Continue (จะกดไม่ได้ถ้ายังไม่มี save)")]
    [SerializeField] private Button continueButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (continueButton != null)
        {
            continueButton.interactable = SaveSystem.HasSave;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void StartGame()
    {
        SaveSystem.StartNewGame("WhiteSpace");
    }

    public void ContinueGame()
    {
        if (!SaveSystem.LoadGame())
        {
            Debug.LogWarning("[MainMenu] No save to continue");
        }
    }

    public void BackToMain()
    {
        SaveSystem.ReturnToMainMenu("Main");
    }

    public void ExitGame()
    {
        Debug.Log("Exit Game");
        Application.Quit();
    }
}
