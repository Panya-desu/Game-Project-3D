using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Builds the "white world" look shared by the title screen and the White Space:
/// a sea of shallow water that mirrors everything, the Gate of Memories, floating glass shards
/// holding memory images (cut from the cover art), drifting light motes, light rays, bloom and fog.
///   Tools/Environment/Build Title Screen   -> Assets/Scenes/Main.unity
///   Tools/Environment/Dress White Space    -> Assets/Scenes/WhiteSpace.unity (+ Confrontation)
/// Everything generated lives under "__Environment" (and Assets/Art/Generated) so it can be rebuilt.
/// </summary>
public static class EnvironmentBuilder
{
    const string Root = "__Environment";
    const string ArtDir = "Assets/Art/Generated";
    const string MatDir = "Assets/Art/Generated/Materials";
    const string MeshDir = "Assets/Art/Generated/Meshes";
    const string GatePrefab = "Assets/Art/Generated/GateOfMemories.prefab";
    const string VolumePath = "Assets/Art/Generated/WhiteWorldVolume.asset";

    static readonly Color Sky = new Color(0.91f, 0.93f, 0.98f);

    [MenuItem("Tools/Environment/Build All")]
    public static void BuildAll()
    {
        PrepareAssets();
        BuildTitle();
        DressWhiteSpace();
        DressConfrontation();
        AssetDatabase.SaveAssets();
        Debug.Log("[EnvironmentBuilder] done");
    }

    // ───────────────────────── shared assets ─────────────────────────

    static Material pearl, aura;
    static Material water, marble, gold, portal, portalDim, glass, crystalGlow, mote, ray, haze;
    static readonly List<Material> memories = new List<Material>();
    static readonly List<Mesh> shardMeshes = new List<Mesh>();
    static Mesh crystalMesh;
    static VolumeProfile volume;
    static Material skybox;

    [MenuItem("Tools/Environment/Prepare Assets")]
    public static void PrepareAssets()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(MeshDir);
        ConfigureTextures();

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        water = Mat("Water", lit, new Color(0.7f, 0.78f, 0.9f, 0.42f), 0.97f, 0f, transparent: true);
        water.SetTexture("_BumpMap", Tex("water_normal.png"));
        water.SetFloat("_BumpScale", 0.25f);
        water.EnableKeyword("_NORMALMAP");
        // no glint of the sun: on this mirror-smooth water it was a bright blob that followed the player
        // around whenever they looked down (the reflections of the gates and shards stay)
        water.SetFloat("_SpecularHighlights", 0f);
        water.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        water.SetTextureScale("_BaseMap", new Vector2(30, 30));
        water.SetTextureScale("_BumpMap", new Vector2(30, 30));
        // Drawn after opaque objects (so it covers the mirrored gate) but before other transparent things
        water.renderQueue = (int)RenderQueue.Transparent - 50;

        marble = Mat("Marble", lit, new Color(0.95f, 0.95f, 0.97f), 0.85f, 0f);
        pearl = Mat("Pearl", lit, new Color(0.84f, 0.87f, 0.94f), 0.92f, 0.15f);

        gold = Mat("Gold", lit, new Color(1f, 0.82f, 0.52f), 0.85f, 1f);
        portal = Mat("Portal", lit, Color.white, 0.5f, 0f, emission: new Color(3.2f, 3.4f, 3.8f));
        portalDim = Mat("PortalDim", lit, new Color(0.8f, 0.82f, 0.88f), 0.5f, 0f, emission: new Color(0.35f, 0.37f, 0.42f));
        glass = Mat("Glass", lit, new Color(0.8f, 0.88f, 1f, 0.38f), 1f, 0.3f, transparent: true, emission: new Color(0.45f, 0.48f, 0.55f));
        glass.SetTexture("_EmissionMap", Tex("iridescent.png"));
        crystalGlow = Mat("CrystalGlow", lit, new Color(0.9f, 0.95f, 1f, 0.6f), 1f, 0f, transparent: true, emission: new Color(1.4f, 1.6f, 2.0f));

        memories.Clear();
        for (int i = 1; i <= 5; i++)
        {
            var m = Mat("Memory" + i, unlit, new Color(1f, 1f, 1f, 0.9f), 0f, 0f, transparent: true);
            m.SetTexture("_BaseMap", Tex($"memory_{i}.png"));
            memories.Add(m);
        }

        aura = Mat("Aura", particles, new Color(1f, 0.98f, 0.92f, 0.55f), 0, 0, transparent: true, additive: true);
        aura.SetTexture("_BaseMap", Tex("glow_dot.png"));
        mote = Mat("Mote", particles, new Color(1f, 0.97f, 0.88f, 0.85f), 0, 0, transparent: true, additive: true);
        mote.SetTexture("_BaseMap", Tex("glow_dot.png"));
        ray = Mat("LightRay", particles, new Color(1f, 0.97f, 0.9f, 0.22f), 0, 0, transparent: true, additive: true);
        ray.SetTexture("_BaseMap", Tex("light_ray.png"));
        haze = Mat("Haze", unlit, new Color(1f, 1f, 1f, 0.75f), 0, 0, transparent: true);
        haze.SetTexture("_BaseMap", Tex("horizon_haze.png"));

        shardMeshes.Clear();
        for (int i = 0; i < 5; i++) shardMeshes.Add(SaveMesh("Shard" + i, BuildShardMesh(100 + i)));
        crystalMesh = SaveMesh("Crystal", BuildCrystalMesh(7));

        skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Sky.mat");
        if (skybox == null) { skybox = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(skybox, $"{MatDir}/Sky.mat"); }
        skybox.shader = Shader.Find("Skybox/Panoramic");
        skybox.SetTexture("_MainTex", Tex("sky_gradient.png"));
        skybox.SetFloat("_Exposure", 1.0f);
        skybox.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(skybox);

        BuildVolume();
        BuildGatePrefab();
        AssetDatabase.SaveAssets();
    }

    static void ConfigureTextures()
    {
        foreach (var f in Directory.GetFiles(ArtDir, "*.png"))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(f.Replace('\\', '/'));
            if (imp == null) continue;
            bool normal = f.EndsWith("water_normal.png");
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.alphaIsTransparency = !normal;
            imp.wrapMode = normal || f.EndsWith("iridescent.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (f.EndsWith("sky_gradient.png")) { imp.mipmapEnabled = false; imp.alphaIsTransparency = false; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp; }
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{name}");

    static Material Mat(string name, Shader shader, Color color, float smooth, float metal,
                        bool transparent = false, Color? emission = null, bool additive = false)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // glass and memories are visible from both sides
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = $"{MeshDir}/{name}.asset";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>Jagged flat glass shard: front/back faces (submesh 0, picture) and bevelled sides (submesh 1, glass).</summary>
    static Mesh BuildShardMesh(int seed)
    {
        var rnd = new System.Random(seed);
        int n = 7;
        var outline = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.5f;
            float r = 0.7f + (float)rnd.NextDouble() * 0.35f;
            outline[i] = new Vector2(Mathf.Cos(a) * r * 0.78f, Mathf.Sin(a) * r);
        }
        float th = 0.05f;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var faces = new List<int>(); var sides = new List<int>();
        Vector2 Uv(Vector2 p) => new Vector2(p.x / 2.0f + 0.5f, p.y / 2.2f + 0.5f);
        // front and back fans (slightly domed)
        foreach (float side in new[] { 1f, -1f })
        {
            int c = verts.Count;
            verts.Add(new Vector3(0, 0, th * 1.6f * side)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++) { verts.Add(new Vector3(outline[i].x, outline[i].y, th * side)); uvs.Add(Uv(outline[i])); }
            for (int i = 0; i < n; i++)
            {
                int a = c + 1 + i, b = c + 1 + (i + 1) % n;
                if (side > 0) faces.AddRange(new[] { c, b, a }); else faces.AddRange(new[] { c, a, b });
            }
        }
        // sides
        for (int i = 0; i < n; i++)
        {
            int s = verts.Count;
            Vector2 p = outline[i], q = outline[(i + 1) % n];
            verts.Add(new Vector3(p.x, p.y, th)); verts.Add(new Vector3(q.x, q.y, th));
            verts.Add(new Vector3(q.x, q.y, -th)); verts.Add(new Vector3(p.x, p.y, -th));
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            sides.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
        }
        var mesh = new Mesh { name = "Shard" + seed };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faces, 0); mesh.SetTriangles(sides, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Faceted elongated crystal (flat shaded).</summary>
    static Mesh BuildCrystalMesh(int seed)
    {
        var rnd = new System.Random(seed);
        int n = 6;
        var ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            float r = 0.22f + (float)rnd.NextDouble() * 0.08f;
            ring[i] = new Vector3(Mathf.Cos(a) * r, (float)rnd.NextDouble() * 0.1f, Mathf.Sin(a) * r);
        }
        var top = new Vector3(0, 1.0f, 0); var bottom = new Vector3(0, -0.45f, 0);
        var verts = new List<Vector3>(); var tris = new List<int>();
        for (int i = 0; i < n; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % n];
            int v = verts.Count; verts.AddRange(new[] { a, top, b }); tris.AddRange(new[] { v, v + 1, v + 2 });
            v = verts.Count; verts.AddRange(new[] { a, b, bottom }); tris.AddRange(new[] { v, v + 1, v + 2 });
        }
        var mesh = new Mesh { name = "Crystal" };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static void BuildVolume()
    {
        volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (volume != null) AssetDatabase.DeleteAsset(VolumePath);
        volume = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(volume, VolumePath);

        var bloom = volume.Add<Bloom>(true);
        bloom.threshold.Override(0.95f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.97f, 0.93f));
        var vig = volume.Add<Vignette>(true);
        vig.intensity.Override(0.22f); vig.smoothness.Override(0.5f); vig.color.Override(new Color(0.35f, 0.4f, 0.55f));
        var ca = volume.Add<ColorAdjustments>(true);
        ca.postExposure.Override(-0.05f); ca.contrast.Override(12f); ca.saturation.Override(4f);
        var tm = volume.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.Neutral);
        foreach (var c in volume.components) AssetDatabase.AddObjectToAsset(c, volume);
        EditorUtility.SetDirty(volume);
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>
    /// The Gate of Memories: marble pillars and arch with gold trim, two door leaves on pivots,
    /// a glowing portal and a crystal keystone. About 8.6 m tall at scale 1.
    /// </summary>
    static void BuildGatePrefab()
    {
        var root = new GameObject("GateOfMemories");
        var t = root.transform;
        // steps
        Prim(PrimitiveType.Cube, t, "Step1", new Vector3(0, 0.1f, 0), new Vector3(6.6f, 0.2f, 2.4f), marble);
        Prim(PrimitiveType.Cube, t, "Step2", new Vector3(0, 0.3f, 0), new Vector3(5.8f, 0.2f, 1.8f), marble);
        // pillars + capitals
        foreach (float x in new[] { -2.25f, 2.25f })
        {
            Prim(PrimitiveType.Cube, t, "Pillar", new Vector3(x, 3.3f, 0), new Vector3(0.7f, 6.0f, 0.8f), marble);
            Prim(PrimitiveType.Cube, t, "Base", new Vector3(x, 0.55f, 0), new Vector3(0.95f, 0.3f, 1.0f), gold);
            Prim(PrimitiveType.Cube, t, "Capital", new Vector3(x, 6.35f, 0), new Vector3(0.95f, 0.25f, 1.0f), gold);
        }
        // arch: segments on a semicircle (centre y = 6.4, radius 2.25)
        int seg = 13;
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.PI * i / seg;
            var p = new Vector3(Mathf.Cos(a) * 2.25f, 6.4f + Mathf.Sin(a) * 2.25f, 0);
            Prim(PrimitiveType.Cube, t, "Arch", p, new Vector3(0.62f, 0.62f, 0.8f), marble, new Vector3(0, 0, a * Mathf.Rad2Deg));
            var g = new Vector3(Mathf.Cos(a) * 1.88f, 6.4f + Mathf.Sin(a) * 1.88f, 0.05f);
            Prim(PrimitiveType.Cube, t, "ArchTrim", g, new Vector3(0.12f, 0.5f, 0.86f), gold, new Vector3(0, 0, a * Mathf.Rad2Deg));
        }
        // glowing portal: rectangle + half-disc top
        var portalRoot = new GameObject("Portal").transform; portalRoot.SetParent(t, false);
        Prim(PrimitiveType.Cube, portalRoot, "PortalRect", new Vector3(0, 3.4f, 0.12f), new Vector3(3.8f, 6.1f, 0.04f), portal);
        Prim(PrimitiveType.Cylinder, portalRoot, "PortalTop", new Vector3(0, 6.4f, 0.12f), new Vector3(3.8f, 0.02f, 3.8f), portal, new Vector3(90, 0, 0));
        // door leaves on pivots at the pillar edges (closed); they swing toward the camera side (-Z)
        foreach (int side in new[] { -1, 1 })
        {
            var pivot = new GameObject(side < 0 ? "LeafPivotL" : "LeafPivotR").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(1.9f * side, 0.4f, -0.12f);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(pivot, false);
            leaf.localPosition = new Vector3(-0.95f * side, 3.0f, 0);
            Prim(PrimitiveType.Cube, leaf, "Panel", Vector3.zero, new Vector3(1.88f, 6.0f, 0.14f), pearl);
            // gold border
            Prim(PrimitiveType.Cube, leaf, "BorderL", new Vector3(-0.86f, 0, -0.09f), new Vector3(0.1f, 5.9f, 0.05f), gold);
            Prim(PrimitiveType.Cube, leaf, "BorderR", new Vector3(0.86f, 0, -0.09f), new Vector3(0.1f, 5.9f, 0.05f), gold);
            Prim(PrimitiveType.Cube, leaf, "BorderT", new Vector3(0, 2.92f, -0.09f), new Vector3(1.8f, 0.1f, 0.05f), gold);
            Prim(PrimitiveType.Cube, leaf, "BorderB", new Vector3(0, -2.92f, -0.09f), new Vector3(1.8f, 0.1f, 0.05f), gold);
            // inner panels
            Prim(PrimitiveType.Cube, leaf, "InnerTop", new Vector3(0, 1.45f, -0.08f), new Vector3(1.2f, 2.2f, 0.03f), marble);
            Prim(PrimitiveType.Cube, leaf, "InnerBottom", new Vector3(0, -1.55f, -0.08f), new Vector3(1.2f, 2.0f, 0.03f), marble);
            Prim(PrimitiveType.Cube, leaf, "Rail", new Vector3(0, -0.15f, -0.1f), new Vector3(1.5f, 0.06f, 0.05f), gold);
            // half of the round emblem at the seam (the two leaves form one circle)
            Prim(PrimitiveType.Cylinder, leaf, "Emblem", new Vector3(-0.94f * side, 0.45f, -0.1f), new Vector3(0.75f, 0.02f, 0.75f), gold, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, leaf, "EmblemCore", new Vector3(-0.94f * side, 0.45f, -0.12f), new Vector3(0.42f, 0.02f, 0.42f), portal, new Vector3(90, 0, 0));
        }
        Prim(PrimitiveType.Cube, t, "SeamLight", new Vector3(0, 3.4f, -0.05f), new Vector3(0.05f, 6.0f, 0.05f), portal);
        Prim(PrimitiveType.Cube, t, "ThresholdLight", new Vector3(0, 0.42f, -0.2f), new Vector3(3.6f, 0.03f, 0.3f), portal);
        var halo = Prim(PrimitiveType.Quad, t, "Aura", new Vector3(0, 5.2f, 0.6f), new Vector3(16f, 16f, 1f), aura);
        halo.AddComponent<PulseGlow>().amount = 0.25f;
        // gold rings on the pillars and an outer arch ring
        foreach (float x in new[] { -2.25f, 2.25f })
            foreach (float y in new[] { 1.4f, 5.2f })
                Prim(PrimitiveType.Cube, t, "PillarRing", new Vector3(x, y, 0), new Vector3(0.78f, 0.08f, 0.88f), gold);
        for (int i = 0; i <= 17; i++)
        {
            float a = Mathf.PI * i / 17;
            Prim(PrimitiveType.Cube, t, "OuterRing", new Vector3(Mathf.Cos(a) * 2.62f, 6.4f + Mathf.Sin(a) * 2.62f, 0.02f), new Vector3(0.1f, 0.5f, 0.84f), gold, new Vector3(0, 0, a * Mathf.Rad2Deg));
        }
        // crystal keystone
        var key = new GameObject("Keystone");
        key.transform.SetParent(t, false);
        key.transform.localPosition = new Vector3(0, 8.9f, 0.1f);
        key.transform.localScale = Vector3.one * 0.9f;
        key.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
        key.AddComponent<MeshRenderer>().sharedMaterial = crystalGlow;
        key.AddComponent<FloatingShard>().bobHeight = 0.12f;
        // light spilling out of the portal
        var lgo = new GameObject("PortalLight");
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(0, 3.2f, -1.2f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.range = 14f; l.intensity = 2.5f; l.color = new Color(0.9f, 0.95f, 1f); l.shadows = LightShadows.None;

        PrefabUtility.SaveAsPrefabAsset(root, GatePrefab);
        Object.DestroyImmediate(root);
    }

    // ───────────────────────── scene pieces ─────────────────────────

    static GameObject NewRoot()
    {
        var old = GameObject.Find(Root);
        if (old != null) Object.DestroyImmediate(old);
        return new GameObject(Root);
    }

    static void SetupLighting(Color sunColor, float sunIntensity)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.88f, 0.92f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.86f, 0.88f, 0.94f);
        RenderSettings.ambientGroundColor = new Color(0.7f, 0.72f, 0.8f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Sky;
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 190f;

        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = sunColor;
        sun.intensity = sunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        // the White Space sun stands high: her shadow there is as short as the house lamps' (a lower sun
        // stretched it out to more than her height)
        float elevation = EditorSceneManager.GetActiveScene().name == "WhiteSpace" ? 64f : 38f;
        sun.transform.rotation = Quaternion.Euler(elevation, -28f, 0f);
        RenderSettings.sun = sun;
    }

    static void AddVolume(Transform root)
    {
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (v.isGlobal) v.gameObject.SetActive(false); // the scene's own global volume is replaced
        var go = new GameObject("WhiteWorldVolume");
        go.transform.SetParent(root, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.sharedProfile = volume;
    }

    static void EnablePost(Camera cam)
    {
        if (cam == null) return;
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        EditorUtility.SetDirty(cam);
    }

    static GameObject Water(Transform root, Vector3 centre, float size, Collider reuseFloor = null)
    {
        GameObject floor;
        if (reuseFloor != null)
        {
            floor = reuseFloor.gameObject;
            floor.GetComponent<Renderer>().sharedMaterial = water;
        }
        else
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Water";
            floor.transform.SetParent(root, false);
            floor.transform.position = centre;
            floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = water;
        }
        if (floor.GetComponent<WaterRipple>() == null) floor.AddComponent<WaterRipple>();
        var r = floor.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        return floor;
    }

    static GameObject Shard(Transform parent, int index, Vector3 pos, float scale, bool picture)
    {
        var go = new GameObject(picture ? "MemoryShard" : "GlassShard");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-40f, 40f), Random.Range(-25f, 25f));
        go.AddComponent<MeshFilter>().sharedMesh = shardMeshes[index % shardMeshes.Count];
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { picture ? memories[index % memories.Count] : glass, glass };
        mr.shadowCastingMode = ShadowCastingMode.Off;
        var f = go.AddComponent<FloatingShard>();
        f.bobHeight = 0.25f + Random.value * 0.3f;
        f.bobSpeed = 0.12f + Random.value * 0.1f;
        f.spinSpeed = new Vector3(Random.Range(-3f, 3f), Random.Range(-10f, 10f), Random.Range(-4f, 4f));
        return go;
    }

    static GameObject Crystal(Transform parent, Vector3 pos, float scale, bool floating)
    {
        var go = new GameObject("Crystal");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale * Random.Range(0.9f, 1.6f), scale);
        go.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0, 360f), Random.Range(-25f, 25f));
        go.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Random.value < 0.35f ? crystalGlow : glass;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        if (floating)
        {
            var f = go.AddComponent<FloatingShard>();
            f.bobHeight = 0.15f; f.bobSpeed = 0.15f + Random.value * 0.1f; f.spinSpeed = new Vector3(0, Random.Range(-12f, 12f), 0);
        }
        return go;
    }

    static void Motes(Transform parent, Vector3 centre, Vector3 size, int rate)
    {
        var go = new GameObject("LightMotes");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.98f, 0.9f, 0.9f), new Color(0.85f, 0.92f, 1f, 0.8f));
        main.gravityModifier = -0.01f;
        main.maxParticles = 600;
        main.prewarm = true;
        main.loop = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.2f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mote;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void Ray(Transform parent, Vector3 pos, Vector3 euler, Vector2 size)
    {
        var q = Prim(PrimitiveType.Quad, parent, "LightRay", Vector3.zero, new Vector3(size.x, size.y, 1f), ray);
        q.transform.position = pos;
        q.transform.eulerAngles = euler;
        q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var p = q.AddComponent<PulseGlow>(); p.amount = 0.35f; p.speed = 0.08f;
    }

    static void HorizonHaze(Transform parent, Vector3 centre, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var q = Prim(PrimitiveType.Quad, parent, "Haze", Vector3.zero, new Vector3(radius * 0.9f, radius * 0.18f, 1), haze);
            q.transform.position = centre + new Vector3(Mathf.Sin(a) * radius, radius * 0.06f, Mathf.Cos(a) * radius);
            q.transform.rotation = Quaternion.LookRotation(q.transform.position - centre);
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// Mirror everything under `source` into a (1,-1,1) parent placed at the water height. Copies keep
    /// only meshes and renderers; MirrorFollower keeps moving parts (shards, gate leaves) in sync.
    /// </summary>
    static void BuildReflection(Transform root, Transform source, float waterY)
    {
        var mirror = new GameObject("Reflection").transform;
        mirror.SetParent(root, false);
        mirror.position = new Vector3(source.position.x, waterY - (source.position.y - waterY), source.position.z);
        mirror.localScale = new Vector3(1, -1, 1);
        var copy = Object.Instantiate(source.gameObject, mirror);
        // Gate instances inside the copy must be unpacked before components can be removed
        foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        copy.name = source.name + " (mirror)";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = source.localRotation;
        StripToVisuals(copy.transform, source);
    }

    static void StripToVisuals(Transform copy, Transform src)
    {
        foreach (var c in copy.GetComponents<Component>())
        {
            if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
            Object.DestroyImmediate(c);
        }
        if (copy != null && src != null && copy.parent != null && copy.parent.name != "Reflection")
            copy.gameObject.AddComponent<MirrorFollower>().source = src;
        for (int i = 0; i < copy.childCount && i < src.childCount; i++)
            StripToVisuals(copy.GetChild(i), src.GetChild(i));
        var mr = copy.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.sharedMaterials = mr.sharedMaterials.Select(MirrorVariant).ToArray();
        }
    }

    static readonly Dictionary<Material, Material> mirrorMats = new Dictionary<Material, Material>();

    /// <summary>Transparent materials seen in the water get half the opacity (reflections are fainter).</summary>
    static Material MirrorVariant(Material m)
    {
        if (m == null || m.renderQueue < (int)RenderQueue.Transparent) return m;
        if (mirrorMats.TryGetValue(m, out var v) && v != null) return v;
        string path = $"{MatDir}/{m.name}_Reflection.mat";
        v = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (v == null) { v = new Material(m); AssetDatabase.CreateAsset(v, path); }
        else v.CopyPropertiesFromMaterial(m);
        var c = m.GetColor("_BaseColor"); c.a *= 0.45f; v.SetColor("_BaseColor", v.HasProperty("_BaseColor") ? c : c);
        if (v.IsKeywordEnabled("_EMISSION")) v.SetColor("_EmissionColor", m.GetColor("_EmissionColor") * 0.45f);
        v.renderQueue = m.renderQueue;
        EditorUtility.SetDirty(v);
        mirrorMats[m] = v;
        return v;
    }

    static void ScatterShards(Transform parent, Vector3 centre, float radiusMin, float radiusMax, float yMin, float yMax, int count, int pictures, float scaleMin, float scaleMax, System.Func<Vector3, bool> avoid = null)
    {
        int placed = 0, tries = 0, pics = 0;
        while (placed < count && tries++ < 500)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Lerp(radiusMin, radiusMax, Mathf.Sqrt(Random.value));
            var p = centre + new Vector3(Mathf.Sin(a) * r, Random.Range(yMin, yMax), Mathf.Cos(a) * r);
            if (avoid != null && avoid(p)) continue;
            bool pic = pics < pictures;
            Shard(parent, placed, p, Random.Range(scaleMin, scaleMax) * (pic ? 1.4f : 1f), pic);
            if (pic) pics++;
            placed++;
        }
    }

    // ───────────────────────── Title screen ─────────────────────────

    [MenuItem("Tools/Environment/Build Title Screen")]
    public static void BuildTitle()
    {
        if (water == null) PrepareAssets();
        Random.InitState(2026);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        var root = NewRoot().transform;
        SetupLighting(new Color(1f, 0.97f, 0.92f), 1.15f);
        AddVolume(root);

        var world = new GameObject("World").transform; world.SetParent(root, false);
        Water(root, Vector3.zero, 600f);

        var gate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefab));
        gate.transform.SetParent(world, false);
        gate.transform.position = new Vector3(4.8f, 0f, 1f);
        gate.transform.rotation = Quaternion.Euler(0, -8f, 0);

        // memory shards around and above the gate, like the cover art
        ScatterShards(world, new Vector3(3f, 0, 3f), 5f, 15f, 3.5f, 11f, 16, 5, 0.8f, 1.5f, p => (Mathf.Abs(p.x - 4.8f) < 3.6f && p.z < 5f) || (p.x < 0f && p.y > 5.5f && p.z < 3f));
        for (int i = 0; i < 18; i++)
            Crystal(world, new Vector3(Random.Range(-18f, 22f), Random.Range(0.2f, 0.6f), Random.Range(4f, 30f)), Random.Range(0.5f, 1.6f), false);
        for (int i = 0; i < 10; i++)
            Crystal(world, new Vector3(Random.Range(-10f, 14f), Random.Range(2f, 8f), Random.Range(-2f, 12f)), Random.Range(0.15f, 0.35f), true);

        BuildReflection(root, world, 0f);

        var fx = new GameObject("Atmosphere").transform; fx.SetParent(root, false);
        Motes(fx, new Vector3(2f, 4f, 0f), new Vector3(30f, 8f, 24f), 35);
        Ray(fx, new Vector3(-1f, 9f, 6f), new Vector3(0, -10, -22), new Vector2(3.5f, 22f));
        Ray(fx, new Vector3(5f, 10f, 8f), new Vector3(0, 8, 18), new Vector2(4.5f, 24f));
        Ray(fx, new Vector3(10f, 9f, 10f), new Vector3(0, 15, 26), new Vector2(3f, 20f));
        HorizonHaze(fx, new Vector3(2f, 0f, 0f), 90f);

        // camera
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        cam.transform.position = new Vector3(-1.2f, 1.9f, -14f);
        cam.transform.rotation = Quaternion.LookRotation(new Vector3(1.2f, 4.3f, 0f) - cam.transform.position);
        cam.fieldOfView = 48f;
        cam.farClipPlane = 600f;
        EnablePost(cam);
        var fly = new GameObject("FlyTarget").transform; fly.SetParent(root, false);
        fly.position = gate.transform.TransformPoint(new Vector3(0, 3.3f, 1.5f));
        fly.rotation = gate.transform.rotation;

        // UI
        var oldCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.transform.parent == null && c.gameObject.name == "Canvas");
        if (oldCanvas != null) oldCanvas.gameObject.SetActive(false); // static cover + old buttons
        var title = BuildTitleUI(root);

        var ts = new GameObject("TitleScreen").AddComponent<TitleScreen>();
        ts.transform.SetParent(root, false);
        ts.cameraRig = cam.transform;
        ts.gateLeafLeft = gate.transform.Find("LeafPivotL");
        ts.gateLeafRight = gate.transform.Find("LeafPivotR");
        ts.gateLight = gate.transform.Find("PortalLight").GetComponent<Light>();
        ts.flyTarget = fly;
        ts.logoGroup = title.logo; ts.clickGroup = title.click; ts.buttonsGroup = title.buttons;
        ts.clickText = title.clickText; ts.newGameButton = title.newGame; ts.exitButton = title.exit;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EnvironmentBuilder] Title screen built");
    }

    struct TitleUI { public CanvasGroup logo, click, buttons; public TMP_Text clickText; public Button newGame, exit; }

    static TMP_Text Text(Transform parent, string name, string text, float size, FontStyles style, Color color, TextAlignmentOptions align,
                         Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        return t;
    }

    static CanvasGroup Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.AddComponent<CanvasGroup>();
    }

    static Button TextButton(Transform parent, string label, Vector2 pos)
    {
        var t = Text(parent, "Button_" + label, label, 30, FontStyles.Normal, new Color(0.15f, 0.18f, 0.28f, 0.9f), TextAlignmentOptions.MidlineRight,
                     new Vector2(1, 0), pos, new Vector2(260, 50));
        t.raycastTarget = true;
        var b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        var c = b.colors; c.normalColor = Color.white; c.highlightedColor = new Color(0.55f, 0.65f, 1f); c.pressedColor = new Color(0.4f, 0.5f, 0.9f); b.colors = c;
        return b;
    }

    static TitleUI BuildTitleUI(Transform root)
    {
        var go = new GameObject("TitleCanvas");
        go.transform.SetParent(root, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var ink = new Color(0.1f, 0.13f, 0.22f);

        var ui = new TitleUI();
        ui.logo = Group(go.transform, "Logo");
        Text(ui.logo.transform, "Title", "ECHO OF MEMORIES", 104, FontStyles.Bold | FontStyles.Italic, ink, TextAlignmentOptions.TopLeft,
             new Vector2(0, 1), new Vector2(120, -110), new Vector2(1300, 140));
        var line = new GameObject("Line", typeof(RectTransform)); line.transform.SetParent(ui.logo.transform, false);
        var img = line.AddComponent<Image>(); img.color = new Color(0.1f, 0.13f, 0.22f, 0.55f); img.raycastTarget = false;
        var lrt = img.rectTransform; lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0, 1); lrt.anchoredPosition = new Vector2(126, -250); lrt.sizeDelta = new Vector2(560, 3);
        Text(ui.logo.transform, "Subtitle", "a narrative puzzle game of lost memories", 32, FontStyles.Italic, new Color(0.18f, 0.22f, 0.32f, 0.9f), TextAlignmentOptions.TopLeft,
             new Vector2(0, 1), new Vector2(126, -266), new Vector2(1000, 50));
        Text(ui.logo.transform, "Credit", "Prototype  |  CP354 771  |  College of Computing, KKU", 20, FontStyles.Normal, new Color(0.2f, 0.24f, 0.34f, 0.75f), TextAlignmentOptions.BottomLeft,
             new Vector2(0, 0), new Vector2(40, 30), new Vector2(900, 40));

        ui.click = Group(go.transform, "Click");
        ui.clickText = Text(ui.click.transform, "ClickToStart", "Click to Start", 36, FontStyles.Normal, ink, TextAlignmentOptions.Center,
                            new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(800, 60));
        ui.clickText.characterSpacing = 12f;

        ui.buttons = Group(go.transform, "Buttons");
        ui.newGame = TextButton(ui.buttons.transform, "New Game", new Vector2(-60, 100));
        ui.exit = TextButton(ui.buttons.transform, "Exit", new Vector2(-60, 45));
        return ui;
    }

    // ───────────────────────── White Space ─────────────────────────

    [MenuItem("Tools/Environment/Dress White Space")]
    public static void DressWhiteSpace()
    {
        if (water == null) PrepareAssets();
        Random.InitState(7);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/WhiteSpace.unity", OpenSceneMode.Single);
        var root = NewRoot().transform;
        SetupLighting(new Color(1f, 0.97f, 0.93f), 1.1f);
        AddVolume(root);

        var floor = GameObject.Find("Floor");
        Water(root, Vector3.zero, 200f, floor != null ? floor.GetComponent<Collider>() : null);

        var world = new GameObject("World").transform; world.SetParent(root, false);
        var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefab);
        var doors = GameObject.Find("Doors");
        if (doors != null)
        {
            float[] xs = { -4.6f, 0f, 4.6f };
            int i = 0;
            foreach (Transform d in doors.transform)
            {
                if (i >= xs.Length) break;
                // keep the door logic (Door, InteractableTask, collider); swap the visuals for a gate
                d.position = new Vector3(xs[i], 0.1f, doors.transform.position.z - 6f);
                foreach (var r in d.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                var box = d.GetComponent<BoxCollider>();
                if (box != null) { box.center = new Vector3(0, 1.75f, 0); box.size = new Vector3(2.2f, 3.5f, 0.8f); }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab);
                g.name = "Gate_" + d.name;
                g.transform.SetParent(world, false);
                g.transform.position = new Vector3(xs[i], 0f, d.position.z);
                g.transform.localScale = Vector3.one * 0.5f;
                bool active = d.GetComponent<Door>() != null && d.GetComponent<InteractableTask>() != null; // the door the first quest uses
                if (active)
                {
                    var l = g.transform.Find("LeafPivotL"); if (l != null) l.localRotation = Quaternion.Euler(0, -75f, 0);
                    var r = g.transform.Find("LeafPivotR"); if (r != null) r.localRotation = Quaternion.Euler(0, 75f, 0);
                }
                if (!active)
                {
                    foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.sharedMaterial == portal) r.sharedMaterial = portalDim;
                    var pl = g.transform.Find("PortalLight"); if (pl != null) pl.GetComponent<Light>().intensity = 0.4f;
                }
                i++;
            }
        }

        var center = new Vector3(0, 0, 4f);
        ScatterShards(world, center, 9f, 28f, 2.5f, 10f, 22, 5, 0.6f, 1.3f, p => Mathf.Abs(p.x) < 8f && p.z < 12f && p.y < 6f);
        for (int k = 0; k < 26; k++)
        {
            var p = new Vector3(Random.Range(-30f, 30f), Random.Range(0.1f, 0.5f), Random.Range(-20f, 40f));
            if (Mathf.Abs(p.x) < 7f && p.z > -6f && p.z < 12f) continue; // keep the walking area clear
            Crystal(world, p, Random.Range(0.5f, 1.8f), false);
        }
        for (int k = 0; k < 12; k++)
            Crystal(world, new Vector3(Random.Range(-12f, 12f), Random.Range(2.5f, 7f), Random.Range(-4f, 20f)), Random.Range(0.12f, 0.3f), true);

        BuildReflection(root, world, 0f);

        var fx = new GameObject("Atmosphere").transform; fx.SetParent(root, false);
        Motes(fx, new Vector3(0, 3f, 4f), new Vector3(28f, 6f, 26f), 40);
        Ray(fx, new Vector3(-6f, 8f, 10f), new Vector3(0, 0, -20), new Vector2(3f, 18f));
        Ray(fx, new Vector3(6f, 8f, 12f), new Vector3(0, 0, 20), new Vector2(3.5f, 18f));
        HorizonHaze(fx, Vector3.zero, 80f);

        var player = Object.FindAnyObjectByType<Movement>();
        if (player != null)
        {
            var pr = player.GetComponent<MeshRenderer>(); if (pr != null) pr.shadowCastingMode = ShadowCastingMode.Off;
            EnablePost(player.GetComponentInChildren<Camera>());
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EnvironmentBuilder] White Space dressed");
    }

    // ───────────────────────── Confrontation ─────────────────────────

    [MenuItem("Tools/Environment/Dress Confrontation")]
    public static void DressConfrontation()
    {
        if (!File.Exists("Assets/Scenes/Confrontation.unity")) return;
        if (water == null) PrepareAssets();
        Random.InitState(11);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Confrontation.unity", OpenSceneMode.Single);
        var root = NewRoot().transform;
        SetupLighting(new Color(1f, 0.96f, 0.92f), 1.0f);
        AddVolume(root);
        var floor = GameObject.Find("Floor");
        Water(root, Vector3.zero, 200f, floor != null ? floor.GetComponent<Collider>() : null);
        var world = new GameObject("World").transform; world.SetParent(root, false);
        // broken shards hanging still: memories that are being held back
        ScatterShards(world, new Vector3(0, 0, 6f), 8f, 24f, 2f, 9f, 18, 5, 0.6f, 1.2f, p => Mathf.Abs(p.x) < 5f && p.z < 12f);
        for (int k = 0; k < 20; k++)
            Crystal(world, new Vector3(Random.Range(-25f, 25f), 0.2f, Random.Range(-10f, 35f)), Random.Range(0.5f, 1.5f), false);
        BuildReflection(root, world, 0f);
        var fx = new GameObject("Atmosphere").transform; fx.SetParent(root, false);
        Motes(fx, new Vector3(0, 3f, 6f), new Vector3(24f, 6f, 24f), 25);
        var player = Object.FindAnyObjectByType<Movement>();
        if (player != null)
        {
            var cam = player.GetComponentInChildren<Camera>();
            EnablePost(cam);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EnvironmentBuilder] Confrontation dressed");
    }
}
