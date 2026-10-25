using System.Collections.Generic;
using UnityEngine;

// 一個注音符號 (ㄅ、ㄧ、ㄠ…)
public class Symbol
{
    public string id;           // 也是語音檔名 (Resources/Audio/<id>，或家長錄音 voices/<id>.wav)
    public string ch;           // 符號本身
    public int group;           // 0-9，學校教的分組
    public string defaultCh;    // 預設念法用的字 (教育部辭典字音)，null 表示沒有
    public Color color;
    public bool IsInitial => group <= 5;
}

// 一個拼音 (例如 ㄐㄧㄠˇ 腳)
public class Syllable
{
    public string id;       // 也是語音檔名
    public string zhuyin;   // 完整注音 (含聲調)
    public string initial;  // 聲母，沒有時為 ""
    public string rhyme;    // 介音 + 韻母，例如 ㄧㄠ
    public string tone;     // "" 一聲, ˊ 二聲, ˇ 三聲, ˋ 四聲
    public string ch;       // 例字
    public string word;     // 例詞

    public string Display => zhuyin;
    public string Core => initial + rhyme;
    public bool Contains(Symbol s) => Core.Contains(s.ch);
}

// 怪獸身上帶的「答案」：可能是一個符號，也可能是一個拼音
public class Answer
{
    public string id;
    public string display;
    public string audioId;
    public Color color;
    public Symbol symbol;     // 符號題
    public Syllable syllable; // 拼音題
}

// 注音資料：從 Resources/phonic_symbols.txt 和 phonic_syllables.txt 讀入
// (Tools/download_moe_audio.ps1 也讀同一份資料下載語音)
public static class PhonicData
{
    public static readonly string[] GroupNames =
    {
        "ㄅㄆㄇㄈ", "ㄉㄊㄋㄌ", "ㄍㄎㄏ", "ㄐㄑㄒ", "ㄓㄔㄕㄖ",
        "ㄗㄘㄙ", "ㄧㄨㄩ", "ㄚㄛㄜㄝ", "ㄞㄟㄠㄡ", "ㄢㄣㄤㄥㄦ",
    };
    public const int GroupCount = 10;

    // 同一組裡依序的顏色 (紅、橘、綠、藍、紫)
    static readonly Color[] Palette =
    {
        new Color(1f, 0.45f, 0.45f), new Color(1f, 0.68f, 0.25f), new Color(0.45f, 0.85f, 0.45f),
        new Color(0.55f, 0.6f, 1f), new Color(0.8f, 0.5f, 1f),
    };

    static List<Symbol> symbols;
    static List<Syllable> syllables;
    static Dictionary<char, Symbol> byChar;

    public static List<Symbol> Symbols { get { Load(); return symbols; } }
    public static List<Syllable> Syllables { get { Load(); return syllables; } }

    static void Load()
    {
        if (symbols != null) return;
        symbols = new List<Symbol>();
        syllables = new List<Syllable>();
        byChar = new Dictionary<char, Symbol>();

        var groupCounts = new int[GroupCount];
        foreach (var c in ReadTable("phonic_symbols"))
        {
            int g = int.Parse(c[2]) - 1;
            var s = new Symbol
            {
                id = c[0], ch = c[1], group = g,
                defaultCh = c[3] == "-" ? null : c[3],
                color = Palette[groupCounts[g]++ % Palette.Length],
            };
            symbols.Add(s);
            byChar[s.ch[0]] = s;
        }

        foreach (var c in ReadTable("phonic_syllables"))
        {
            string z = c[1];
            string tone = "ˊˇˋ˙".IndexOf(z[z.Length - 1]) >= 0 ? z.Substring(z.Length - 1) : "";
            string core = z.Substring(0, z.Length - tone.Length);
            string initial = byChar.TryGetValue(core[0], out var first) && first.IsInitial ? core.Substring(0, 1) : "";
            syllables.Add(new Syllable
            {
                id = c[0], zhuyin = z, initial = initial, rhyme = core.Substring(initial.Length),
                tone = tone, ch = c[2], word = c[3],
            });
        }
    }

    static IEnumerable<string[]> ReadTable(string resource)
    {
        var asset = Resources.Load<TextAsset>(resource);
        foreach (var raw in asset.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            yield return line.Split('|');
        }
    }

    public static Symbol SymbolOf(char c)
    {
        Load();
        return byChar.TryGetValue(c, out var s) ? s : null;
    }

    public static List<Symbol> SymbolsInGroup(int group)
    {
        var list = new List<Symbol>();
        foreach (var s in Symbols) if (group < 0 || s.group == group) list.Add(s);
        return list;
    }

    // 拼音裡的注音符號 (不含聲調)，例如 ㄐㄧㄠˇ → ㄐ、ㄧ、ㄠ
    public static List<Symbol> PartsOf(Syllable s)
    {
        var list = new List<Symbol>();
        foreach (char c in s.Core)
        {
            var sym = SymbolOf(c);
            if (sym != null) list.Add(sym);
        }
        return list;
    }

    // 含有這一組任何一個符號的拼音 (group < 0 表示全部)
    public static List<Syllable> SyllablesForGroup(int group)
    {
        var groupSymbols = SymbolsInGroup(group);
        var list = new List<Syllable>();
        foreach (var s in Syllables)
            foreach (var sym in groupSymbols)
                if (s.Contains(sym)) { list.Add(s); break; }
        return list;
    }

    public static Answer SymbolAnswer(Symbol s)
    {
        return new Answer { id = s.id, display = s.ch, audioId = s.id, color = s.color, symbol = s };
    }

    public static Answer SyllableAnswer(Syllable s)
    {
        return new Answer { id = s.id, display = s.Display, audioId = s.id, color = PartsOf(s)[0].color, syllable = s };
    }
}
