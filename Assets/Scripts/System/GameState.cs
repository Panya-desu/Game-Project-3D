using UnityEngine;

/// <summary>
/// Central place to ask "can the player move / look / interact right now?".
/// Combines every system that takes over the mouse or keyboard (pause menu, keypads, phone,
/// dialogue, cutscenes such as the title screen or scene fades).
/// </summary>
public static class GameState
{
    /// <summary>Set by fades, the title screen and scripted scenes.</summary>
    public static bool IsCutscene { get; set; }

    static int escapeConsumedFrame = -1;

    /// <summary>Call when Esc was used to close something, so the pause menu doesn't also open this frame.</summary>
    public static void ConsumeEscape() => escapeConsumedFrame = Time.frameCount;

    public static bool EscapeConsumedThisFrame => escapeConsumedFrame == Time.frameCount;

    /// <summary>True while any overlay owns the input (player should not walk or look around).</summary>
    public static bool InputBlocked =>
        PauseMenu.IsPaused || IsCutscene || KeypadInteractable.IsEnteringKeypad || PhoneManager.IsPhoneOpen;

    /// <summary>InputBlocked, or a dialogue is on screen.</summary>
    public static bool InputBlockedOrTalking => InputBlocked || DialogueManager.IsAnyTalking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // Static fields survive "Enter Play Mode" when domain reload is disabled
        IsCutscene = false;
    }
}
