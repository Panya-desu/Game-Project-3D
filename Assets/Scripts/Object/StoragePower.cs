using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storage-room power, driven by the fuses of LeverPuzzle (Panya's fuse box puzzle).
/// Without power the room lights are dim and red emergency lights pulse; when the last fuse
/// is placed the lights flicker back on with a power-up sound. Works with save/load because it
/// only reads LeverPuzzle.IsPowered.
/// </summary>
public class StoragePower : MonoBehaviour
{
    public LeverPuzzle puzzle;
    public List<Light> roomLights = new List<Light>();
    public List<Light> emergencyLights = new List<Light>();
    [Range(0f, 1f)] public float unpoweredFactor = 0.25f;
    [Tooltip("Local volume with the dark, cold grading used while the power is out")]
    public UnityEngine.Rendering.Volume blackoutVolume;

    readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
    bool powered;
    float blackoutFade;

    // the blackout grading only applies while the camera is in the storage wing (or at its door)
    void LateUpdate()
    {
        if (blackoutVolume != null && initialised) blackoutVolume.weight = blackoutFade * StorageZone.Current;
    }
    bool initialised;

    void Start()
    {
        foreach (var l in roomLights) if (l != null) baseIntensity[l] = l.intensity;
        powered = puzzle == null || puzzle.IsPowered;
        Apply(powered ? 1f : unpoweredFactor);
        SetEmergency(!powered);
        blackoutFade = powered ? 0f : 1f;
        initialised = true;
    }

    void Update()
    {
        if (!initialised || puzzle == null) return;
        blackoutFade = Mathf.MoveTowards(blackoutFade, powered ? 0f : 1f, Time.deltaTime * 0.6f);
        bool now = puzzle.IsPowered;
        if (now == powered) return;
        powered = now;
        StopAllCoroutines();
        StartCoroutine(powered ? PowerUp() : PowerDown());
    }

    IEnumerator PowerUp()
    {
        GameAudio.PlaySfx("power_up");
        SetEmergency(false);
        for (int i = 0; i < 6; i++)
        {
            Apply(i % 2 == 0 ? 1f : unpoweredFactor);
            yield return new WaitForSeconds(Random.Range(0.06f, 0.2f));
        }
        Apply(1f);
    }

    IEnumerator PowerDown()
    {
        GameAudio.PlaySfx("power_down");
        Apply(unpoweredFactor);
        yield return new WaitForSeconds(0.5f);
        SetEmergency(true);
    }

    void Apply(float factor)
    {
        foreach (var kv in baseIntensity)
        {
            if (kv.Key == null) continue;
            kv.Key.intensity = kv.Value * factor;
            var flicker = kv.Key.GetComponent<LightFlicker>();
            if (flicker != null) flicker.SetBaseIntensity(kv.Value * factor);
        }
    }

    void SetEmergency(bool on)
    {
        foreach (var l in emergencyLights) if (l != null) l.enabled = on;
    }
}
