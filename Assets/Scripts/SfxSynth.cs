using UnityEngine;

// 用程式合成音效，專案不需要額外的音效檔
public static class SfxSynth
{
    const int SampleRate = 44100;

    static AudioClip Make(string name, float dur, System.Func<float, System.Random, float> f)
    {
        int n = Mathf.CeilToInt(SampleRate * dur);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            data[i] = f(i / (float)SampleRate, rng);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 戰機連射的「噗」：低沉柔和，連續發射也不刺耳
    public static AudioClip Pew()
    {
        float phase = 0f, noise = 0f;
        const float dur = 0.09f;
        return Make("pew", dur, (t, r) =>
        {
            phase += 2f * Mathf.PI * Mathf.Lerp(150f, 70f, t / dur) / SampleRate;
            float white = (float)r.NextDouble() * 2f - 1f;
            noise += (white - noise) * 0.05f; // 很悶的雜音
            float env = Mathf.Clamp01(t / 0.006f) * (1f - t / dur) * (1f - t / dur);
            return (Mathf.Sin(phase) + noise * 1.5f) * env;
        });
    }

    // 爆炸的「轟」：低沉的雜音慢慢消失
    public static AudioClip Boom()
    {
        float noise = 0f, phase = 0f;
        return Make("boom", 0.9f, (t, r) =>
        {
            float white = (float)r.NextDouble() * 2f - 1f;
            noise += (white - noise) * 0.08f;
            phase += 2f * Mathf.PI * Mathf.Lerp(40f, 90f, Mathf.Exp(-t * 8f)) / SampleRate;
            float env = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-t * 4.5f);
            return (noise * 3f + Mathf.Sin(phase) * 0.6f) * env;
        });
    }

    // 打到護盾的「叮」
    public static AudioClip Ding()
    {
        return Make("ding", 0.25f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 18f);
            return (Mathf.Sin(2f * Mathf.PI * 1760f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2640f * t)) * env;
        });
    }

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
