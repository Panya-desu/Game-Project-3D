using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Footsteps for the characters of the cutscenes (she walks towards the gates on the water, steps back
/// from the hit and walks on at the end; The Double rushes in): each foot her animation puts down
/// (FootL/FootR) plays the ground's footstep, quieter the further she is from the camera, and on the
/// water it leaves a ring like the player's own steps (WhiteSpaceFlair listens to <see cref="Stepped"/>).
/// Added to every character CharacterLibrary.Spawn makes; the player's own shadow body leaves it off
/// (PlayerBody plays her steps through Movement), and the title screen stays silent.
/// </summary>
public class ActorFootsteps : MonoBehaviour
{
    /// <summary>A cutscene character put a foot down (position, strength).</summary>
    public static event System.Action<Vector3, float> Stepped;

    EchoAnimationEvents events;
    Transform root;
    Vector3 lastPos;
    float speed, lastStep = -1f;
    /// <summary>Lower and darker steps (The Double).</summary>
    public bool heavy;

    public static ActorFootsteps Attach(EchoCharacterView view, bool heavy = false)
    {
        if (view == null) return null;
        var anim = view.GetComponentInChildren<Animator>() ?? view.GetComponentInParent<Animator>();
        var ev = anim != null ? anim.GetComponent<EchoAnimationEvents>() : null;
        if (ev == null) return null;
        var steps = ev.GetComponent<ActorFootsteps>();
        if (steps == null) steps = ev.gameObject.AddComponent<ActorFootsteps>();
        steps.heavy = heavy;
        return steps;
    }

    void Awake()
    {
        events = GetComponent<EchoAnimationEvents>();
        root = transform.root;
        lastPos = root.position;
        if (events != null) { events.LeftFoot += OnFoot; events.RightFoot += OnFoot; }
    }

    void OnDestroy()
    {
        if (events != null) { events.LeftFoot -= OnFoot; events.RightFoot -= OnFoot; }
    }

    void Update()
    {
        Vector3 d = root.position - lastPos;
        lastPos = root.position;
        float v = Time.deltaTime > 0f ? new Vector2(d.x, d.z).magnitude / Time.deltaTime : 0f;
        speed = Mathf.Lerp(speed, v, 1f - Mathf.Exp(-8f * Time.deltaTime));
    }

    void OnFoot()
    {
        if (!isActiveAndEnabled || SceneManager.GetActiveScene().name == "Main") return;
        if (Time.time - lastStep < 0.15f) return;
        lastStep = Time.time;
        var cam = Camera.main;
        float dist = cam != null ? Vector3.Distance(cam.transform.position, root.position) : 3f;
        // a walking step at full weight, a shift of weight while standing (looking round, the hit) softer
        float strength = speed > 0.25f ? 1f : 0.7f;
        float volume = strength * Mathf.Lerp(1f, 0.4f, Mathf.InverseLerp(2.5f, 10f, dist));
        string step = Movement.StepSound();
        bool water = step == "water_step_";
        GameAudio.PlaySfx(step + Random.Range(1, water ? 6 : 5), volume, heavy ? 0.04f : 0.08f, heavy ? 0.85f : 1f);
        Stepped?.Invoke(root.position, strength);
    }
}
