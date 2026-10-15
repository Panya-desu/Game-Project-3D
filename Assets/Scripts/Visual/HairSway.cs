using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hair (and the bow) that flows with her movement, like anime games: the model has no hair bones, so a
/// point near the hair tips is simulated as a damped spring that lags behind the head when she walks,
/// turns or stops, plus a light breeze. The Echo/Toon shader bends each vertex by that offset, more the
/// further it hangs below the crown, so the roots stay put and the tips swing (_SwayAnchor/_SwayOffset).
/// Added by CharacterLook.Apply.
/// </summary>
[DefaultExecutionOrder(10001)] // after the Animator, IK and SkirtSpring
public class HairSway : MonoBehaviour
{
    public float stiffness = 55f;
    public float damping = 7f;
    public float maxSwing = 0.08f;   // metres at the tips, model scale
    public float breeze = 0.006f;

    class Part
    {
        public Renderer renderer;
        public Transform bone;
        public float crown, length, follow; // anchor height above the bone, sway length, how far the tip point hangs
        public Vector3 pos, vel;
        public bool ready;
    }

    readonly List<Part> parts = new List<Part>();
    MaterialPropertyBlock block;
    static readonly int AnchorId = Shader.PropertyToID("_SwayAnchor"), OffsetId = Shader.PropertyToID("_SwayOffset");

    void Start()
    {
        var animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) { enabled = false; return; }
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Hair" && head != null) parts.Add(new Part { renderer = r, bone = head, crown = 0.13f, length = 0.4f, follow = 0.22f });
            else if (r.name == "Ribbon" && chest != null) parts.Add(new Part { renderer = r, bone = chest, crown = 0.06f, length = 0.1f, follow = 0.06f });
        }
        block = new MaterialPropertyBlock();
        enabled = parts.Count > 0;
    }

    void LateUpdate()
    {
        float s = transform.lossyScale.y;
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        foreach (var p in parts)
        {
            if (p.renderer == null || p.bone == null) continue;
            Vector3 anchor = p.bone.position + Vector3.up * p.crown * s;
            // the point that should swing: below the anchor and a little behind the face, so turning moves it too
            Vector3 target = anchor - Vector3.up * p.follow * s - p.bone.forward * 0.03f * s;
            if (!p.ready || (p.pos - target).sqrMagnitude > 1f) { p.pos = target; p.vel = Vector3.zero; p.ready = true; }
            if (dt > 0f)
            {
                p.vel += (target - p.pos) * stiffness * dt;
                p.vel *= Mathf.Exp(-damping * dt);
                p.pos += p.vel * dt;
            }
            Vector3 offset = Vector3.ClampMagnitude(p.pos - target, maxSwing * s);
            offset.y = Mathf.Min(offset.y, 0f) * 0.3f; // swing sideways and back, hardly up or down
            offset += p.bone.right * Mathf.Sin(Time.time * 1.3f + p.crown * 10f) * breeze * s;

            p.renderer.GetPropertyBlock(block);
            block.SetVector(AnchorId, new Vector4(anchor.x, anchor.y, anchor.z, p.length * s));
            block.SetVector(OffsetId, offset);
            p.renderer.SetPropertyBlock(block);
        }
    }
}
