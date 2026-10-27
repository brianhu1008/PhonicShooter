using System.Collections.Generic;
using UnityEngine;

// 一題九九乘法：a × b
public class MulQuestion
{
    public int a, b;
    public int Answer => a * b;
    public string Text => a + " × " + b + " = ?";
    public string Full => a + " × " + b + " = " + Answer;
    public string Chant => MulData.Chant(a, b);
    public string QuestionVoice => "MulVoice/q_" + a + "_" + b;
    public string ChantVoice => "MulVoice/c_" + a + "_" + b;
}

public static class MulData
{
    // 乘法表範圍：2～5、6～9、全部
    public static readonly string[] RangeNames = { "2～5 的乘法", "6～9 的乘法", "2～9 全部" };
    static readonly int[][] RangeTables = { new[] { 2, 3, 4, 5 }, new[] { 6, 7, 8, 9 }, new[] { 2, 3, 4, 5, 6, 7, 8, 9 } };

    static readonly string[] Digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    public static MulQuestion Next(int range, MulQuestion last)
    {
        var tables = RangeTables[range];
        MulQuestion q;
        do q = new MulQuestion { a = tables[Random.Range(0, tables.Length)], b = Random.Range(1, 10) };
        while (last != null && q.a == last.a && q.b == last.b);
        return q;
    }

    // 正確答案 + 容易搞混的錯誤答案 (相鄰的乘積、加法…)，打亂順序
    public static List<int> Choices(MulQuestion q, int count)
    {
        int ans = q.Answer;
        var near = new List<int> { q.a * (q.b + 1), q.a * (q.b - 1), (q.a + 1) * q.b, (q.a - 1) * q.b, q.a + q.b, ans + 1, ans - 1, ans + 10, ans - 10 };
        var list = new List<int> { ans };
        Shuffle(near);
        foreach (var n in near)
        {
            if (list.Count >= count) break;
            if (n > 0 && n <= 90 && !list.Contains(n)) list.Add(n);
        }
        while (list.Count < count)
        {
            int n = Random.Range(2, 10) * Random.Range(1, 10);
            if (!list.Contains(n)) list.Add(n);
        }
        Shuffle(list);
        return list;
    }

    // 九九乘法口訣：三四十二、二三得六、二五一十、四五二十
    public static string Chant(int a, int b)
    {
        int p = a * b;
        string head = Digits[a] + Digits[b];
        return p < 10 ? head + "得" + Digits[p] : head + Number(p);
    }

    // 口訣裡的數字念法：10 → 一十、12 → 十二、24 → 二十四
    public static string Number(int p)
    {
        if (p < 10) return Digits[p];
        int t = p / 10, o = p % 10;
        if (t == 1) return o == 0 ? "一十" : "十" + Digits[o];
        return Digits[t] + "十" + (o == 0 ? "" : Digits[o]);
    }

    // 題目念法：三乘以四 (Tools/make_mul_voices.ps1 用同樣的說法產生語音)
    public static string Spoken(int a, int b) => Digits[a] + "乘以" + Digits[b];

    public static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
