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
/// </summary>
public class StorageZone : MonoBehaviour
{
    // storage x 22.7..37.7 z 0.8..25.8, storeroom x 22.7..37.7 z 25.8..40.8, library x 37.7..52.7 z 10.8..25.8
    static readonly Bounds Wing = new Bounds(new Vector3(37.7f, 3f, 20.8f), new Vector3(30f, 10f, 40f));
    static readonly Vector2 Doorway = new Vector2(22.6f, 22.8f);   // the door from the living room (x, z)
    const float FadeBefore = 3.5f, FadeAfter = 4.5f;   // metres before / after the doorway

    /// <summary>Smoothed weight for the camera this frame (what the volumes use).</summary>
    public static float Current { get; private set; }
    static int lastFrame = -1;

    Volume volume;

    public static float Weight(Vector3 p)
    {
        float d = Vector2.Distance(new Vector2(p.x, p.z), Doorway);
        float along = Wing.Contains(p) ? d : -d;   // negative in the house, positive in the wing
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-FadeBefore, FadeAfter, along));
    }

    public static float CameraWeight()
    {
        var cam = Camera.main;
        return cam != null ? Weight(cam.transform.position) : 0f;
    }

    void Awake() => volume = GetComponent<Volume>();

    void LateUpdate()
    {
        if (lastFrame != Time.frameCount)
        {
            float target = CameraWeight();
            // the first frame of a scene starts where the camera is; after that it eases (no jumps)
            Current = lastFrame < 0 || Time.frameCount - lastFrame > 2 ? target : Mathf.MoveTowards(Current, target, Time.deltaTime * 1.2f);
            lastFrame = Time.frameCount;
        }
        if (volume != null) volume.weight = Current;
    }

    void OnDestroy() => lastFrame = -1;
}
