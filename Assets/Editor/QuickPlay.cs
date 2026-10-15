using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Opens the main menu scene and presses Play (menu: Tools/Dev/Play From Main Menu).
/// Also usable when launching the editor: Unity.exe -projectPath . -executeMethod QuickPlay.OpenMainAndPlay
/// </summary>
public static class QuickPlay
{
    [MenuItem("Tools/Dev/Play From Main Menu")]
    public static void OpenMainAndPlay()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
