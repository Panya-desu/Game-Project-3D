using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Look-dev captures in play mode (see LabDriver). Batch: -executeMethod Lab.Keypads (no -quit).</summary>
public static class Lab
{
    [MenuItem("Tools/Dev/Lab: Keypad Screens")]
    public static void Keypads() => Start("Assets/Scenes/Level_1.unity", "keypads");

    [MenuItem("Tools/Dev/Lab: Player Body")]
    public static void PlayerBody() => Start("Assets/Scenes/Level_1.unity", "player");

    [MenuItem("Tools/Dev/Lab: Mix All Sounds (Level_1)")]
    public static void MixAll() => Start("Assets/Scenes/Level_1.unity", "mixall");

    [MenuItem("Tools/Dev/Lab: Hair Motion (White Space)")]
    public static void HairMotion() => Start("Assets/Scenes/WhiteSpace.unity", "hairmotion");

    [MenuItem("Tools/Dev/Lab: Story Cams")]
    public static void StoryCams() => Start("Assets/Scenes/Level_1.unity", "storycams");

    [MenuItem("Tools/Dev/Lab: Windows")]
    public static void Windows() => Start("Assets/Scenes/Level_1.unity", "windows");

    [MenuItem("Tools/Dev/Lab: Sun Glow")]
    public static void SunGlow() => Start("Assets/Scenes/WhiteSpace.unity", "sunglow");

    [MenuItem("Tools/Dev/Lab: Walk End (White Space)")]
    public static void WalkEndWhite() => Start("Assets/Scenes/WhiteSpace.unity", "walkend");

    [MenuItem("Tools/Dev/Lab: Arms Walk To Idle")]
    public static void ArmIdle() => Start("Assets/Scenes/WhiteSpace.unity", "armidle");

    [MenuItem("Tools/Dev/Lab: Hair Weights")]
    public static void HairWeights() => Start("Assets/Scenes/WhiteSpace.unity", "hairweights");

    [MenuItem("Tools/Dev/Lab: Hair Look")]
    public static void HairLook() => Start("Assets/Scenes/WhiteSpace.unity", "hairlook");

    [MenuItem("Tools/Dev/Lab: Walk End (Confrontation)")]
    public static void WalkEndConfront() => Start("Assets/Scenes/Confrontation.unity", "walkend");

    [MenuItem("Tools/Dev/Lab: Frame Cost (Title)")]
    public static void CostTitle() => Start("Assets/Scenes/Main.unity", "cost");

    [MenuItem("Tools/Dev/Lab: Frame Cost (Level_1)")]
    public static void CostLevel() => Start("Assets/Scenes/Level_1.unity", "cost");

    [MenuItem("Tools/Dev/Lab: Eye Shot (White Space)")]
    public static void EyeShotWhite() => Start("Assets/Scenes/WhiteSpace.unity", "eyeshot");

    [MenuItem("Tools/Dev/Lab: Eye Shot (Bedroom)")]
    public static void EyeShotBedroom() => Start("Assets/Scenes/Level_1.unity", "eyeshot");

    [MenuItem("Tools/Dev/Lab: Walk Check (White Space)")]
    public static void WalkCheck() => Start("Assets/Scenes/WhiteSpace.unity", "walkcheck");

    [MenuItem("Tools/Dev/Lab: Stop Film (White Space)")]
    public static void StopFilm() => Start("Assets/Scenes/WhiteSpace.unity", "stopfilm");

    [MenuItem("Tools/Dev/Lab: Stops (White Space)")]
    public static void Stops() => Start("Assets/Scenes/WhiteSpace.unity", "stops");

    [MenuItem("Tools/Dev/Lab: Cutscene Sounds (White Space)")]
    public static void CutSoundsWhite() => Start("Assets/Scenes/WhiteSpace.unity", "cutsounds");

    [MenuItem("Tools/Dev/Lab: Cutscene Sounds (Confrontation)")]
    public static void CutSoundsConfront() => Start("Assets/Scenes/Confrontation.unity", "cutsounds");

    [MenuItem("Tools/Dev/Lab: Stride Jump (Level_1)")]
    public static void Stride() => Start("Assets/Scenes/WhiteSpace.unity", "stride");

    [MenuItem("Tools/Dev/Lab: Ending Door (Level_1)")]
    public static void EndDoor() => Start("Assets/Scenes/Level_1.unity", "enddoor");

    [MenuItem("Tools/Dev/Lab: Mix Test (Level_1)")]
    public static void MixTest() => Start("Assets/Scenes/Level_1.unity", "mix");

    [MenuItem("Tools/Dev/Lab: Step Events (Level_1)")]
    public static void Steps() => Start("Assets/Scenes/Level_1.unity", "steps");

    [MenuItem("Tools/Dev/Lab: Reverb Test (Level_1)")]
    public static void Reverb() => Start("Assets/Scenes/Level_1.unity", "reverb");

    [MenuItem("Tools/Dev/Lab: Room Photos (Level_1)")]
    public static void Rooms() => Start("Assets/Scenes/Level_1.unity", "rooms");

    [MenuItem("Tools/Dev/Lab: Room Photos (Title)")]
    public static void RoomsTitle() => Start("Assets/Scenes/Main.unity", "rooms");

    [MenuItem("Tools/Dev/Lab: Room Photos (White Space)")]
    public static void RoomsWhite() => Start("Assets/Scenes/WhiteSpace.unity", "rooms");

    [MenuItem("Tools/Dev/Lab: Room Photos (Confrontation)")]
    public static void RoomsConfront() => Start("Assets/Scenes/Confrontation.unity", "rooms");

    [MenuItem("Tools/Dev/Lab: Wake Cutscene Face")]
    public static void WakeFace() => Start("Assets/Scenes/Level_1.unity", "wakeface");

    [MenuItem("Tools/Dev/Lab: Opening Cutscene Face (White Space)")]
    public static void WhiteFace() => Start("Assets/Scenes/WhiteSpace.unity", "wakeface");

    [MenuItem("Tools/Dev/Lab: Chin (bedroom lamps)")]
    public static void Chin() => Start("Assets/Scenes/Level_1.unity", "chin");

    [MenuItem("Tools/Dev/Lab: Player Shadow Walk")]
    public static void ShadowWalk() => Start("Assets/Scenes/Level_1.unity", "shadowwalk");

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
