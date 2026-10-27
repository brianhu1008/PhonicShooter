using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 自動建立三個空場景並加入 Build Settings (第一個是遊戲開啟時的入口)：
//   Park.unity     兒童樂園
//   Main.unity     注音摩天輪
//   Multiply.unity 九九乘法戰機
// 場景本身是空的，按下 Play 後 AppBoot 會依場景名稱用程式建出整個遊戲。
[InitializeOnLoad]
public static class SceneSetup
{
    public const string ParkPath = "Assets/Scenes/Park.unity";
    public const string ScenePath = "Assets/Scenes/Main.unity";
    public const string MultiplyPath = "Assets/Scenes/Multiply.unity";
    public const string IslandPath = "Assets/Scenes/Island.unity"; // 卡皮巴拉發音島
    public static readonly string[] AllScenes = { ParkPath, ScenePath, MultiplyPath, IslandPath };

    const string BaseMatPath = "Assets/Resources/BaseMat.mat";
    const string TextMatPath = "Assets/Resources/TextMat.mat";
    const string TerrainMatPath = "Assets/Resources/TerrainMat.mat";

    static SceneSetup()
    {
        EditorApplication.delayCall += EnsureScenes;
    }

    static void EnsureScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureBaseMaterial();
        EnsureAllScenes();
    }

    // 建置時要用到的材質放在 Resources，shader 才不會被剔除
    public static void EnsureBaseMaterial()
    {
        Directory.CreateDirectory("Assets/Resources");
        if (!File.Exists(BaseMatPath))
            AssetDatabase.CreateAsset(new UnityEngine.Material(UnityEngine.Shader.Find("Standard")), BaseMatPath);
        // 牌子上的字用的材質 (會被前面的物體擋住)
        CreateShaderMaterial("Assets/Shaders/Text3D.shader", TextMatPath);
        // 九九乘法的地形 (頂點顏色)
        CreateShaderMaterial("Assets/Shaders/VertexColorLit.shader", TerrainMatPath);
        AssetDatabase.SaveAssets();
    }

    static void CreateShaderMaterial(string shaderPath, string matPath)
    {
        if (File.Exists(matPath)) return;
        var shader = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(shaderPath);
        if (shader != null) AssetDatabase.CreateAsset(new UnityEngine.Material(shader), matPath);
    }

    // 建立缺少的場景並設定 Build Settings；回傳是否有新建
    public static bool EnsureAllScenes()
    {
        Directory.CreateDirectory("Assets/Scenes");
        bool created = false;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in AllScenes)
        {
            if (File.Exists(path)) continue;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            created = true;
        }
        var list = new EditorBuildSettingsScene[AllScenes.Length];
        for (int i = 0; i < AllScenes.Length; i++) list[i] = new EditorBuildSettingsScene(AllScenes[i], true);
        EditorBuildSettings.scenes = list;
        if (created)
        {
            AssetDatabase.Refresh();
            if (!UnityEngine.Application.isBatchMode)
                EditorSceneManager.OpenScene(string.IsNullOrEmpty(active) || !File.Exists(active) ? ParkPath : active);
        }
        return created;
    }

    [MenuItem("注音怪獸/開啟兒童樂園場景")]
    static void OpenPark()
    {
        EnsureAllScenes();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ParkPath);
    }
}
