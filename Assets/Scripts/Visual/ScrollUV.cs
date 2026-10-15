using UnityEngine;

/// <summary>Scrolls a material's base (and emission) map, e.g. the heart monitor trace.</summary>
public class ScrollUV : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.25f, 0f);
    Material mat;
    Vector2 offset;

    void Start() => mat = GetComponent<Renderer>().material;

    void Update()
    {
        if (mat == null) return;
        offset += speed * Time.deltaTime;
        offset.x %= 1f;
        mat.SetTextureOffset("_BaseMap", offset);
        if (mat.HasProperty("_EmissionMap")) mat.SetTextureOffset("_EmissionMap", offset);
    }
}
