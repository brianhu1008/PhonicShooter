using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 建置 Windows 版到 Build/PhonicShooter.exe，網頁版 (iPad Safari) 到 Build/WebGL
// 命令列：Unity.exe -batchmode -quit -projectPath <專案> -executeMethod BuildScript.BuildWindows
//         Unity.exe -batchmode -quit -projectPath <專案> -buildTarget WebGL -executeMethod BuildScript.BuildWebGL
public static class BuildScript
{
    const string WindowsOutput = "Build/PhonicShooter.exe";
    const string WebOutput = "Build/WebGL";
    const string BundledVoicesDir = "Assets/Resources/Voices";

    [MenuItem("注音怪獸/建置 Windows 版")]
    public static void BuildWindows()
    {
        Prepare();
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = false;
        Build(WindowsOutput, BuildTarget.StandaloneWindows64);
    }

    [MenuItem("注音怪獸/建置網頁版 (iPad)")]
    public static void BuildWebGL()
    {
        Prepare();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // 任何網站都能放，不用設定伺服器
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "PROJECT:PhonicFull"; // 填滿整個畫面 (Assets/WebGLTemplates/PhonicFull)
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = false;

        // 網頁版不能錄音：把電腦上錄好的注音一起打包
        CopyRecordedVoices();
        try { Build(WebOutput, BuildTarget.WebGL); }
        finally { AssetDatabase.DeleteAsset(BundledVoicesDir); }
    }

    static void Prepare()
    {
        SceneSetup.EnsureBaseMaterial();
        AudioTrimmer.Trim(false); // 辭典錄音只留字音 (不念部首、筆畫)
        if (!File.Exists(SceneSetup.ScenePath)) SceneSetup.CreateAndOpen();
        PlayerSettings.productName = "注音怪獸大作戰";
        PlayerSettings.companyName = "PhonicGame";
        PlayerSettings.SplashScreen.show = false;
    }

    static void CopyRecordedVoices()
    {
        string src = Path.Combine(Application.persistentDataPath, "voices");
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(BundledVoicesDir);
        int n = 0;
        foreach (var f in Directory.GetFiles(src, "*.wav"))
        {
            File.Copy(f, Path.Combine(BundledVoicesDir, Path.GetFileName(f)), true);
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[BuildScript] 打包 {n} 個家長錄音 ({src})");
    }

    static void Build(string output, BuildTarget target)
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { SceneSetup.ScenePath },
            locationPathName = output,
            target = target,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[BuildScript] Result: " + report.summary.result + ", errors: " + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
