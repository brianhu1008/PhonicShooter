using UnityEngine;

// 中文字型與 3D 文字：遊樂園、注音、九九乘法共用
public static class ZhText
{
    static Font font;
    static Material material;

    public static Font Font
    {
        get
        {
            if (font != null) return font;
#if UNITY_WEBGL && !UNITY_EDITOR
            // 網頁版 (iPad Safari) 用不到電腦的字型，用打包進來的 Noto Sans TC
            font = Resources.Load<Font>("Fonts/NotoSansTC-Regular");
#else
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "PMingLiU", "Microsoft YaHei", "Arial Unicode MS" }, 64);
#endif
            // 動態字型新增字時貼圖會換掉，跟著更新
            Font.textureRebuilt += f =>
            {
                if (f == font && material != null && material != f.material) material.mainTexture = f.material.mainTexture;
            };
            return font;
        }
    }

    // 3D 文字用的材質：會被前面的物體擋住 (內建字型材質會穿透)
    public static Material Material
    {
        get
        {
            if (material == null)
            {
                var template = Resources.Load<Material>("TextMat");
                material = template != null ? new Material(template) : Font.material;
            }
            if (material != Font.material && material.mainTexture != Font.material.mainTexture)
                material.mainTexture = Font.material.mainTexture;
            return material;
        }
    }

    // 建立一段 3D 文字，會自動縮放到 maxWidth x maxHeight 以內
    public static TextMesh Make(Transform parent, Vector3 localPos, Quaternion localRot, string text, Color color, float maxWidth, float maxHeight)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one * 0.05f;
        var tm = go.AddComponent<TextMesh>();
        tm.font = Font;
        tm.fontSize = 100;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = text;
        go.GetComponent<MeshRenderer>().sharedMaterial = Material;
        var fit = go.AddComponent<TextFit>();
        fit.width = maxWidth;
        fit.height = maxHeight;
        return tm;
    }

    public static void SetText(TextMesh tm, string text)
    {
        tm.text = text;
        var fit = tm.GetComponent<TextFit>();
        if (fit != null) fit.Refit();
    }
}

// 文字網格產生後，把字縮放到指定大小
public class TextFit : MonoBehaviour
{
    public float width = 1f, height = 1f;
    Renderer r;
    int wait;
    bool done;

    void Awake() => r = GetComponent<Renderer>();

    public void Refit()
    {
        done = false;
        wait = 1;
    }

    void LateUpdate()
    {
        if (done) return;
        if (wait > 0) { wait--; return; }
        var size = r.localBounds.size;
        if (size.x < 0.0001f || size.y < 0.0001f) return;
        transform.localScale = Vector3.one * Mathf.Min(width / size.x, height / size.y);
        done = true;
    }
}
