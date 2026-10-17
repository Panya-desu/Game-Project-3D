using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The sound of the spaces (the ambience loops are left dry, see GameAudio):
///  - the bare rooms of the storage wing echo: concrete with a long, hard echo in the fuse room and the
///    storeroom, a softer, darker one in the library where the books soak it up. Their reverb zones reach
///    every doorway and fade in over a couple of metres; while the player is in one, GameAudio adds an
///    echo voice to every sound (wet only, so the sound itself stays clear) that rings out to the end of
///    its tail. The house and the White Space keep their plain, clear sound;
///  - while the storage power is out, the fuse box crackles now and then (a positional sound, so it is
///    heard from its corner of the room and grows as the player comes close).
/// </summary>
public class AudioMood : MonoBehaviour
{
    readonly System.Collections.Generic.List<(Vector3 c, float inner, float outer, Space s)> zones = new System.Collections.Generic.List<(Vector3, float, float, Space)>();

    /// <summary>0..1, how far the listener is inside an echoing room (0 outside them all).</summary>
    public static float EchoAmount { get; private set; }
    /// <summary>The echo of that room (GameAudio's echo voices use it).</summary>
    public static Space EchoSpace { get; private set; }
    LeverPuzzle puzzle;
    AudioSource spark;
    float sparkGain = 1f;
    float nextSpark;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<AudioMood>() != null) return;
        var go = new GameObject("[AudioMood]");
        SceneManager.MoveGameObjectToScene(go, scene);
        var mood = go.AddComponent<AudioMood>();
        switch (scene.name)
        {
            case "Level_1":
                // the bare storage wing: concrete, long and hard (zones reach every doorway)
                mood.Zone("StorageSouth", new Vector3(30.2f, 2f, 5.5f), 7.5f, 9.5f, Concrete);
                mood.Zone("StorageMiddle", new Vector3(30.2f, 2f, 13.3f), 7.5f, 9.5f, Concrete);
                mood.Zone("StorageNorth", new Vector3(30.2f, 2f, 21f), 7.5f, 9.5f, Concrete);
                mood.Zone("Storeroom", new Vector3(30.2f, 2f, 33.3f), 9f, 11f, Concrete);
                // the library: the books soak it up, a softer, darker echo
                mood.Zone("Library", new Vector3(45.2f, 2f, 18.3f), 9f, 11f, Library);
                mood.SetUpSpark();
                break;
            default:
                Destroy(go);
                break;
        }
    }

    // The built-in presets were measured in play to be barely audible (+1.3 to +2.4 dB of echo after a
    // sound) and the stone rooms came out drier than the bedroom, so each space has its own settings.
    public struct Space { public int room, roomHF, reflections, reverb; public float decay, decayHF, reflectionsDelay, reverbDelay, gain; }
    static readonly Space Concrete = new Space { room = -150, roomHF = -350, decay = 2.1f, decayHF = 0.75f, reflections = -250, reflectionsDelay = 0.012f, reverb = 250, reverbDelay = 0.02f , gain = 1f };
    static readonly Space Library = new Space { room = -150, roomHF = -600, decay = 1.7f, decayHF = 0.6f, reflections = -400, reflectionsDelay = 0.008f, reverb = 200, reverbDelay = 0.015f, gain = 1.35f }; // softer room, a little more echo voice: as present as the concrete

    void Zone(string name, Vector3 centre, float inner, float outer, Space space)
    {
        var go = new GameObject("Reverb_" + name);
        go.transform.SetParent(transform, false);
        go.transform.position = centre;
        zones.Add((centre, inner, outer, space));
        var zone = go.AddComponent<AudioReverbZone>(); // for the positional sounds (fuse box, gate shimmer)
        zone.reverbPreset = AudioReverbPreset.User;
        zone.room = space.room; zone.roomHF = space.roomHF; zone.roomLF = 0;
        zone.decayTime = space.decay; zone.decayHFRatio = space.decayHF;
        zone.reflections = space.reflections; zone.reflectionsDelay = space.reflectionsDelay;
        zone.reverb = space.reverb; zone.reverbDelay = space.reverbDelay;
        zone.diffusion = 100f; zone.density = 100f;
        zone.minDistance = inner;
        zone.maxDistance = outer;
    }

    void SetUpSpark()
    {
        puzzle = FindAnyObjectByType<LeverPuzzle>();
        var sockets = FindObjectsByType<PlaceItemInteractable>(FindObjectsSortMode.None);
        if (puzzle == null || sockets.Length == 0) return;
        Vector3 box = Vector3.zero;
        foreach (var s in sockets) box += s.transform.position;
        box /= sockets.Length;
        var go = new GameObject("FuseBoxCrackle");
        go.transform.SetParent(transform, false);
        go.transform.position = box;
        spark = go.AddComponent<AudioSource>();
        spark.playOnAwake = false;
        spark.spatialBlend = 1f;
        spark.rolloffMode = AudioRolloffMode.Linear;
        spark.minDistance = 1.5f;
        spark.maxDistance = 14f;
        spark.clip = GameAudio.Mixed("spark", out sparkGain);
        nextSpark = Time.time + 4f;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float best = 0f;
        if (cam != null)
        {
            Vector3 p = cam.transform.position;
            foreach (var z in zones)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(z.c.x, z.c.z));
                float w = d <= z.inner ? 1f : Mathf.InverseLerp(z.outer, z.inner, d);
                if (w > best) { best = w; EchoSpace = z.s; }
            }
        }
        EchoAmount = best;
    }

    void OnDestroy() => EchoAmount = 0f;

    void Update()
    {
        if (spark == null || spark.clip == null) return;
        // the fuse box is on the wall the storage room shares with the house: a 3D sound alone came
        // straight through it into the dining and living rooms, so it is only heard in the wing (and a
        // little through the open door)
        var cam = Camera.main;
        float heard = cam != null ? StorageZone.HeardFromWing(cam.transform.position) : 1f;
        if (spark.isPlaying) spark.volume = sparkVolume * heard;
        if (Time.time < nextSpark) return;
        nextSpark = Time.time + Random.Range(6f, 13f);
        if (puzzle.IsPowered || PauseMenu.IsPaused || heard <= 0.01f) return;
        spark.pitch = Random.Range(0.9f, 1.15f);
        sparkVolume = Random.Range(0.6f, 1f) * sparkGain;
        spark.volume = sparkVolume * heard;
        spark.Play();
    }

    float sparkVolume;
}
