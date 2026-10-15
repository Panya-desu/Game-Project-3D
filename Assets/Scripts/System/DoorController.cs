using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swings a door between its closed and open poses.
/// The two poses are sampled once from the door's own animation clips (the clip whose name
/// contains "Close" gives the closed pose, the other one the open pose), then the Animator is
/// switched off and the hinge is tweened in code. This means:
///  - pressing open always starts from where the door is now (no "close first" clip),
///  - pressing again mid-swing reverses smoothly from the current angle instead of jumping,
///  - a saved door can be snapped straight to open/closed when the scene loads.
/// </summary>
public class DoorController : MonoBehaviour
{
    [Tooltip("Longest time a full swing may take (the clip length is used when shorter).")]
    [SerializeField] private float maxDuration = 1.4f;

    private class Part
    {
        public Transform t;
        public Vector3 closedPos, openPos;
        public Quaternion closedRot, openRot;
    }

    private readonly List<Part> parts = new List<Part>();
    private float progress;   // 0 = closed, 1 = open
    private float target;
    private float duration = 1f;
    private bool initialised;

    public bool IsOpen => target > 0.5f;
    public bool IsMoving => progress != target;

    /// <summary>Fires when a swing ends with the leaf back in the frame (for the slam sound).</summary>
    public event System.Action Closed;

    /// <summary>The transform that swings (for tests / debugging).</summary>
    public Transform Hinge
    {
        get
        {
            Init();
            return parts.Count > 0 ? parts[0].t : transform;
        }
    }

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (initialised) return;
        initialised = true;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null) return;
        animator.enabled = false;
        if (animator.runtimeAnimatorController == null) return;

        AnimationClip openClip = null, closeClip = null;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            if (clip.name.ToLowerInvariant().Contains("close"))
            {
                if (closeClip == null) closeClip = clip;
            }
            else if (openClip == null)
            {
                openClip = clip;
            }
        }

        GameObject root = animator.gameObject;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        var closedPos = new Vector3[all.Length];
        var closedRot = new Quaternion[all.Length];

        // closed pose: end of the close clip, or the pose the door was placed in
        if (closeClip != null) closeClip.SampleAnimation(root, closeClip.length);
        for (int i = 0; i < all.Length; i++)
        {
            closedPos[i] = all[i].localPosition;
            closedRot[i] = all[i].localRotation;
        }

        // open pose: end of the open clip
        if (openClip != null)
        {
            openClip.SampleAnimation(root, openClip.length);
            duration = Mathf.Clamp(openClip.length, 0.3f, maxDuration);
        }

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t.localPosition == closedPos[i] && t.localRotation == closedRot[i]) continue;
            parts.Add(new Part
            {
                t = t,
                closedPos = closedPos[i],
                openPos = t.localPosition,
                closedRot = closedRot[i],
                openRot = t.localRotation
            });
        }

        if (parts.Count == 0)
        {
            Debug.LogWarning("[DoorController] Could not find an open pose for " + name);
        }

        Apply(0f);
    }

    private void Update()
    {
        if (progress == target) return;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / duration);
        Apply(progress);
        if (progress == 0f && Closed != null) Closed();
    }

    private void Apply(float p)
    {
        // slows down near fully open, but keeps its speed near the frame so closing ends in a slam
        float s = 1f - (1f - p) * (1f - p);
        foreach (Part part in parts)
        {
            part.t.localPosition = Vector3.Lerp(part.closedPos, part.openPos, s);
            part.t.localRotation = Quaternion.Slerp(part.closedRot, part.openRot, s);
        }
    }

    public void OpenDoor()
    {
        Init();
        target = 1f;
    }

    public void CloseDoor()
    {
        Init();
        target = 0f;
    }

    /// <summary>Jump straight to open/closed without animating (used when loading a save).</summary>
    public void SetOpenImmediate(bool open)
    {
        Init();
        target = progress = open ? 1f : 0f;
        Apply(progress);
    }
}
