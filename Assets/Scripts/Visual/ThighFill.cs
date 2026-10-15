using UnityEngine;

/// <summary>
/// The character model has no skin on the upper thighs: everything under the skirt, from the hip joints
/// down to 0.29 m along the thigh bone, was cut away in the source model. When a leg swings forward the
/// skirt does not cover that, and the legs look detached from the body. This fills the gap with a tapered
/// skin tube on each thigh bone, shaped to the cut edge (measured in Blender), and a skirt-coloured inner
/// piece around the hips. Added by CharacterLook.Apply.
/// </summary>
public class ThighFill : MonoBehaviour
{
    // cut edge of the thigh skin, in the source model's metres, measured along the UpperLeg bone
    const float RimAlong = 0.2924f, RimFront = 0.013f, RimRadiusFront = 0.0666f, RimRadiusSide = 0.0615f;
    const int Segments = 20, Rings = 8;
    /// <summary>Stencil value the skirt writes; the fill is drawn only where it is absent (the original
    /// skirt was made for a body without thighs and does not clear a leg that swings forward).</summary>
    public const int StencilBit = 8;

    static readonly System.Collections.Generic.Dictionary<Material, Material> hidden = new System.Collections.Generic.Dictionary<Material, Material>();

    static Material UnderSkirtOnly(Material src)
    {
        if (src == null) return null;
        if (hidden.TryGetValue(src, out var m) && m != null) return m;
        m = new Material(src) { name = src.name + " (under skirt)" };
        m.SetFloat("_StencilRef", StencilBit);
        m.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
        m.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Keep);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 10; // after the skirt has marked its pixels
        hidden[src] = m;
        return m;
    }

    public static void Add(GameObject character, Material skin, Material skirt)
    {
        var animator = character.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman || skin == null) return;
        var fill = animator.GetComponent<ThighFill>() ?? animator.gameObject.AddComponent<ThighFill>();
        fill.Build(animator, UnderSkirtOnly(skin), UnderSkirtOnly(skirt));
    }

    /// <summary>World positions of the body mesh in its current pose (null if the mesh is not readable).</summary>
    static Vector3[] BodyVertices(Animator animator)
    {
        foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name != "Body" || smr.sharedMesh == null || !smr.sharedMesh.isReadable) continue;
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var v = baked.vertices;
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            Destroy(baked);
            return v;
        }
        return null;
    }

    /// <summary>Radius of the skin around the bone in each direction, in the band right below the cut edge.</summary>
    static void MeasureRim(Vector3[] body, Vector3 a, Vector3 axis, Vector3 f, Vector3 side, float s, float[] rim, ref Vector2 centre)
    {
        var pts = new System.Collections.Generic.List<Vector2>();
        foreach (var w in body)
        {
            Vector3 rel = w - a;
            float along = Vector3.Dot(rel, axis) / s;
            if (along < RimAlong - 0.004f || along > RimAlong + 0.012f) continue;
            Vector3 radial = rel - axis * (along * s);
            if (radial.magnitude > 0.095f * s) continue; // not the other leg
            pts.Add(new Vector2(Vector3.Dot(radial, f), Vector3.Dot(radial, side)));
        }
        if (pts.Count < 12) return;
        Vector2 c = Vector2.zero;
        foreach (var p in pts) c += p;
        c /= pts.Count;
        var best = new float[rim.Length];
        foreach (var p in pts)
        {
            Vector2 d = p - c;
            float ang = Mathf.Atan2(d.y, d.x);
            if (ang < 0f) ang += Mathf.PI * 2f;
            int k = Mathf.RoundToInt(ang / (Mathf.PI * 2f) * rim.Length) % rim.Length;
            best[k] = Mathf.Max(best[k], d.magnitude);
        }
        for (int k = 0; k < rim.Length; k++)
            if (best[k] <= 0f) best[k] = Mathf.Max(best[(k + rim.Length - 1) % rim.Length], best[(k + 1) % rim.Length]);
        for (int k = 0; k < rim.Length; k++)
            if (best[k] > 0f) rim[k] = (best[(k + rim.Length - 1) % rim.Length] + 2f * best[k] + best[(k + 1) % rim.Length]) / 4f;
        centre = c;
    }

    void Build(Animator animator, Material skin, Material skirt)
    {
        float s = animator.transform.lossyScale.y;
        Vector3 front = animator.transform.forward;
        Vector3[] body = BodyVertices(animator);
        foreach (var (upper, lower) in new[]
        {
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
        })
        {
            var a = animator.GetBoneTransform(upper);
            var b = animator.GetBoneTransform(lower);
            if (a == null || b == null || a.Find("ThighFill") != null) continue;
            Vector3 axis = (b.position - a.position).normalized;
            Vector3 f = Vector3.ProjectOnPlane(front, axis).normalized;
            Vector3 side = Vector3.Cross(axis, f);

            // the cut edge as it really is on this model: the radius of the skin right at the edge in each
            // direction around the bone (falls back to the ellipse measured in Blender)
            var rim = new float[Segments];
            for (int k = 0; k < Segments; k++)
            {
                float ang = k / (float)Segments * Mathf.PI * 2f;
                rim[k] = new Vector2(Mathf.Cos(ang) * RimRadiusFront, Mathf.Sin(ang) * RimRadiusSide).magnitude * s;
            }
            Vector2 centre = new Vector2(RimFront * s, 0f);
            if (body != null) MeasureRim(body, a.position, axis, f, side, s, rim, ref centre);

            // rings: up the thigh, the cut edge (a hair wider, so it closes the opening), then just inside the leg
            var ringAlong = new float[Rings + 1];
            var ringGrow = new float[Rings + 1];
            for (int r = 0; r <= Rings - 2; r++)
            {
                float t = r / (float)(Rings - 2);
                ringAlong[r] = Mathf.Lerp(0.16f, RimAlong - 0.004f, t);
                ringGrow[r] = Mathf.Lerp(0.95f, 1.0f, t);
            }
            ringAlong[Rings - 1] = RimAlong + 0.002f; ringGrow[Rings - 1] = 1.02f;
            ringAlong[Rings] = RimAlong + 0.014f; ringGrow[Rings] = 0.94f;

            var verts = new Vector3[(Rings + 1) * (Segments + 1)];
            var uvs = new Vector2[verts.Length];
            for (int r = 0; r <= Rings; r++)
            {
                for (int k = 0; k <= Segments; k++)
                {
                    float ang = k / (float)Segments * Mathf.PI * 2f;
                    float radius = rim[k % Segments] * ringGrow[r];
                    Vector3 p = a.position + axis * (ringAlong[r] * s)
                              + f * (centre.x + Mathf.Cos(ang) * radius)
                              + side * (centre.y + Mathf.Sin(ang) * radius);
                    int i = r * (Segments + 1) + k;
                    verts[i] = a.InverseTransformPoint(p);
                    // the thigh's own area of the skin texture (the cut edge's UVs, measured in Blender),
                    // so the colour matches the leg below it
                    uvs[i] = new Vector2(Mathf.Lerp(0.12f, 0.39f, 0.5f - 0.5f * Mathf.Cos(ang)), 0.41f);
                }
            }
            var tris = new int[Rings * Segments * 6];
            int n = 0;
            for (int r = 0; r < Rings; r++)
                for (int k = 0; k < Segments; k++)
                {
                    int i0 = r * (Segments + 1) + k, i1 = i0 + 1, j0 = i0 + Segments + 1, j1 = j0 + 1;
                    tris[n++] = i0; tris[n++] = j0; tris[n++] = i1;
                    tris[n++] = i1; tris[n++] = j0; tris[n++] = j1;
                }
            var mesh = new Mesh { name = "ThighFill", vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            // the winding above faces outwards only if side = axis x front; make sure the normals point out
            if (Vector3.Dot(a.TransformDirection(mesh.normals[Segments / 4]), side) < 0f)
            {
                System.Array.Reverse(tris);
                mesh.triangles = tris;
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            var go = new GameObject("ThighFill");
            go.transform.SetParent(a, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = skin;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // the hips under the skirt (also cut away): a skirt-coloured inner piece closes the crotch
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips != null && skirt != null && hips.Find("UnderSkirt") == null)
        {
            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.name = "UnderSkirt";
            Destroy(inner.GetComponent<Collider>());
            inner.transform.SetParent(hips, false);
            inner.transform.position = hips.position - Vector3.up * 0.035f * s;
            inner.transform.rotation = animator.transform.rotation;
            Vector3 size = new Vector3(0.27f, 0.13f, 0.2f) * s;
            Vector3 parent = hips.lossyScale;
            inner.transform.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
            var ir = inner.GetComponent<Renderer>();
            ir.sharedMaterial = skirt;
            ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
