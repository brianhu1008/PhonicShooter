using System.Collections.Generic;
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
        // 剔除沒用到的引擎程式碼 (檔案比較小)；CreatePrimitive 需要的碰撞元件在 Shapes.cs 直接引用保留
        PlayerSettings.stripEngineCode = true;

        // 網頁版不能錄音：把電腦上錄好的注音一起打包
        CopyRecordedVoices();

        // iPad Safari 解不開 Unity 打包的音檔 (EncodingError: Decoding failed)：
        // 聲音全部轉成原始 PCM 資料 (GameAudio 執行時直接建立)，原本的音檔建置時先移出 Resources
        var hidden = ExportWebAudio();
        // 每次都重新產生資料 (不然 Unity 有時會沿用上一次的資料，聲音換了卻沒打包進去)
        try { Build(WebOutput, BuildTarget.WebGL, BuildOptions.CleanBuildCache); }
        finally
        {
            RestoreWebAudio(hidden);
            AssetDatabase.DeleteAsset(BundledVoicesDir);
        }
    }

    const string WebAudioRoot = "Assets/WebAudio";            // 不會進版本控制 (.gitignore)
    const string WebAudioStore = WebAudioRoot + "/Store";      // 平常放這裡 (不是 Resources，不會被打包)
    const string WebAudioActive = WebAudioRoot + "/Resources"; // 建置網頁版時改名成 Resources
    const string HiddenRoot = WebAudioRoot + "/Hidden";
    const int WebRate = 22050;

    // 把每個 Resources 裡的聲音轉成 Pcm/<路徑>.bytes；回傳暫時移走的音檔資料夾 (原位置 → 暫存位置)
    static List<KeyValuePair<string, string>> ExportWebAudio()
    {
        Directory.CreateDirectory(WebAudioStore + "/Pcm");
        var folders = new HashSet<string>();
        int exported = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            int r = path.IndexOf("/Resources/");
            if (r < 0 || path.StartsWith(WebAudioRoot)) continue;
            string rel = Path.ChangeExtension(path.Substring(r + "/Resources/".Length), null);
            string dst = WebAudioStore + "/Pcm/" + rel + ".bytes";
            folders.Add(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(path)) continue;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || !clip.LoadAudioData()) { Debug.LogWarning("[BuildScript] 讀不到聲音 " + path); continue; }
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            // 轉單聲道、22050 Hz
            int frames = clip.samples;
            int outCount = Mathf.Max(1, Mathf.RoundToInt(frames * (float)WebRate / clip.frequency));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            using (var w = new BinaryWriter(File.Create(dst)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("PCM1"));
                w.Write(WebRate);
                w.Write(outCount);
                for (int i = 0; i < outCount; i++)
                {
                    float src = i * (float)clip.frequency / WebRate;
                    int i0 = Mathf.Min(frames - 1, (int)src), i1 = Mathf.Min(frames - 1, i0 + 1);
                    float t = src - i0, v0 = 0f, v1 = 0f;
                    for (int c = 0; c < clip.channels; c++) { v0 += data[i0 * clip.channels + c]; v1 += data[i1 * clip.channels + c]; }
                    float v = Mathf.Lerp(v0, v1, t) / clip.channels;
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
                }
            }
            exported++;
        }
        Debug.Log($"[BuildScript] 網頁版聲音轉成 PCM：新轉 {exported} 個");

        // PCM 資料放進 Resources，原本的音檔資料夾暫時移出 Resources (不要打包兩份)
        AssetDatabase.Refresh();
        AssetDatabase.DeleteAsset(WebAudioActive);
        Check(AssetDatabase.MoveAsset(WebAudioStore, WebAudioActive));
        Directory.CreateDirectory(HiddenRoot);
        AssetDatabase.Refresh();
        var moved = new List<KeyValuePair<string, string>>();
        int n = 0;
        foreach (var folder in folders)
        {
            if (folder.EndsWith("/Resources")) continue; // 不能整個 Resources 移走 (只移裡面的子資料夾)
            string to = HiddenRoot + "/" + (n++) + "_" + Path.GetFileName(folder);
            if (Check(AssetDatabase.MoveAsset(folder, to))) moved.Add(new KeyValuePair<string, string>(folder, to));
        }
        return moved;
    }

    static void RestoreWebAudio(List<KeyValuePair<string, string>> moved)
    {
        foreach (var kv in moved) Check(AssetDatabase.MoveAsset(kv.Value, kv.Key));
        Check(AssetDatabase.MoveAsset(WebAudioActive, WebAudioStore));
        AssetDatabase.DeleteAsset(HiddenRoot);
        AssetDatabase.Refresh();
    }

    static bool Check(string error)
    {
        if (string.IsNullOrEmpty(error)) return true;
        Debug.LogError("[BuildScript] " + error);
        return false;
    }

    static void Prepare()
    {
        SceneSetup.EnsureBaseMaterial();
        AudioTrimmer.Trim(false); // 辭典錄音只留字音 (不念部首、筆畫)
        SceneSetup.EnsureAllScenes();
        // 產品名稱不要改：家長錄音存在以產品名稱命名的資料夾裡
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

    static void Build(string output, BuildTarget target, BuildOptions extra = BuildOptions.None)
    {
        var options = new BuildPlayerOptions
        {
            scenes = SceneSetup.AllScenes, // 第一個 (兒童樂園) 是開啟時的入口
            locationPathName = output,
            target = target,
            options = extra,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[BuildScript] Result: " + report.summary.result + ", errors: " + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
