using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global sound player. Created automatically (no need to put it in a scene).
/// Clips are loaded by name from Assets/Resources/Audio, e.g. GameAudio.PlaySfx("unlock").
/// Ambient loops cross-fade, so walking between rooms changes the mood smoothly.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    /// <summary>Master volume 0..1 (changed from the pause menu, saved in PlayerPrefs).</summary>
    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        set { PlayerPrefs.SetFloat("MasterVolume", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
    }

    // Default ambience for each scene; AmbientZone triggers override it per room
    static readonly Dictionary<string, string> SceneAmbience = new Dictionary<string, string>
    {
        { "Main", "amb_title" },
        { "WhiteSpace", "amb_whitespace" },
        { "WhiteSpace 2", "amb_whitespace" },
        { "Level_1", "amb_home" },
        { "Bedroom", "amb_home" },
        { "House", "amb_home" },
        { "Confrontation", "amb_confront" },
        { "DemoEnd", "amb_menu" },
    };

    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> sfxPool = new List<AudioSource>();
    AudioSource ambientA, ambientB;
    bool aIsActive = true;
    string currentAmbient;
    Coroutine fadeRoutine;

    public const float AmbientVolume = 0.45f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[GameAudio]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ambientA = CreateSource("AmbientA", true);
        ambientB = CreateSource("AmbientB", true);
        AudioListener.volume = MasterVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SceneAmbience.TryGetValue(scene.name, out string amb))
            PlayAmbient(amb, 1.5f);
        else
            PlayAmbient(null, 1.5f);
    }

    AudioSource CreateSource(string name, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        src.volume = 0f;
        // UI clicks must still play while the pause menu pauses the listener
        src.ignoreListenerPause = !loop;
        // the ambience loops are already the sound of the room: no extra echo from the reverb zones (AudioMood)
        if (loop) src.reverbZoneMix = 0f;
        return src;
    }

    // ── the echo of the bare rooms ──
    // A sound's echo is played by its own voice: the same clip through a reverb filter, wet only (the sound
    // itself stays clear), and with silence added after it so the voice keeps playing until the echo has
    // died away (a filter stops with its source, which cut the echo off).
    readonly List<AudioSource> echoPool = new List<AudioSource>();
    readonly Dictionary<AudioClip, AudioClip> padded = new Dictionary<AudioClip, AudioClip>();

    void PlayEcho(AudioClip clip, float volume, float pitch)
    {
        var space = AudioMood.EchoSpace;
        var tail = Padded(clip, space.decay + 0.6f);
        if (tail == null) return;
        AudioSource v = null;
        foreach (var s in echoPool) if (!s.isPlaying) { v = s; break; }
        if (v == null)
        {
            if (echoPool.Count >= 8) return; // enough echo already
            v = CreateSource("Echo" + echoPool.Count, false);
            v.reverbZoneMix = 0f;
            v.gameObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.User;
            echoPool.Add(v);
        }
        var f = v.GetComponent<AudioReverbFilter>();
        f.dryLevel = -10000f; // wet only
        f.room = space.room; f.roomHF = space.roomHF; f.roomLF = 0f;
        f.decayTime = space.decay; f.decayHFRatio = space.decayHF;
        f.reflectionsLevel = space.reflections; f.reflectionsDelay = space.reflectionsDelay;
        f.reverbLevel = space.reverb; f.reverbDelay = space.reverbDelay;
        f.diffusion = 100f; f.density = 100f;
        v.clip = tail;
        v.volume = volume * (space.gain > 0f ? space.gain : 1f);
        v.pitch = pitch;
        v.Play();
    }

    AudioClip Padded(AudioClip clip, float extraSeconds)
    {
        if (padded.TryGetValue(clip, out var p)) return p;
        p = null;
        int extra = Mathf.CeilToInt(extraSeconds * clip.frequency);
        var data = new float[clip.samples * clip.channels];
        if (clip.loadState == AudioDataLoadState.Loaded && clip.GetData(data, 0))
        {
            p = AudioClip.Create(clip.name + " (echo)", clip.samples + extra, clip.channels, clip.frequency, false);
            p.SetData(data, 0); // the rest stays silent
        }
        padded[clip] = p;
        return p;
    }

    AudioClip Get(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;
        if (!cache.TryGetValue(clipName, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + clipName);
            if (clip == null) Debug.LogWarning("[GameAudio] Missing clip Resources/Audio/" + clipName);
            cache[clipName] = clip;
        }
        return clip;
    }

    // ───────────── Static helpers (safe to call even if audio failed to start) ─────────────

    public static void PlaySfx(string clipName, float volume = 1f, float pitchVariation = 0f)
    {
        if (Instance != null) Instance.PlaySfxInternal(clipName, volume, pitchVariation);
    }

    public static void PlayAmbient(string clipName, float fadeSeconds = 2f)
    {
        if (Instance != null) Instance.PlayAmbientInternal(clipName, fadeSeconds);
    }

    public static string CurrentAmbient => Instance != null ? Instance.currentAmbient : null;

    /// <summary>
    /// A held breath before a big moment: the ambience fades to silence over <paramref name="seconds"/> and
    /// stays silent; the next PlayAmbient (even of the same loop) fades back in from there.
    /// </summary>
    public static void Hush(float seconds)
    {
        if (Instance != null) Instance.PlayAmbientInternal(null, seconds);
    }

    void PlaySfxInternal(string clipName, float volume, float pitchVariation)
    {
        var clip = Get(clipName);
        if (clip == null) return;

        AudioSource src = null;
        foreach (var s in sfxPool)
            if (!s.isPlaying) { src = s; break; }
        if (src == null)
        {
            src = CreateSource("Sfx" + sfxPool.Count, false);
            sfxPool.Add(src);
        }

        src.clip = clip;
        // in an echoing room the sound itself is a touch quieter, so with its echo it is as loud as anywhere
        src.volume = volume * Mathf.Lerp(1f, 0.95f, AudioMood.EchoAmount);
        src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        src.Play();
        if (AudioMood.EchoAmount > 0.02f) PlayEcho(clip, volume * 0.42f * AudioMood.EchoAmount, src.pitch); // a soft echo, not louder
    }

    void PlayAmbientInternal(string clipName, float fadeSeconds)
    {
        if (clipName == currentAmbient && clipName != null) return;
        currentAmbient = clipName;

        AudioSource from = aIsActive ? ambientA : ambientB;
        AudioSource to = aIsActive ? ambientB : ambientA;
        aIsActive = !aIsActive;

        var clip = Get(clipName);
        to.clip = clip;
        to.volume = 0f;
        if (clip != null) to.Play();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(from, to, clip != null ? AmbientVolume : 0f, fadeSeconds));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float targetVolume, float seconds)
    {
        float startFrom = from.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, seconds));
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            to.volume = Mathf.Lerp(0f, targetVolume, k);
            yield return null;
        }
        from.Stop();
        from.volume = 0f;
        to.volume = targetVolume;
    }
}
