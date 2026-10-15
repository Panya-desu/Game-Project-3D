using UnityEngine;

/// <summary>
/// Size of the protagonist's model in cutscenes, matched to the first-person player's eye height so she
/// fits the doors and rooms built for the player (the model's eyes are ~1.45 m above its soles).
/// Gameplay itself stays first person: the player has no visible body.
/// </summary>
public static class PlayerAvatar
{
    /// <summary>Character scale for the current scene (1 when there is no player).</summary>
    public static float ModelScale
    {
        get
        {
            var mover = Object.FindAnyObjectByType<Movement>();
            if (mover == null) return 1f;
            var cam = mover.GetComponentInChildren<Camera>(true);
            if (cam == null) return 1f;
            // eye height above the floor she stands on (the controller's capsule hovers a little above it)
            float floor = CharacterLibrary.GroundBelow(mover.transform.position).y;
            float eyeAboveFeet = cam.transform.position.y - floor;
            if (eyeAboveFeet <= 0.5f)
            {
                var controller = mover.GetComponent<CharacterController>();
                float bottom = controller != null ? controller.center.y - controller.height / 2f : -1f;
                eyeAboveFeet = (cam.transform.localPosition.y - bottom) * mover.transform.lossyScale.y;
            }
            // the levels are built about twice real size (house doors 4.2 m, chairs 2.1 m, the player's eye
            // 2.8 m above the floor), so her eyes are put at the player's eye height: about 1.9x, which
            // makes her ~76% of a door's height like a real person (the old 1.5x cap made her look small)
            return Mathf.Clamp(eyeAboveFeet / 1.45f, 1f, 2.5f);
        }
    }
}
