using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhonicsIsland
{
    // 把卡皮巴拉發音島會唸的每一句英文，用 Edge 的 Natural 語音 (edge-tts，要連網) 錄成 Resources/Voice/<key>.mp3。
    // iPad Safari 不讓遊戲用瀏覽器朗讀 (只有手指點下去的那一刻才可以)，而且 iPad 也沒有 Edge 的好聽語音，
    // 所以網頁版改播這些錄好的聲音。建置網頁版時會自動跑 (BuildScript)；只錄還沒有的句子，不再用到的會刪掉。
    // 第一次要先執行 Tools/PhonicsIsland/setup_tts.bat 安裝 edge-tts。
    public static class IslandVoices
    {
        const string VoiceDir = "Assets/PhonicsIsland/Resources/Voice";
        const string Script = "Tools/PhonicsIsland/make_island_voices.py";
        const string Python = "Tools/PhonicsIsland/ttsenv/Scripts/python.exe";
        public const string Voice = "en-US-JennyNeural";
        const string Rate = "-15%";

        [MenuItem("注音怪獸/錄製發音島英文語音")]
        public static void Generate()
        {
            if (!File.Exists(Python))
            {
                Debug.LogError("[IslandVoices] 還沒安裝 edge-tts：請先執行 Tools/PhonicsIsland/setup_tts.bat (先沿用現有的錄音)");
                return;
            }
            var lines = PhonicsIslandGame.AllSpokenLines(300);
            Directory.CreateDirectory(VoiceDir);

            // 換了聲音就全部重錄
            string voiceTag = "Assets/PhonicsIsland/Editor/island_voice.txt";
            bool sameVoice = File.Exists(voiceTag) && File.ReadAllText(voiceTag).Trim() == Voice + " " + Rate;

            int removed = 0;
            foreach (var f in Directory.GetFiles(VoiceDir).Where(f => !f.EndsWith(".meta")))
                if (!sameVoice || !f.EndsWith(".mp3") || !lines.ContainsKey(Path.GetFileNameWithoutExtension(f)))
                {
                    AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
                    removed++;
                }

            var missing = lines.Where(kv => !File.Exists(Path.Combine(VoiceDir, kv.Key + ".mp3"))).ToList();
            if (missing.Count > 0)
            {
                string list = Path.Combine(Path.GetTempPath(), "phonics_island_voices.txt");
                File.WriteAllLines(list, missing.Select(kv => kv.Key + "\t" + PhonicsIslandGame.WebSpeechText(kv.Value)), new System.Text.UTF8Encoding(false));
                var psi = new System.Diagnostics.ProcessStartInfo(Path.GetFullPath(Python),
                    $"\"{Path.GetFullPath(Script)}\" \"{list}\" \"{Path.GetFullPath(VoiceDir)}\" {Voice} {Rate}")
                {
                    CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    var error = p.StandardError.ReadToEndAsync();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) Debug.LogError("[IslandVoices] " + error.Result);
                    Debug.Log("[IslandVoices] " + output.Trim());
                }
                File.Delete(list);
            }
            File.WriteAllText(voiceTag, Voice + " " + Rate);
            AssetDatabase.Refresh();
            Debug.Log($"[IslandVoices] {lines.Count} 句 ({Voice})：新錄 {missing.Count}，刪掉 {removed}");
        }
    }
}
