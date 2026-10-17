using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// How much the camera is "in" the storage wing (storage room, storeroom, library): 0 in the house and
/// 1 inside, with a long smooth ramp through the storage door: it starts dimming 3.5 m before the door in
/// the living room and is complete 4.5 m into the storage room, and it follows the camera gently over time. A box volume either leaked through the
/// whole wall into the dining room or, when made smaller, let the player peek in from the doorway
/// without the storage grading; this follows the rooms instead of a box.
/// Put on a global Volume to drive its weight (the cold storage grading); StoragePower uses
/// <see cref="Weight"/> for the blackout grading.
/// The dim also spills out of the doorway with the storage door: while the door is shut the living room
/// keeps its own look (nothing to peek at), and as the door swings open the darkness creeps out into the
/// living room within a second (all of it by the time the door is a third open, so even standing right
/// at the door there is no bright room to peek at), up to 7 m before the door: the player sees what waits inside.
/// </summary>
public class StorageZone : MonoBehaviour
{
    // storage x 22.7..37.7 z 0.8..25.8, storeroom x 22.7..37.7 z 25.8..40.8, library x 37.7..52.7 z 10.8..25.8
    static readonly Bounds Wing = new Bounds(new Vector3(37.7f, 3f, 20.8f), new Vector3(30f, 10f, 40f));
    static readonly Vector2 Doorway = new Vector2(22.6f, 22.8f);   // the door from the living room (x, z)
    const float FadeAfter = 4.5f;                      // metres into the wing until it is complete
    const float ShutBefore = 0.6f, OpenBefore = 7f;    // metres before the doorway it starts: door shut / open
    const float SpillSeconds = 0.8f;                   // how long the darkness takes to creep out (quick: no peeking)
    const float SpillLead = 0.3f;                      // the dark is all out once the door is this far open
    const float SpillAtDoor = 0.8f;                    // how dark it gets right at the open door

    /// <summary>0 = storage door shut, 1 = open (eased); outside play mode the door counts as open.</summary>
    public static float Spill { get; private set; } = 1f;
    static DoorController door;

    /// <summary>Smoothed weight for the camera this frame (what the volumes use).</summary>
    public static float Current { get; private set; }
    static int lastFrame = -1;

    Volume volume;

    public static float Weight(Vector3 p)
    {
        float d = Vector2.Distance(new Vector2(p.x, p.z), Doorway);
        float along = Wing.Contains(p) ? d : -d;   // negative in the house, positive in the wing
        if (along < 0f)
        {
            // the living-room side: nothing while the door is shut, dusk creeping out as it opens
            float before = Mathf.Lerp(ShutBefore, OpenBefore, Spill);
            return SpillAtDoor * Spill * Mathf.SmoothStep(0f, 1f, 1f + along / before);
        }
        // inside the wing: from the doorway's level to the full storage look 4.5 m in
        float atDoor = Mathf.Lerp(0.35f, SpillAtDoor, Spill);
        return Mathf.Lerp(atDoor, 1f, Mathf.SmoothStep(0f, 1f, along / FadeAfter));
    }

    /// <summary>
    /// How much of a sound made inside the wing reaches a listener at p: all of it inside, and in the house
    /// only a little through the open storage door, gone a few metres away (the walls keep the rest in).
    /// </summary>
    public static float HeardFromWing(Vector3 p)
    {
        if (Wing.Contains(p)) return 1f;
        float d = Vector2.Distance(new Vector2(p.x, p.z), Doorway);
        return 0.5f * Spill * (1f - Mathf.SmoothStep(0f, 1f, d / 4f));
    }

    public static float CameraWeight()
    {
        var cam = Camera.main;
        return cam != null ? Weight(cam.transform.position) : 0f;
    }

    void Awake()
    {
        volume = GetComponent<Volume>();
        door = null;
        float best = 3f;
        foreach (var d in FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 p = d.Hinge.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.z), Doorway);
            if (dist < best) { best = dist; door = d; }
        }
    }

    void LateUpdate()
    {
        if (lastFrame != Time.frameCount)
        {
            float open = door == null ? 1f : Mathf.Clamp01(door.Progress / SpillLead);
            Spill = lastFrame < 0 ? open : Mathf.MoveTowards(Spill, open, Time.deltaTime / SpillSeconds);
            float target = CameraWeight();
            // the first frame of a scene starts where the camera is; after that it eases (no jumps)
            Current = lastFrame < 0 || Time.frameCount - lastFrame > 2 ? target : Mathf.MoveTowards(Current, target, Time.deltaTime * 1.8f);
            lastFrame = Time.frameCount;
        }
        if (volume != null) volume.weight = Current;
    }

    void OnDestroy()
    {
        lastFrame = -1;
        Spill = 1f;
        door = null;
    }
}
