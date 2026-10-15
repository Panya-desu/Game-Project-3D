using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Starts the automated play-through (see SmokeTestDriver) from the title screen. In batch mode run without -quit:
///   Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.Run -logFile smoke.log
/// The editor exits with code 0 when every check passes.
/// </summary>
public static class SmokeTest
{
    [MenuItem("Tools/Dev/Run Smoke Test")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        SessionState.SetBool("SmokeTest", true);
        EditorApplication.EnterPlaymode();
    }
}
