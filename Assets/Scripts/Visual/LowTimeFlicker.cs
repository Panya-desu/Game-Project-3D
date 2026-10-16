using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The house feels the memories fading: in the last five minutes of the 40-minute clock the lamps near the
/// player stutter now and then, a short uneven dip like failing power, more often as the clock runs out
/// (about every 20 s at five minutes, every 5 s in the last minute; the heartbeat of RunTimer comes in
/// then too). Lights that already animate themselves (LightFlicker), the emergency lights and the
/// character key lights are left alone; each lamp returns exactly to its own brightness.
/// </summary>
public class LowTimeFlicker : MonoBehaviour
{
    const float StartAt = 300f;   // seconds left
    float next;
    /// <summary>How many times the lamps have stuttered (for the smoke test).</summary>
    public static int Stutters;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level_1" || FindAnyObjectByType<LowTimeFlicker>() != null) return;
        var go = new GameObject("[LowTimeFlicker]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<LowTimeFlicker>();
    }

    void Update()
    {
        if (!RunTimer.Running || RunTimer.TimedOut || RunTimer.Remaining > StartAt || CutscenePlayer.IsPlaying || PauseMenu.IsPaused)
        {
            next = Time.time + 4f;
            return;
        }
        if (Time.time < next) return;
        float urgency = 1f - RunTimer.Remaining / StartAt; // 0 at five minutes, 1 at zero
        next = Time.time + Mathf.Lerp(20f, 5f, urgency) * Random.Range(0.75f, 1.25f);
        var cam = Camera.main;
        if (cam != null) StartCoroutine(Stutter(cam.transform.position, Mathf.Lerp(0.5f, 0.15f, urgency)));
    }

    IEnumerator Stutter(Vector3 near, float lowest)
    {
        var lamps = new List<(Light l, float i)>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || !l.enabled || l.intensity <= 0f) continue;
            if (l.GetComponent<LightFlicker>() != null || l.name.StartsWith("Emergency") || l.name == "KeyLight") continue;
            if (Vector3.Distance(l.transform.position, near) > 12f) continue;
            lamps.Add((l, l.intensity));
        }
        if (lamps.Count == 0) yield break;
        Stutters++;
        GameAudio.PlaySfx("spark", 0.18f, 0.1f);
        int dips = Random.Range(2, 5);
        for (int d = 0; d < dips; d++)
        {
            float k = Random.Range(lowest, lowest + 0.3f);
            foreach (var (l, i) in lamps) if (l != null) l.intensity = i * k;
            yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            foreach (var (l, i) in lamps) if (l != null) l.intensity = i;
            yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
        }
    }
}
