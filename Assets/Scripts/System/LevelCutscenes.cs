using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Story cutscenes of the memory house (Level_1):
///  1. Waking up: the protagonist is seen standing in her room, looking around, the camera finds the
///     stopped clock, then slides back into her eyes (first visit only, not when a save is loaded).
///  2. Power restored: the camera looks over the storage room while the lights flicker back on.
///  3. The door: a white flash and the camera drifts towards the door that appeared (no walk-up: the
///     player has not moved, so showing her at the door would look like a teleport back).
/// Each plays once, when its moment happens during play (a loaded save that is already past it does not replay it).
/// </summary>
public class LevelCutscenes : MonoBehaviour
{
    const string SceneName = "Level_1";

    static readonly Vector3 BedroomCentre = new Vector3(0.2f, 0f, 4.1f);

    LeverPuzzle puzzle;
    bool wasPowered, wasSolved;
    EchoCharacterView hero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName || FindAnyObjectByType<LevelCutscenes>() != null) return;
        var go = new GameObject("[LevelCutscenes]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<LevelCutscenes>();
    }

    void Start()
    {
        puzzle = FindAnyObjectByType<LeverPuzzle>();
        // the ending door: its leaf must lead through the Door behind it
        foreach (var d in FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (d.transform.root.name == "ToEnding") EnterThrough.Wire(d);
        wasPowered = puzzle != null && puzzle.IsPowered;
        wasSolved = puzzle != null && puzzle.IsSolved;

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null && !dm.FirstDialogueDone) WakeUp();
    }

    void Update()
    {
        if (hero != null) hero.SetTalking(DialogueManager.IsAnyTalking);
        if (puzzle == null) return;

        if (!wasPowered && puzzle.IsPowered)
        {
            wasPowered = true;
            PowerRestored();
        }
        if (!wasSolved && puzzle.IsSolved)
        {
            wasSolved = true;
            DoorAppears();
        }
    }

    // ───────────── 1. waking up ─────────────

    void WakeUp()
    {
        var player = FindAnyObjectByType<Movement>();
        var eye = Camera.main;
        if (player == null || eye == null) return;

        Vector3 feet = CharacterLibrary.GroundBelow(player.transform.position);
        Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        // she faces into the room, and the camera always stays on the room side (never behind a wall)
        Vector3 toRoom = Vector3.ProjectOnPlane(BedroomCentre - feet, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toRoom);
        float sc = PlayerAvatar.ModelScale; // camera heights follow her size (she is scaled to the level)
        Vector3 head = feet + Vector3.up * 1.45f * sc;

        hero = CharacterLibrary.Spawn(false, feet, toRoom);
        var heroRoot = hero != null ? hero.transform.root.gameObject : null;
        if (heroRoot != null) heroRoot.transform.position = feet;

        Vector3 clock = new Vector3(3.5f, 1.8f, -2.4f);
        var shots = new[]
        {
            // she stands in the middle of the room, looking around
            new CutscenePlayer.Shot(feet + toRoom * 2.4f - right * 0.9f + Vector3.up * 1.15f * sc,
                                    feet + toRoom * 2.8f + right * 0.7f + Vector3.up * 1.35f * sc,
                                    head - Vector3.up * 0.05f * sc, head, 4.5f,
                                    onStart: () => { if (hero != null) hero.LookAround(); }),
            // the stopped clock on the desk
            new CutscenePlayer.Shot(new Vector3(3.7f, 2.35f, 1.6f), new Vector3(3.5f, 2.05f, -0.5f),
                                    clock + Vector3.up * 0.1f, clock, 3.5f),
            // back behind her shoulder and into her eyes
            new CutscenePlayer.Shot(feet + toRoom * 1.2f + right * 1.1f + Vector3.up * 1.7f * sc, eye.transform.position,
                                    head, eye.transform.position + fwd * 3f, 2.4f,
                                    onUpdate: k => { if (heroRoot != null && k > 0.7f) heroRoot.SetActive(false); }),
        };
        CutscenePlayer.Play(shots, () =>
        {
            if (heroRoot != null) Destroy(heroRoot);
            hero = null;
        });
    }

    // ───────────── 2. power restored ─────────────

    void PowerRestored()
    {
        var shots = new[]
        {
            new CutscenePlayer.Shot(new Vector3(24.3f, 3.1f, 10.2f), new Vector3(26.4f, 3.4f, 14.2f),
                                    new Vector3(30f, 1.6f, 15f), new Vector3(32f, 1.7f, 18.5f), 4.5f),
        };
        CutscenePlayer.Play(shots);
    }

    // ───────────── 3. the door appears ─────────────

    void DoorAppears()
    {
        // only the door: she is not shown walking up to it, since the player is still standing
        // where they solved the puzzle when control comes back
        var ending = GameObject.Find("ToEnding");
        var door = ending != null ? ending.GetComponentInChildren<Door>(true) : null;
        Vector3 target = door != null ? door.transform.position + new Vector3(0.4f, 1.6f, 0f) : new Vector3(30.6f, 1.6f, 8f);

        var shots = new[]
        {
            // a flash, and a door stands where there was none; the camera slowly drifts closer
            new CutscenePlayer.Shot(target + new Vector3(-0.2f, 0.8f, 7.5f), target + new Vector3(-0.2f, 0.3f, 4.2f),
                                    target + Vector3.up * 0.4f, target, 4.5f,
                                    onStart: () =>
                                    {
                                        ScreenFader.Flash(new Color(1f, 1f, 1f, 0.9f), 0.8f);
                                        GameAudio.PlaySfx("memory_whoosh");
                                    }),
        };
        CutscenePlayer.Play(shots);
    }
}
