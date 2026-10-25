using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 教育部辭典的錄音念完字 (或詞) 之後還會念部首、筆畫等等。
// 這裡把 Assets/MoeAudio/*.ogg 只留下開頭的字音 / 詞音，存成 Assets/Resources/Audio/<名稱>.wav 給遊戲用：
//   <id>.ogg         → <id>.wav           字音
//   w_<id>.ogg       → w_<id>.wav         例詞 (辭典有詞的錄音)
//   wc_<id>_<n>.ogg  → w_<id>.wav         例詞沒有錄音時，把每個字的字音接起來
public static class AudioTrimmer
{
    const string SourceDir = "Assets/MoeAudio";
    const string OutDir = "Assets/Resources/Audio";
    const string ReportPath = "Build/trim_report.txt";

    const float Window = 0.01f;       // 每 10ms 算一次音量
    const float GapSeconds = 0.25f;   // 停頓超過這麼久就算一段結束
    const float WordGapSeconds = 0.6f;
    const float MinSegment = 0.12f;   // 太短的雜音不算
    const float Pad = 0.06f;
    const float MaxCharSeconds = 1.0f; // 一個字音不會超過 1 秒
    const float MaxWordSeconds = 2.2f; // 一個詞 (2-3 個字) 不會超過 2.2 秒
    const float JoinGap = 0.04f;       // 接字時字和字之間的空白

    [MenuItem("注音怪獸/裁切辭典錄音")]
    public static void TrimAllForced() => Trim(true);

    // force = false 時只處理來源比輸出新的檔案
    public static void Trim(bool force)
    {
        Directory.CreateDirectory(OutDir);
        var report = new StringBuilder();
        var expected = new HashSet<string>();
        int count = 0;

        var sources = Directory.GetFiles(SourceDir, "*.ogg").Select(p => p.Replace('\\', '/')).ToList();
        var joinGroups = new SortedDictionary<string, List<string>>();

        foreach (var src in sources)
        {
            string name = Path.GetFileNameWithoutExtension(src);
            if (name.StartsWith("wc_"))
            {
                // wc_<id>_<n>
                string rest = name.Substring(3);
                string id = rest.Substring(0, rest.LastIndexOf('_'));
                if (!joinGroups.TryGetValue(id, out var list)) joinGroups[id] = list = new List<string>();
                list.Add(src);
                continue;
            }

            string dst = OutDir + "/" + name + ".wav";
            expected.Add(dst);
            if (!force && IsUpToDate(dst, src)) continue;

            float max = name.StartsWith("w_") ? MaxWordSeconds : MaxCharSeconds;
            var cut = TrimClip(src, max, name, report, out int freq);
            if (cut == null) continue;
            WriteWav(dst, cut, freq);
            count++;
        }

        foreach (var kv in joinGroups)
        {
            var parts = kv.Value.OrderBy(p => int.Parse(p.Substring(p.LastIndexOf('_') + 1).Replace(".ogg", ""))).ToList();
            string dst = OutDir + "/w_" + kv.Key + ".wav";
            expected.Add(dst);
            if (!force && parts.All(p => IsUpToDate(dst, p))) continue;

            var joined = new List<float>();
            int freq = 0;
            bool ok = true;
            foreach (var p in parts)
            {
                var cut = TrimClip(p, MaxCharSeconds, Path.GetFileNameWithoutExtension(p), report, out int f);
                if (cut == null || (freq != 0 && f != freq)) { ok = false; break; }
                freq = f;
                if (joined.Count > 0) joined.AddRange(new float[Mathf.RoundToInt(JoinGap * freq)]);
                joined.AddRange(cut);
            }
            if (!ok) { report.AppendLine("w_" + kv.Key + ": 接字失敗"); continue; }
            WriteWav(dst, joined.ToArray(), freq);
            report.AppendLine($"w_{kv.Key}: 由 {parts.Count} 個字接成 {joined.Count / (float)freq:0.00}s");
            count++;
        }

        // 刪掉來源已經不存在的舊輸出
        foreach (var old in Directory.GetFiles(OutDir, "*.wav").Select(p => p.Replace('\\', '/')))
        {
            if (expected.Contains(old)) continue;
            AssetDatabase.DeleteAsset(old);
        }

        AssetDatabase.Refresh();
        if (count > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        }
        Debug.Log($"[AudioTrimmer] 裁切 {count} 個檔案，報告：{ReportPath}");
    }

    static bool IsUpToDate(string dst, string src) =>
        File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src);

    // 讀入一個辭典錄音，只留開頭的那一段 (不超過 maxSeconds)
    static float[] TrimClip(string src, float maxSeconds, string name, StringBuilder report, out int frequency)
    {
        frequency = 0;
        var importer = (AudioImporter)AssetImporter.GetAtPath(src);
        if (importer == null) { report.AppendLine(name + ": 無法讀取"); return null; }
        var settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(src);
        if (clip == null || !clip.LoadAudioData()) { report.AppendLine(name + ": 無法讀取"); return null; }

        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        var mono = new float[clip.samples];
        for (int i = 0; i < mono.Length; i++) mono[i] = data[i * clip.channels];
        frequency = clip.frequency;

        // 詞裡字和字之間會停頓 0.3~0.5 秒，和下一段 (部首等) 之間約 1 秒，所以詞用比較長的停頓來分段
        bool isWord = maxSeconds > MaxCharSeconds;
        var segments = FindSegments(mono, clip.frequency, 0.08f, isWord ? WordGapSeconds : GapSeconds);
        if (segments.Count == 0) { report.AppendLine(name + ": 找不到聲音"); return null; }
        var first = segments[0];

        // 有些錄音和後面的話之間停頓很短：提高門檻、縮短停頓，直到第一段夠短
        float[] thresholds = { 0.15f, 0.25f, 0.35f, 0.45f };
        foreach (var th in thresholds)
        {
            if (first.y - first.x <= maxSeconds) break;
            var refined = FindSegments(mono, clip.frequency, th, 0.1f);
            if (refined.Count == 0) continue;
            first = refined[0];
            // 門檻提高後尾音會被切掉一點，往後多留一些 (但不超過下一段的開頭)
            float limit = refined.Count > 1 ? refined[1].x - 0.03f : first.y + 0.15f;
            first.y = Mathf.Min(first.y + 0.15f, limit);
        }
        var segText = string.Join(" ", segments.Take(6).Select(s => $"[{s.x:0.00}-{s.y:0.00}]"));
        report.AppendLine(name + $": 全長 {clip.length:0.00}s，保留 [{first.x:0.00}-{first.y:0.00}] {(first.y - first.x):0.00}s　段落 {segText}");

        int a = Mathf.Max(0, Mathf.FloorToInt((first.x - Pad) * clip.frequency));
        int b = Mathf.Min(mono.Length, Mathf.CeilToInt((first.y + Pad) * clip.frequency));
        var cut = new float[b - a];
        System.Array.Copy(mono, a, cut, 0, cut.Length);
        // 結尾淡出，避免喀一聲
        int fade = Mathf.Min(cut.Length, clip.frequency / 50);
        for (int i = 0; i < fade; i++) cut[cut.Length - 1 - i] *= i / (float)fade;
        return cut;
    }

    // 找出有聲音的段落 (秒)
    static List<Vector2> FindSegments(float[] s, int freq, float relThreshold, float gapSeconds)
    {
        int win = Mathf.Max(1, Mathf.RoundToInt(Window * freq));
        int n = s.Length / win;
        var env = new float[n];
        float max = 0f;
        int headWins = Mathf.RoundToInt(3f / Window); // 音量門檻以開頭 3 秒為準 (後面的解說可能比較大聲)
        for (int w = 0; w < n; w++)
        {
            double sum = 0;
            for (int i = 0; i < win; i++) { float v = s[w * win + i]; sum += v * v; }
            env[w] = Mathf.Sqrt((float)(sum / win));
            if (w < headWins) max = Mathf.Max(max, env[w]);
        }
        float threshold = max * relThreshold;
        int gapWins = Mathf.RoundToInt(gapSeconds / Window);

        var result = new List<Vector2>();
        int start = -1, lastLoud = -1;
        for (int w = 0; w <= n; w++)
        {
            bool loud = w < n && env[w] > threshold;
            if (loud)
            {
                if (start < 0) start = w;
                lastLoud = w;
            }
            else if (start >= 0 && (w == n || w - lastLoud >= gapWins))
            {
                float a = start * Window, b = (lastLoud + 1) * Window;
                if (b - a >= MinSegment) result.Add(new Vector2(a, b));
                start = -1;
            }
        }
        return result;
    }

    static void WriteWav(string path, float[] samples, int frequency)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataBytes = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(frequency);
            w.Write(frequency * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
        }
    }
}
