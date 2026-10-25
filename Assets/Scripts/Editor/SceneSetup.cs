using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 第一次打開專案時自動建立 Assets/Scenes/Main.unity 並加入 Build Settings。
// 場景本身是空的，按下 Play 後 GameManager 會用程式把整個遊戲建出來。
[InitializeOnLoad]
public static class SceneSetup
{
    public const string ScenePath = "Assets/Scenes/Main.unity";

    static SceneSetup()
    {
        EditorApplication.delayCall += EnsureScene;
    }

    const string BaseMatPath = "Assets/Resources/BaseMat.mat";

    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureBaseMaterial();
        if (File.Exists(ScenePath)) return;
        CreateAndOpen();
    }

    // 建立一個用 Standard shader 的材質放在 Resources，建置時 shader 才不會被剔除
    public static void EnsureBaseMaterial()
    {
        Directory.CreateDirectory("Assets/Resources");
        if (!File.Exists(BaseMatPath))
        {
            var mat = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, BaseMatPath);
        }
        // 牌子上的字用的材質 (會被前面的物體擋住)
        if (!File.Exists(TextMatPath))
        {
            var shader = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>("Assets/Shaders/Text3D.shader");
            if (shader != null) AssetDatabase.CreateAsset(new UnityEngine.Material(shader), TextMatPath);
        }
        AssetDatabase.SaveAssets();
    }

    const string TextMatPath = "Assets/Resources/TextMat.mat";

    [MenuItem("注音怪獸/開啟遊戲場景")]
    static void OpenScene()
    {
        if (!File.Exists(ScenePath)) { CreateAndOpen(); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    public static void CreateAndOpen()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
    }
}
