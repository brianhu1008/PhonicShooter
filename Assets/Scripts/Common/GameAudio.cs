using System.Collections.Generic;
using UnityEngine;

// 載入 Resources 裡的聲音。
// 網頁版：iPad Safari 解不開 Unity 打包的音檔 (EncodingError: Decoding failed)，
// 所以建置時 (BuildScript.ExportWebAudio) 把每個聲音轉成原始 PCM 資料放在 Resources/Pcm/<路徑>.bytes，
// 執行時直接用 AudioClip.Create 建立，完全不經過瀏覽器的音檔解碼。
public static class GameAudio
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    // warnIfMissing = false：本來就可能沒有的聲音 (例如家長沒錄的符號)
    public static AudioClip Load(string path, bool warnIfMissing = true)
    {
        if (cache.TryGetValue(path, out var clip)) return clip;
#if UNITY_WEBGL && !UNITY_EDITOR
        var data = Resources.Load<TextAsset>("Pcm/" + path);
        clip = data != null ? FromPcm(path, data.bytes) : null;
        if (clip == null && warnIfMissing) Debug.LogWarning("[GameAudio] 找不到聲音 " + path);
#else
        clip = Resources.Load<AudioClip>(path);
#endif
        cache[path] = clip;
        return clip;
    }

    // 格式："PCM1" + 取樣率 (int) + 樣本數 (int) + 16-bit 單聲道樣本
    public const int HeaderSize = 12;

    public static AudioClip FromPcm(string name, byte[] bytes)
    {
        if (bytes == null || bytes.Length < HeaderSize || bytes[0] != 'P' || bytes[1] != 'C' || bytes[2] != 'M' || bytes[3] != '1') return null;
        int rate = System.BitConverter.ToInt32(bytes, 4);
        int count = System.BitConverter.ToInt32(bytes, 8);
        count = Mathf.Min(count, (bytes.Length - HeaderSize) / 2);
        if (count <= 0 || rate <= 0) return null;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = System.BitConverter.ToInt16(bytes, HeaderSize + i * 2) / 32768f;
        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
