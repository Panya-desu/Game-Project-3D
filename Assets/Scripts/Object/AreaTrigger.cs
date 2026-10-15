using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Invisible box that reacts when the player walks in: changes the ambient sound of the room
/// and/or fires an event (e.g. a quest objective "enter the storage room").
/// Needs a Collider with "Is Trigger" ticked.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AreaTrigger : MonoBehaviour
{
    [Tooltip("Ambient loop from Resources/Audio to play inside this area (empty = don't change)")]
    public string ambient = "";

    [Tooltip("Optional: while this puzzle has no power (fuses missing) play unpoweredAmbient instead")]
    public LeverPuzzle powerSource;
    public string unpoweredAmbient = "amb_blackout";

    [Tooltip("Fire onEnter only the first time")]
    public bool once = true;

    [Tooltip("Keep firing onEnter (twice a second) while the player stays inside. Useful for quest objectives that may not be active yet when the player arrives.")]
    public bool fireWhileInside = false;

    public UnityEvent onEnter = new UnityEvent();

    bool fired;
    float nextStayFire;

    /// <summary>The ambience this area wants right now.</summary>
    public string CurrentAmbient =>
        powerSource != null && !powerSource.IsPowered && !string.IsNullOrEmpty(unpoweredAmbient) ? unpoweredAmbient : ambient;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Movement>() == null) return;

        if (!string.IsNullOrEmpty(ambient))
        {
            GameAudio.PlayAmbient(CurrentAmbient, 2f);
        }

        if (once && fired) return;
        fired = true;
        onEnter.Invoke();
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponentInParent<Movement>() == null) return;

        // Power came back while standing inside: switch the ambience
        if (!string.IsNullOrEmpty(ambient) && GameAudio.CurrentAmbient != CurrentAmbient)
        {
            GameAudio.PlayAmbient(CurrentAmbient, 2f);
        }

        if (!fireWhileInside || Time.time < nextStayFire) return;
        nextStayFire = Time.time + 0.5f;
        onEnter.Invoke();
    }
}
