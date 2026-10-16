using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The shafts of light in the White Space and on the title screen were flat textured quads: seen from the
/// side they showed as 2D strips hanging in the air. On load each "LightRay" quad gets the soft Echo/LightBeam
/// shader (a cone that widens to its foot, faded ends, drifting dust, a slow breath, each beam out of step
/// with the others) and turns about its own long axis to face the camera, so it reads as a volume of light
/// from every side.
/// </summary>
public class LightBeams : MonoBehaviour
{
    Vector3 axis;
    static Material beamMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (beamMaterial == null)
        {
            var shader = Resources.Load<Shader>("Shaders/LightBeam");
            if (shader == null) return;
            beamMaterial = new Material(shader) { name = "LightBeam (runtime)" };
        }
        int i = 0;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name != "LightRay" || r.GetComponent<LightBeams>() != null) continue;
            r.sharedMaterial = beamMaterial;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Phase", i++ * 2.3f);
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.gameObject.AddComponent<LightBeams>();
        }
    }

    void Awake() => axis = transform.up;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 toCam = Vector3.ProjectOnPlane(cam.transform.position - transform.position, axis);
        if (toCam.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(-toCam.normalized, axis); // the quad's face (-Z) to the camera
    }
}
