using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared look for the runtime UI (dialogue box, phone, timer, game over):
/// the Sarabun font (OFL, Resources/Fonts), the pearl / gold / night-blue palette of the title screen,
/// and small procedural sprites (soft panels, gradient bands, glows) so no extra image files are needed.
/// </summary>
public static class UiTheme
{
    public static readonly Color Gold = new Color(0.95f, 0.82f, 0.55f);
    public static readonly Color GoldDeep = new Color(0.78f, 0.6f, 0.32f);
    public static readonly Color Pearl = new Color(0.96f, 0.96f, 0.99f);
    public static readonly Color Night = new Color(0.06f, 0.07f, 0.13f);
    public static readonly Color Ink = new Color(0.2f, 0.23f, 0.33f);

    static TMP_FontAsset regular, bold;
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static TMP_FontAsset Regular => regular != null ? regular : regular = Load("Fonts/Sarabun-Regular");
    public static TMP_FontAsset Bold => bold != null ? bold : bold = Load("Fonts/Sarabun-Bold");

    static TMP_FontAsset Load(string path)
    {
        var font = Resources.Load<Font>(path);
        if (font == null) return TMP_Settings.defaultFontAsset;
        var asset = TMP_FontAsset.CreateFontAsset(font);
        if (asset == null) return TMP_Settings.defaultFontAsset;
        asset.name = font.name + " (runtime)";
        // characters the font does not have (arrows, symbols) fall back to the default TMP font
        if (TMP_Settings.defaultFontAsset != null)
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        // render every everyday character into the atlas now (at load, behind the black fade): added one
        // by one as new letters first appear, they stalled a frame (the title's "Click to Continue")
        var common = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) common.Append(c);
        common.Append("…‘’“”—–•·°×");
        asset.TryAddCharacters(common.ToString());
        return asset;
    }

    /// <summary>Applies the theme font and a soft shadow so text reads on any background.</summary>
    public static void Style(TMP_Text t, bool isBold, float size, Color color, float shadow = 0.6f)
    {
        if (t == null) return;
        t.font = isBold ? Bold : Regular;
        t.fontSize = size;
        t.color = color;
        if (shadow > 0f)
        {
            var mat = new Material(t.font.material);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, shadow));
            mat.SetFloat("_UnderlayOffsetX", 0.4f);
            mat.SetFloat("_UnderlayOffsetY", -0.4f);
            mat.SetFloat("_UnderlaySoftness", 0.5f);
            t.fontMaterial = mat;
        }
    }

    /// <summary>Rounded rectangle with soft edges, usable as a 9-slice sprite.</summary>
    public static Sprite RoundedRect(int radius = 24, float softness = 2f)
    {
        string key = $"round{radius}_{softness}";
        if (sprites.TryGetValue(key, out var s)) return s;
        int size = radius * 2 + 4;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius - 2));
                float dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius - 2));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((radius - d) / softness);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
        return sprites[key] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    /// <summary>Horizontal band that fades out at the left/right ends and towards the top (dialogue backdrop).</summary>
    public static Sprite DialogueBand()
    {
        if (sprites.TryGetValue("band", out var s)) return s;
        int w = 512, h = 128;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                float side = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.09f);
                float vert = Mathf.Lerp(1f, 0.75f, v) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.15f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * side * vert));
            }
        tex.SetPixels32(px); tex.Apply();
        return sprites["band"] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Thin line that fades at both ends (gold dividers).</summary>
    public static Sprite FadeLine()
    {
        if (sprites.TryGetValue("line", out var s)) return s;
        int w = 256, h = 4;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.35f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
        tex.SetPixels32(px); tex.Apply();
        return sprites["line"] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Full-screen frame that is transparent in the middle and solid at the edges (timer warning).</summary>
    public static Sprite EdgeGlow()
    {
        if (sprites.TryGetValue("edge", out var s)) return s;
        int w = 256, h = 144;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x / (w - 1f)) * 2f - 1f, v = (y / (h - 1f)) * 2f - 1f;
                float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                float a = Mathf.SmoothStep(0f, 1f, (r - 0.55f) / 0.75f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
        tex.SetPixels32(px); tex.Apply();
        return sprites["edge"] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Small diamond (continue marker).</summary>
    public static Sprite Diamond()
    {
        if (sprites.TryGetValue("diamond", out var s)) return s;
        int n = 32;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x + 0.5f - n / 2f) + Mathf.Abs(y + 0.5f - n / 2f);
                float a = Mathf.Clamp01((n / 2f - 1f - d) / 1.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
        tex.SetPixels32(px); tex.Apply();
        return sprites["diamond"] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Loads a PNG from Resources as a sprite (works without changing the texture import type).</summary>
    public static Sprite FromResources(string path)
    {
        if (sprites.TryGetValue(path, out var s)) return s;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return sprites[path] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
}
