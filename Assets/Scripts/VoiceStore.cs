using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 家長自己錄的聲音 (例如 ㄅㄆㄇㄈ 的念法)，存成 WAV 放在 persistentDataPath/voices/<id>.wav。
// 有錄音時優先使用錄音，沒有才用教育部辭典的音檔。
public static class VoiceStore
{
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static string Folder => Path.Combine(Application.persistentDataPath, "voices");
    static string PathOf(string id) => Path.Combine(Folder, id + ".wav");

    public static AudioClip Get(string id)
    {
        if (!clips.TryGetValue(id, out var clip))
        {
            clip = File.Exists(PathOf(id)) ? LoadWav(id, PathOf(id)) : null;
            // 網頁版：電腦上錄好的聲音在建置時複製到 Resources/Voices
            if (clip == null) clip = Resources.Load<AudioClip>("Voices/" + id);
            clips[id] = clip;
        }
        return clip;
    }

    public static bool Has(string id) => Get(id) != null;

    public static void Delete(string id)
    {
        if (File.Exists(PathOf(id))) File.Delete(PathOf(id));
        clips[id] = null;
    }

    // 把麥克風錄到的聲音去頭去尾的靜音、調整音量後存起來。回傳 false 表示沒錄到聲音。
    public static bool Save(string id, float[] samples, int frequency)
    {
        float peak = 0f;
        foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak < 0.02f) return false;

        float threshold = peak * 0.08f;
        int start = 0, end = samples.Length - 1;
        while (start < end && Mathf.Abs(samples[start]) < threshold) start++;
        while (end > start && Mathf.Abs(samples[end]) < threshold) end--;
        int pad = frequency / 20; // 前後留 0.05 秒
        start = Mathf.Max(0, start - pad);
        end = Mathf.Min(samples.Length - 1, end + pad);

        var trimmed = new float[end - start + 1];
        float gain = 0.9f / peak;
        for (int i = 0; i < trimmed.Length; i++) trimmed[i] = samples[start + i] * gain;

        Directory.CreateDirectory(Folder);
        WriteWav(PathOf(id), trimmed, frequency);

        var clip = AudioClip.Create(id, trimmed.Length, 1, frequency, false);
        clip.SetData(trimmed, 0);
        clips[id] = clip;
        return true;
    }

    static void WriteWav(string path, float[] samples, int frequency)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)1);          // 單聲道
            w.Write(frequency);
            w.Write(frequency * 2);     // byte rate
            w.Write((short)2);          // block align
            w.Write((short)16);         // bits per sample
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }

    // 讀回 16-bit PCM WAV (自己存的格式；也接受其他程式錄的 16-bit WAV)
    static AudioClip LoadWav(string id, string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            int channels = BitConverter.ToInt16(bytes, 22);
            int frequency = BitConverter.ToInt32(bytes, 24);
            int bits = BitConverter.ToInt16(bytes, 34);
            if (bits != 16 || channels < 1) return null;

            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string chunk = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (chunk == "data")
                {
                    size = Math.Min(size, bytes.Length - pos - 8);
                    int frames = size / 2 / channels;
                    var data = new float[frames];
                    for (int i = 0; i < frames; i++)
                        data[i] = BitConverter.ToInt16(bytes, pos + 8 + i * 2 * channels) / (float)short.MaxValue;
                    var clip = AudioClip.Create(id, frames, 1, frequency, false);
                    clip.SetData(data, 0);
                    return clip;
                }
                pos += 8 + size + (size & 1);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VoiceStore] 讀取錄音失敗 " + path + ": " + e.Message);
        }
        return null;
    }
}
