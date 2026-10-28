using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhonicsIsland
{
    // 把卡皮巴拉發音島會唸的每一句英文，先用 Windows 英文語音錄成 Resources/Voice/<key>.wav。
    // iPad Safari 不讓遊戲用瀏覽器朗讀 (只有手指點下去的那一刻才可以)，所以網頁版改播這些錄好的聲音。
    // 建置網頁版時會自動跑 (BuildScript)；只錄還沒有的句子，不再用到的句子會刪掉。
    public static class IslandVoices
    {
        const string VoiceDir = "Assets/PhonicsIsland/Resources/Voice";
        const string Script = "Tools/PhonicsIsland/make_island_voices.ps1";

        [MenuItem("注音怪獸/錄製發音島英文語音")]
        public static void Generate()
        {
            var lines = PhonicsIslandGame.AllSpokenLines(300);
            Directory.CreateDirectory(VoiceDir);

            int removed = 0;
            foreach (var f in Directory.GetFiles(VoiceDir, "*.wav"))
                if (!lines.ContainsKey(Path.GetFileNameWithoutExtension(f)))
                {
                    AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
                    removed++;
                }

            var missing = lines.Where(kv => !File.Exists(Path.Combine(VoiceDir, kv.Key + ".wav"))).ToList();
            if (missing.Count > 0)
            {
                string list = Path.Combine(Path.GetTempPath(), "phonics_island_voices.txt");
                File.WriteAllLines(list, missing.Select(kv => kv.Key + "\t" + PhonicsIslandGame.TtsLine(kv.Value)), new System.Text.UTF8Encoding(false));
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{Path.GetFullPath(Script)}\" -ListFile \"{list}\" -OutDir \"{Path.GetFullPath(VoiceDir)}\"")
                {
                    CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd(), error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0 || error.Length > 0) Debug.LogError("[IslandVoices] " + error);
                    Debug.Log("[IslandVoices] " + output.Trim());
                }
                File.Delete(list);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[IslandVoices] {lines.Count} 句：新錄 {missing.Count}，刪掉 {removed}");
        }
    }
}
