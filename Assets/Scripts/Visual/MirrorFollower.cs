using UnityEngine;

/// <summary>
/// Fake water reflection: this object lives under a parent scaled (1,-1,1) and copies the local
/// transform of its source every frame, so the mirrored copy moves with the original
/// (floating shards, opening gate leaves). Cheaper and sharper than a reflection camera.
/// </summary>
public class MirrorFollower : MonoBehaviour
{
    public Transform source;

    void LateUpdate()
    {
        if (source == null) return;
        transform.localPosition = source.localPosition;
        transform.localRotation = source.localRotation;
        transform.localScale = source.localScale;
        if (gameObject.activeSelf != source.gameObject.activeInHierarchy)
            gameObject.SetActive(source.gameObject.activeInHierarchy);
    }
}
