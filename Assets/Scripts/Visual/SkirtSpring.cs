using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secondary motion for the skirt, the way anime games do it (spring bones): after the animation is
/// applied, the tip of each skirt bone is simulated in world space with inertia, a spring back towards the
/// animated pose, gravity and drag, so the skirt lags behind, swings when she walks, turns or stops, and
/// settles again. Capsules around the thighs keep it from passing through her legs.
/// Uses the rig's four skirt bones (SkirtFront/Back Left/Right). Added by CharacterLook.Apply.
/// </summary>
[DefaultExecutionOrder(10000)] // after the Animator and IK have posed the bones
public class SkirtSpring : MonoBehaviour
{
    [Tooltip("Pull back towards the animated pose (higher = stiffer skirt)")]
    public float stiffness = 2.2f;
    [Range(0f, 1f)] public float drag = 0.22f;
    public float gravity = 0.35f;
    [Tooltip("Largest swing away from the animated pose, in degrees")]
    public float maxAngle = 65f;
    public float legRadius = 0.085f;

    /// <summary>Largest bend away from the animated pose in the last frame, in degrees (for tests).</summary>
    public float LastBend { get; private set; }

    const float BoneLength = 0.307f; // from the Blender rig, in the model's own metres

    class Joint
    {
        public Transform bone;
        public Vector3 axis;      // local direction from the bone's head towards the hem
        public float length;     // world length
        public Vector3 tail, prevTail;
        public Quaternion animatedLocal; // the pose before this script bent it
    }

    readonly List<Joint> joints = new List<Joint>();
    Transform leftUpper, leftLower, rightUpper, rightLower;
    Vector3 lastRoot;
    bool ready;

    void Start()
    {
        var animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Skirt")) continue;
            // the skirt hangs down at start: the local axis that points most downwards runs along the bone
            Vector3 best = Vector3.up; float lowest = float.MaxValue;
            foreach (var a in new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                float y = t.TransformDirection(a).y;
                if (y < lowest) { lowest = y; best = a; }
            }
            joints.Add(new Joint { bone = t, axis = best, animatedLocal = t.localRotation });
        }
        enabled = joints.Count > 0;
    }

    // a clip that does not animate the skirt bones must not inherit last frame's bend as its pose
    void Update()
    {
        foreach (var j in joints) j.bone.localRotation = j.animatedLocal;
    }

    void LateUpdate()
    {
        foreach (var j in joints) j.animatedLocal = j.bone.localRotation;
        float scale = transform.lossyScale.y;
        bool reset = !ready || (transform.position - lastRoot).sqrMagnitude > 1f; // spawned or teleported
        lastRoot = transform.position;
        ready = true;
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        float bend = 0f;

        foreach (var j in joints)
        {
            j.length = BoneLength * scale;
            Vector3 head = j.bone.position;
            Vector3 animDir = j.bone.TransformDirection(j.axis).normalized;
            Vector3 rest = head + animDir * j.length;

            if (reset) { j.tail = j.prevTail = rest; continue; }
            if (dt <= 0f) { Aim(j, head, animDir); continue; } // paused: hold the last shape

            Vector3 velocity = (j.tail - j.prevTail) * (1f - drag);
            Vector3 next = j.tail + velocity
                         + (rest - j.tail) * Mathf.Clamp01(stiffness * dt * 4f)
                         + Vector3.down * gravity * scale * dt;

            next = head + (next - head).normalized * j.length;
            next = PushOut(next, leftUpper, leftLower, scale);
            next = PushOut(next, rightUpper, rightLower, scale);

            // keep the swing within a natural range of the animated pose
            Vector3 dir = (next - head).normalized;
            float angle = Vector3.Angle(animDir, dir);
            if (angle > maxAngle) dir = Vector3.Slerp(animDir, dir, maxAngle / angle);
            next = head + dir * j.length;

            j.prevTail = j.tail;
            j.tail = next;
            bend = Mathf.Max(bend, Vector3.Angle(animDir, next - head));
            Aim(j, head, animDir);
        }
        LastBend = bend;
    }

    void Aim(Joint j, Vector3 head, Vector3 animDir)
    {
        Vector3 dir = j.tail - head;
        if (dir.sqrMagnitude < 1e-8f) return;
        j.bone.rotation = Quaternion.FromToRotation(animDir, dir) * j.bone.rotation;
    }

    /// <summary>Moves a point out of the capsule around a thigh (hip joint to knee).</summary>
    Vector3 PushOut(Vector3 p, Transform upper, Transform lower, float scale)
    {
        if (upper == null || lower == null) return p;
        Vector3 a = upper.position, b = lower.position;
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        Vector3 closest = a + ab * t;
        float r = legRadius * scale;
        Vector3 d = p - closest;
        float dist = d.magnitude;
        if (dist >= r || dist < 1e-6f) return p;
        return closest + d / dist * r;
    }
}
