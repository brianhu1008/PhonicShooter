using UnityEngine;

// 用內建幾何形狀拼出模型的小工具
public static class Shapes
{
    public const int IgnoreRaycastLayer = 2;

    // 網頁版建置會剔除沒被程式直接用到的引擎類別；CreatePrimitive 需要這些碰撞元件，
    // 在這裡直接引用，避免每產生一個物件就印一行「class doesn't exist」錯誤
    [UnityEngine.Scripting.Preserve]
    static readonly System.Type[] KeepColliders = { typeof(SphereCollider), typeof(CapsuleCollider), typeof(BoxCollider), typeof(MeshCollider) };

    [UnityEngine.Scripting.Preserve, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void TouchColliders()
    {
        // 實際用一次，確保建置工具保留這些類別
        var go = new GameObject("KeepColliders");
        go.AddComponent<SphereCollider>();
        go.AddComponent<CapsuleCollider>();
        Object.Destroy(go);
        if (KeepColliders.Length == 0) Debug.Log("never");
    }

    // Resources/BaseMat.mat (由編輯器腳本建立) 讓 Standard shader 被包進建置檔，
    // 否則執行檔裡的物件會變成粉紅色
    static Material baseMat;
    static bool baseMatLoaded;

    public static void SetColor(Renderer r, Color color)
    {
        if (!baseMatLoaded)
        {
            baseMat = Resources.Load<Material>("BaseMat");
            baseMatLoaded = true;
        }
        var mat = baseMat != null ? new Material(baseMat) : r.material;
        mat.color = color;
        r.material = mat;
    }

    public static GameObject Primitive(PrimitiveType type, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        SetColor(go.GetComponent<Renderer>(), color);
        return go;
    }

    public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool keepCollider = false)
    {
        var go = Primitive(type, color);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
        return go;
    }
}
