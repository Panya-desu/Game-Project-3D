using UnityEngine;

/// <summary>
/// Scrolls the water normal map in two directions so the floor shimmers like shallow water.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterRipple : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.012f, 0.007f);

    Material mat;
    static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

    void Start()
    {
        mat = GetComponent<Renderer>().material; // instance, so the shared asset is not modified
    }

    void Update()
    {
        if (mat == null) return;
        Vector2 o = speed * Time.time;
        o.y += Mathf.Sin(Time.time * 0.21f) * 0.01f;
        mat.SetTextureOffset(BumpMap, o);
    }
}
