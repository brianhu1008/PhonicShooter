using UnityEngine;

// 用程式合成音效，專案不需要額外的音效檔
public static class SfxSynth
{
    const int SampleRate = 44100;

    // 開槍的「蹦」：低沉的砰 + 一點爆裂的雜音，很快消失
    public static AudioClip Bang()
    {
        float dur = 0.35f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var data = new float[n];
        var rng = new System.Random(1);
        float phase = 0f, noise = 0f, peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float attack = Mathf.Clamp01(t / 0.002f);

            // 低音砰：頻率從 140Hz 掉到 45Hz
            float freq = Mathf.Lerp(45f, 140f, Mathf.Exp(-t * 25f));
            phase += 2f * Mathf.PI * freq / SampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 14f);

            // 爆裂聲：濾掉高頻的雜音
            float white = (float)rng.NextDouble() * 2f - 1f;
            noise += (white - noise) * 0.35f;
            float crack = noise * Mathf.Exp(-t * 35f);

            data[i] = attack * (thump * 0.9f + crack * 0.7f);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.85f / peak;

        var clip = AudioClip.Create("bang", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 被打到的「咚」：很低的悶響 + 短促的拍打聲
    public static AudioClip Hit()
    {
        float dur = 0.3f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var data = new float[n];
        var rng = new System.Random(2);
        float phase = 0f, noise = 0f, peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float attack = Mathf.Clamp01(t / 0.001f);
            float freq = Mathf.Lerp(55f, 110f, Mathf.Exp(-t * 30f));
            phase += 2f * Mathf.PI * freq / SampleRate;
            float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 16f);
            float white = (float)rng.NextDouble() * 2f - 1f;
            noise += (white - noise) * 0.6f;
            float slap = noise * Mathf.Exp(-t * 60f);
            data[i] = attack * (thud + slap * 0.6f);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;

        var clip = AudioClip.Create("hit", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
