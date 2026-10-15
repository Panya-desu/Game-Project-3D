using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Look-dev captures in play mode (see LabDriver). Batch: -executeMethod Lab.Keypads (no -quit).</summary>
public static class Lab
{
    [MenuItem("Tools/Dev/Lab: Keypad Screens")]
    public static void Keypads() => Start("Assets/Scenes/Level_1.unity", "keypads");

    [MenuItem("Tools/Dev/Lab: Player Body")]
    public static void PlayerBody() => Start("Assets/Scenes/Level_1.unity", "player");

    [MenuItem("Tools/Dev/Lab: Player Body (White Space)")]
    public static void PlayerBodyWhite() => Start("Assets/Scenes/WhiteSpace.unity", "player");

    [MenuItem("Tools/Dev/Lab: Performance (Title Screen)")]
    public static void PerfTitle() => Start("Assets/Scenes/Main.unity", "perf");

    [MenuItem("Tools/Dev/Lab: Performance (Level_1)")]
    public static void PerfLevel() => Start("Assets/Scenes/Level_1.unity", "perf");

    [MenuItem("Tools/Dev/Lab: Character Spawn Cost")]
    public static void SpawnCost() => Start("Assets/Scenes/Level_1.unity", "spawn");

    [MenuItem("Tools/Dev/Lab: UI Dump (Level_1)")]
    public static void UiLevel() => Start("Assets/Scenes/Level_1.unity", "ui");

    [MenuItem("Tools/Dev/Lab: UI Dump (White Space)")]
    public static void UiWhite() => Start("Assets/Scenes/WhiteSpace.unity", "ui");

    [MenuItem("Tools/Dev/Lab: Colour Palettes")]
    public static void Palettes() => Stage("palettes");

    [MenuItem("Tools/Dev/Lab: Character")]
    public static void Character() => Stage("character");

    static void Stage(string task)
    {
        // an empty, plainly lit stage so nothing else in a level gets in the way
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var floor = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
        floor.transform.localScale = new UnityEngine.Vector3(4f, 1f, 4f);
        SessionState.SetString("LabTask", task);
        EditorApplication.EnterPlaymode();
    }

    static void Start(string scene, string task)
    {
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        SessionState.SetString("LabTask", task);
        EditorApplication.EnterPlaymode();
    }
}
