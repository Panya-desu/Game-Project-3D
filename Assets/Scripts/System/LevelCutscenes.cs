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
        if (wasSolved) EndingDoorGlow(); // a loaded save past the levers: the door is already there

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

    bool wakeLookStarted;
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
                                    onStart: () => { if (hero != null) hero.WakeIdle(); },
                                    onUpdate: k => { if (hero != null && k > .45f && !wakeLookStarted) { wakeLookStarted = true; hero.LookAround(); } }),
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

    /// <summary>
    /// The door that appeared is a way into a memory, like the gates of the White Space: golden motes rise
    /// from its threshold, a faint warm light spills from it and a soft shimmer is heard near it.
    /// </summary>
    void EndingDoorGlow()
    {
        var ending = GameObject.Find("ToEnding");
        var door = ending != null ? ending.GetComponentInChildren<Door>(true) : null;
        if (door == null || transform.Find("EndingDoorGlow") != null) return;
        var root = new GameObject("EndingDoorGlow").transform;
        root.SetParent(transform, false);
        Vector3 at = door.transform.position;
        WhiteSpaceFlair.MakeGateMotes(root, at);
        // the light inside the doorway was plain white, which the storage room's cold grading turned blue:
        // warm gold, as the gates of the White Space. Not too bright: the storage grading exposes it up
        // (+0.9), so a strong glow, or the room's cold ceiling lights on its lit surface, burnt it out to
        // white with a cold bloom and it read blue once the door was open. Only its own glow, a deeper
        // orange gold, since the cold grading pulls it towards yellow.
        var gold = new Color(1f, 0.5f, 0.17f);
        foreach (var r in ending.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
                if (m != null && m.name.StartsWith("glowing_white"))
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", gold * 0.9f); }
                }
        var lightGo = new GameObject("DoorLight");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.position = at + Vector3.up * 1.4f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.8f, 0.55f);
        l.intensity = 2f;
        l.range = 4.5f;
        l.shadows = LightShadows.None;
        var pulse = lightGo.AddComponent<PulseGlow>();
        pulse.targetLight = l;
        var shimmer = new GameObject("DoorShimmer").AddComponent<AudioSource>();
        shimmer.transform.SetParent(root, false);
        shimmer.transform.position = at + Vector3.up * 1.4f;
        shimmer.clip = GameAudio.Mixed("shimmer", out float shimmerGain);
        doorShimmerGain = shimmerGain;
        shimmer.spatialBlend = 1f; shimmer.rolloffMode = AudioRolloffMode.Linear; shimmer.minDistance = 1.5f; shimmer.maxDistance = 10f;
        shimmer.playOnAwake = false;
        StartCoroutine(ShimmerNowAndThen(shimmer));
    }

    float doorShimmerGain = 1f;

    System.Collections.IEnumerator ShimmerNowAndThen(AudioSource s)
    {
        while (s != null)
        {
            yield return new WaitForSeconds(Random.Range(5f, 9f));
            if (s == null || s.clip == null || PauseMenu.IsPaused) continue;
            s.pitch = Random.Range(0.92f, 1.08f);
            s.volume = 0.6f * doorShimmerGain;
            s.Play();
        }
    }

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
                                        GameAudio.PlaySfx("memory_whoosh", 0.6f);
                                        GameAudio.PlaySfx("door_reveal", 0.85f); // a low tone that swells and rises: something important
                                        EndingDoorGlow();
                                    }),
        };
        CutscenePlayer.Play(shots);
    }
}
