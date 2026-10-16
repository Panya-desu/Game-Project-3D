using System.Collections.Generic;
using UnityEngine;
using EchoCharacters;

/// <summary>
/// Anime look for the Echo characters: swaps their realistic materials for the Echo/Toon shader with
/// per-part settings (warm skin shadows, a hair highlight band, bright eyes, outlines; glowing red eyes
/// and a red rim for The Double), and turns the head and eyes towards a look target with Humanoid IK.
/// Added automatically by CharacterLibrary when a character is spawned.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterLook : MonoBehaviour
{
    public Transform lookTarget;
    public Vector3? lookPoint;
    [Range(0, 1)] public float lookWeight = 0.8f;

    Animator animator;
    float weight;

    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static CharacterLook Apply(GameObject root, bool isDouble)
    {
        var anim = root.GetComponentInChildren<Animator>();
        if (anim == null) return null;
        var look = anim.GetComponent<CharacterLook>();
        if (look == null) look = anim.gameObject.AddComponent<CharacterLook>();
        var secondary = root.GetComponentInChildren<EchoSecondaryMotion>(true);
        if (secondary == null) secondary = root.AddComponent<EchoSecondaryMotion>();
        // gentler than the package defaults: with 8 degrees per joint the three-joint skirt chains plus the
        // authored skirt curves flipped the back of the skirt up when walking (limits are read in OnEnable)
        secondary.enabled = false;
        secondary.skirtMaxAngle = 4f;
        secondary.hairMaxAngle = 5f;
        secondary.stiffness = 65f;
        secondary.drag = 0.35f;
        secondary.shirtMaxAngle = 2.5f;
        secondary.layerGuard = false; // the per-frame cloth checks cost ~40 ms per character (title screen lag)
        secondary.enabled = true;
        var shader = CharacterLibrary.Get() != null ? CharacterLibrary.Get().toonShader : null;
        if (shader == null) shader = Shader.Find("Echo/Toon");
        if (shader == null) return look;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].name.Contains("Lens")) continue; // glass stays transparent
                mats[i] = Toon(mats[i], shader, isDouble);
            }
            r.sharedMaterials = mats;
        }
        return look;
    }

    static Material Toon(Material src, Shader shader, bool isDouble)
    {
        string key = src.name + (isDouble ? "#double" : "");
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(shader) { name = src.name + " (Toon)" };
        if (src.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
        if (src.HasProperty("_BaseColor")) m.SetColor("_BaseColor", src.GetColor("_BaseColor"));

        string n = src.name.ToLowerInvariant();
        // defaults: clothes
        Color shade = new Color(0.74f, 0.76f, 0.9f), outline = new Color(0.2f, 0.2f, 0.3f);
        float width = 0.8f, shadeAmount = 0.85f, rim = 0.3f, spec = 0f, ambient = 0.65f;
        Color emission = Color.black, rimColor = new Color(1f, 0.95f, 0.9f);
        float selfLit = 0f, shadowStrength = 0.6f, lampShadow = 1f;
        Color specColor = Color.white;

        // palette "U1 + S1" (_work/palettes.py): soft porcelain skin with a pink shadow; blue-black hair with a
        // blue shadow, a pale-blue highlight band and a deep navy outline, in the HoYoverse manner
        if (n.Contains("body")) { shade = new Color(0.95f, 0.8f, 0.84f); outline = new Color(0.5f, 0.28f, 0.26f); width = 0.55f; shadeAmount = 0.75f; rim = 0.22f; shadowStrength = 0.3f; lampShadow = 0f; } // no lamp shadows on her skin: under a ceiling lamp the chin's shadow drew a second chin on her neck
        else if (n.Contains("hair")) { shade = new Color(0.62f, 0.66f, 0.92f); outline = new Color(0.04f, 0.05f, 0.11f); width = 0.9f; spec = 0.55f; specColor = new Color(0.72f, 0.8f, 1f); shadowStrength = 0.45f; }
        else if (n.Contains("eye")) { shadeAmount = 0.15f; width = 0f; rim = 0f; ambient = 0.9f; emission = new Color(0.06f, 0.06f, 0.08f); selfLit = 0.75f; shadowStrength = 0f; }
        else if (n.Contains("ribbon")) { shade = new Color(0.78f, 0.55f, 0.62f); outline = new Color(0.35f, 0.08f, 0.12f); width = 0.6f; }
        else if (n.Contains("shirt"))
        {
            // a white blouse and collar (the v4 Shirt.png is plain white): a blue shade tint and navy
            // outlines made blue streaks along the collar
            m.SetTexture("_BaseMap", src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null ? src.GetTexture("_BaseMap") : Texture2D.whiteTexture);
            m.SetColor("_BaseColor", new Color(0.97f, 0.97f, 0.96f));
            shade = new Color(0.86f, 0.86f, 0.9f); outline = new Color(0.5f, 0.5f, 0.55f); width = 0.6f;
        }
        else if (n.Contains("skirt")) { shade = new Color(0.6f, 0.6f, 0.78f); outline = new Color(0.08f, 0.08f, 0.16f); width = 0.9f; }
        else if (n.Contains("shoe")) { outline = new Color(0.05f, 0.05f, 0.08f); width = 0.6f; }
        else if (n.Contains("glasses") || n.Contains("earring") || n.Contains("button")) { width = 0.35f; rim = 0.5f; }

        if (isDouble)
        {
            shade = new Color(0.55f, 0.45f, 0.55f);
            outline = new Color(0.02f, 0f, 0.02f);
            rimColor = new Color(1f, 0.15f, 0.15f);
            rim = 0.9f;
            if (n.Contains("eye")) emission = new Color(1.6f, 0.08f, 0.06f);
        }

        m.SetColor("_ShadeColor", shade);
        m.SetFloat("_ShadeAmount", shadeAmount);
        m.SetFloat("_ShadeThreshold", 0.02f);
        m.SetFloat("_ShadeSoftness", 0.12f);
        m.SetFloat("_AmbientStrength", ambient);
        m.SetColor("_RimColor", rimColor);
        m.SetFloat("_RimStrength", rim);
        m.SetFloat("_SpecStrength", spec);
        m.SetColor("_SpecColor", specColor);
        m.SetColor("_Emission", emission);
        m.SetColor("_OutlineColor", outline);
        m.SetFloat("_OutlineWidth", width);
        m.SetFloat("_SelfLit", selfLit);
        m.SetFloat("_ShadowStrength", shadowStrength);
        m.SetFloat("_LampShadowStrength", lampShadow);
        m.SetFloat("_SwayAmount", 0f); // v4 bones supply secondary motion
        cache[key] = m;
        return m;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        SmoothChinSeam();
    }

    static readonly Dictionary<Mesh, Mesh> smoothed = new Dictionary<Mesh, Mesh>();

    /// <summary>
    /// Her face is skinned 100% to the head bone, but the neck right under the chin only ~45%, so the
    /// weight jumped from 0.45 to 1 at the jaw line: whenever the head turned (LookAround turns it ~30
    /// degrees) the skin folded along that seam into a second chin and the jaw looked bent. The head
    /// weight of the blended neck skin is rescaled so it rises smoothly to 1 at the jaw (the neck below
    /// still blends into the shoulders). Done once per mesh, on a copy; the asset is untouched.
    /// </summary>
    void SmoothChinSeam()
    {
        if (animator == null || !animator.isHuman) return;
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null || head.parent == null) return;
        Transform neck = head.parent;
        foreach (var smr in transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var src = smr.sharedMesh;
            if (src == null) continue;
            if (smoothed.TryGetValue(src, out var done)) { if (done != null) smr.sharedMesh = done; continue; }
            int hi = System.Array.IndexOf(smr.bones, head), ni = System.Array.IndexOf(smr.bones, neck);
            smoothed[src] = null;
            if (hi < 0 || ni < 0 || !src.isReadable) continue;
            var bw = src.boneWeights;
            float top = 0f; // the highest head weight on the blended neck skin (just under the jaw)
            foreach (var b in bw) if (Weight(b, ni) > 0.01f) top = Mathf.Max(top, Weight(b, hi));
            if (top <= 0.05f || top >= 0.95f) continue;
            for (int i = 0; i < bw.Length; i++)
            {
                float wh = Weight(bw[i], hi), wn = Weight(bw[i], ni);
                if (wh <= 0f || wn <= 0.01f) continue;
                float t = Mathf.Clamp01(wh / top);
                float target = Mathf.Min(wh + wn, Mathf.Lerp(wh, 1f, t * t)); // only the top of the band moves much
                bw[i] = SetWeights(bw[i], hi, target, ni, wh + wn - target);
            }
            var copy = Instantiate(src);
            copy.name = src.name + " (smooth chin)";
            copy.boneWeights = bw;
            smoothed[src] = copy;
            smr.sharedMesh = copy;
        }
    }

    static float Weight(BoneWeight b, int i) =>
        (b.boneIndex0 == i ? b.weight0 : 0f) + (b.boneIndex1 == i ? b.weight1 : 0f) + (b.boneIndex2 == i ? b.weight2 : 0f) + (b.boneIndex3 == i ? b.weight3 : 0f);

    static BoneWeight SetWeights(BoneWeight b, int hi, float wh, int ni, float wn)
    {
        if (b.boneIndex0 == hi) b.weight0 = wh; else if (b.boneIndex0 == ni) b.weight0 = wn;
        if (b.boneIndex1 == hi) b.weight1 = wh; else if (b.boneIndex1 == ni) b.weight1 = wn;
        if (b.boneIndex2 == hi) b.weight2 = wh; else if (b.boneIndex2 == ni) b.weight2 = wn;
        if (b.boneIndex3 == hi) b.weight3 = wh; else if (b.boneIndex3 == ni) b.weight3 = wn;
        return b;
    }

    /// <summary>Look at a point (null to look ahead again).</summary>
    public void LookAt(Vector3? point) { lookPoint = point; lookTarget = null; }

    void Update()
    {
        bool has = lookTarget != null || lookPoint.HasValue;
        weight = Mathf.MoveTowards(weight, has ? lookWeight : 0f, Time.deltaTime * 1.5f);
    }

    void OnAnimatorIK(int layer)
    {
        if (animator == null || weight <= 0.001f) { if (animator != null) animator.SetLookAtWeight(0f); return; }
        Vector3 p = lookTarget != null ? lookTarget.position : lookPoint.GetValueOrDefault();
        animator.SetLookAtWeight(weight, 0.15f, 0.8f, 1f, 0.6f);
        animator.SetLookAtPosition(p);
    }
}
