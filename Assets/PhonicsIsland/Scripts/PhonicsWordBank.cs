// ============================================================================
//  Phonics Island - word banks and random question sets
//
//  Every island draws new words from these lists each time it is played.
//  To add a word: put it in a list below and add its picture to
//  Tools/make_pictures.ps1 (then run that script).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        // ------------------------------------------------------------------
        //  Word banks
        // ------------------------------------------------------------------
        // Short vowels (words with that short vowel sound)
        static readonly Dictionary<string, string[]> ShortVowelWords = new Dictionary<string, string[]>
        {
            { "a", new[] { "cat", "bag", "hat", "map", "bat", "ant", "apple" } },
            { "e", new[] { "bed", "hen", "pen", "net", "ten", "web", "egg", "bell", "leg" } },
            { "i", new[] { "fish", "pig", "six", "pin", "milk", "gift", "ring", "king" } },
            { "o", new[] { "dog", "box", "fox", "sock", "lock", "frog", "rock", "ox" } },
            { "u", new[] { "sun", "bug", "bus", "cup", "duck", "drum", "nut", "umbrella" } },
        };

        // Words that start with the short vowel (for "Which one starts with /a/?")
        static readonly Dictionary<string, string[]> VowelStartWords = new Dictionary<string, string[]>
        {
            { "a", new[] { "apple", "ant", "alligator" } },
            { "e", new[] { "egg", "elephant" } },
            { "o", new[] { "octopus", "ox", "otter" } },
            { "u", new[] { "umbrella" } },
        };

        static readonly Dictionary<string, string> ShortVowelIpa = new Dictionary<string, string>
        {
            { "a", "æ" }, { "e", "ɛ" }, { "i", "ɪ" }, { "o", "ɑ" }, { "u", "ʌ" }
        };

        // Consonant digraphs
        static readonly Dictionary<string, string[]> DigraphWords = new Dictionary<string, string[]>
        {
            { "sh", new[] { "ship", "shop", "shell", "sheep", "shoe", "shirt", "shark", "shorts", "shower", "fish", "brush", "dish" } },
            { "ch", new[] { "chick", "cheese", "chair", "cherry", "chicken", "chocolate", "church", "chips", "peach", "watch" } },
            { "th", new[] { "three", "thumb", "thread", "think", "teeth", "bath", "mouth", "earth", "feather" } },
            { "wh", new[] { "whale", "wheel" } },
        };

        static readonly Dictionary<string, string> DigraphIpa = new Dictionary<string, string>
        {
            // ch / th get a short vowel so the sound is clear (like 去), not swallowed.
            { "sh", "ʃ" }, { "ch", "tʃə" }, { "th", "θə" }, { "wh", "w" }
        };

        // Consonant blends (two consonants, both heard)
        static readonly Dictionary<string, string[]> BlendWords = new Dictionary<string, string[]>
        {
            { "fl", new[] { "flag", "flower" } },
            { "fr", new[] { "frog" } },
            { "tr", new[] { "train", "tree" } },
            { "cr", new[] { "crab", "crown", "crayon" } },
            { "dr", new[] { "drum" } },
            { "st", new[] { "star", "stop" } },
            { "sn", new[] { "snake", "snail" } },
            { "sp", new[] { "spider", "spoon" } },
            { "sl", new[] { "sled" } },
            { "pl", new[] { "plane" } },
            { "cl", new[] { "clock", "clown" } },
            { "gr", new[] { "grapes" } },
            { "gl", new[] { "glasses" } },
            { "br", new[] { "bread", "brush" } },
            { "sk", new[] { "skate" } },
            { "sw", new[] { "swan" } },
        };

        // Vowel teams. Teams in the same family make the same sound, so they are never
        // offered together as answer choices.
        static readonly Dictionary<string, string[]> TeamWords = new Dictionary<string, string[]>
        {
            { "ee", new[] { "tree", "bee", "sheep", "cheese", "seed", "queen", "teeth", "wheel", "three" } },
            { "ea", new[] { "leaf", "tea", "beach", "peach", "meat", "bean", "seal", "eagle" } },
            { "ai", new[] { "rain", "snail", "train", "mail", "nail", "paint", "sail", "chain", "rainbow" } },
            { "ay", new[] { "crayon" } },
            { "ow", new[] { "cow", "owl", "crown", "clown", "flower", "tower" } },
            { "ou", new[] { "house", "mouse", "cloud", "mouth", "couch", "fountain" } },
            { "oi", new[] { "coin", "oil", "toilet", "point" } },
            { "oy", new[] { "boy", "toy", "oyster" } },
        };

        static readonly string[][] TeamFamilies =
        {
            new[] { "ee", "ea" }, new[] { "ai", "ay" }, new[] { "ow", "ou" }, new[] { "oi", "oy" }, new[] { "oo" }
        };

        static readonly string[] LongOoWords = { "moon", "spoon", "boot", "tooth", "goose", "broom", "balloon", "rooster", "kangaroo", "scooter", "noodles" };
        static readonly string[] ShortOoWords = { "book", "foot", "wood", "cook", "hook", "cookie", "wool", "football", "look" };

        // Beach: things to find (long vowel words + beach things)
        static readonly string[] BeachFindWords =
        {
            "shell", "crab", "star", "tree", "bee", "leaf", "seal", "seed", "peach", "tea", "snail", "sail", "rain", "crayon", "chain", "eagle", "queen"
        };

        // Plurals: -s and -es
        static readonly string[] PluralS = { "cat", "dog", "cup", "duck", "bee", "frog", "sock", "pig", "hat", "bag", "ant", "cookie" };
        static readonly string[] PluralEs = { "box", "fox", "bus", "dish", "watch", "peach", "brush" };

        // Simple nouns for sentences
        static readonly string[] SentenceNouns =
        {
            "cat", "dog", "pig", "hen", "duck", "frog", "fish", "bee", "cow", "owl", "sheep", "whale", "snake", "crab",
            "sun", "moon", "star", "tree", "flower", "book", "bus", "train", "plane", "hat", "cup", "egg", "apple",
            "cake", "kite"
        };

        static readonly string[] LikeNouns = { "apple", "cookie", "cherry", "grapes", "cat", "dog", "duck", "flower", "balloon", "peach" };

        // Multi-letter units kept together on spelling tiles.
        static readonly string[] SpellUnits = { "sh", "ch", "th", "wh", "ck", "ng", "ee", "ea", "ai", "ay", "oo", "ow", "ou", "oi", "oy" };

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------
        static T RandomOf<T>(IList<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        // Up to n distinct random items from pool, skipping anything in avoid.
        static List<string> PickSome(IEnumerable<string> pool, int n, ICollection<string> avoid = null)
        {
            var list = new List<string>();
            foreach (string s in pool)
            {
                if ((avoid == null || !avoid.Contains(s)) && !list.Contains(s)) list.Add(s);
            }
            Shuffle(list);
            return list.Count > n ? list.GetRange(0, n) : list;
        }

        static List<string> AllWords(Dictionary<string, string[]> bank)
        {
            var all = new List<string>();
            foreach (var kv in bank) all.AddRange(kv.Value);
            return all;
        }

        static string Article(string noun)
        {
            return "aeiou".IndexOf(noun[0]) >= 0 ? "an" : "a";
        }

        static string Plural(string noun)
        {
            if (noun == "cherry") return "cherries";
            if (noun == "grapes") return "grapes";
            if (noun.EndsWith("x") || noun.EndsWith("s") || noun.EndsWith("sh") || noun.EndsWith("ch")) return noun + "es";
            return noun + "s";
        }

        // "cheese" with "ch" -> ch, ee, s, e
        static string[] Chunk(string word, string main)
        {
            var chunks = new List<string>();
            int i = 0;
            while (i < word.Length)
            {
                string unit = null;
                if (!string.IsNullOrEmpty(main) && string.CompareOrdinal(word, i, main, 0, main.Length) == 0) unit = main;
                else
                {
                    foreach (string u in SpellUnits)
                    {
                        if (string.CompareOrdinal(word, i, u, 0, u.Length) == 0) { unit = u; break; }
                    }
                }
                if (unit == null) unit = word.Substring(i, 1);
                chunks.Add(unit);
                i += unit.Length;
            }
            return chunks.ToArray();
        }

        static string[] FamilyOf(string team)
        {
            foreach (string[] f in TeamFamilies)
            {
                if (System.Array.IndexOf(f, team) >= 0) return f;
            }
            return new[] { team };
        }

        // Two wrong choices from other sound families.
        static string[] TeamDistractors(string team, string[] candidates)
        {
            var avoid = new List<string>(FamilyOf(team));
            return PickSome(candidates, 2, avoid).ToArray();
        }

        static string[] Opts(string answer, IList<string> wrong)
        {
            var o = new List<string> { answer };
            o.AddRange(wrong);
            return o.ToArray();
        }

        // Spelling question; extras are pieces from the same lesson that are not in the word.
        static Question SpellWord(string word, string main, string[] extraPool)
        {
            string[] chunks = Chunk(word, main);
            var extras = PickSome(extraPool, 2, new List<string>(chunks));
            return Spell(word, chunks, extras.ToArray());
        }

        static int ChunkCount(string word, string main)
        {
            return Chunk(word, main).Length;
        }

        // Fills a question list from builders, never using the same word twice.
        class SetBuilder
        {
            public readonly List<Question> list = new List<Question>();
            public readonly HashSet<string> used = new HashSet<string>();

            public void Add(Question q)
            {
                if (q == null) return;
                list.Add(q);
                used.Add(q.answer);
                if (q.pic != null) used.Add(q.pic);
            }
        }

        // ------------------------------------------------------------------
        //  Letter Forest: short vowels
        // ------------------------------------------------------------------
        Question GenStartsWith(SetBuilder b)
        {
            var vowels = new List<string>(VowelStartWords.Keys);
            Shuffle(vowels);
            string v = null;
            List<string> answers = null;
            foreach (string cand in vowels)
            {
                answers = PickSome(VowelStartWords[cand], 1, b.used);
                if (answers.Count > 0) { v = cand; break; }
            }
            if (v == null) return null;
            var wrongPool = new List<string>();
            foreach (var kv in ShortVowelWords)
            {
                if (kv.Key == v) continue;
                foreach (string w in kv.Value) if (w[0] != v[0]) wrongPool.Add(w);
            }
            return StartsWith(answers[0], v, ShortVowelIpa[v], Opts(answers[0], PickSome(wrongPool, 2)));
        }

        Question GenMissingVowel(SetBuilder b, bool spell)
        {
            var vowels = new List<string>(ShortVowelWords.Keys);
            Shuffle(vowels);
            string v = null;
            List<string> pick = null;
            foreach (string cand in vowels)
            {
                var pool = new List<string>();
                foreach (string w in ShortVowelWords[cand])
                {
                    if (spell && (w.Length > 4 || ChunkCount(w, null) > 4)) continue;
                    pool.Add(w);
                }
                pick = PickSome(pool, 1, b.used);
                if (pick.Count > 0) { v = cand; break; }
            }
            if (v == null) return null;
            string word = pick[0];
            if (spell)
            {
                var others = new List<string>(ShortVowelWords.Keys);
                others.Remove(v);
                return SpellWord(word, null, others.ToArray());
            }
            int at = word.IndexOf(v[0]);
            string template = word.Substring(0, at) + "#" + word.Substring(at + 1);
            var wrong = new List<string>(ShortVowelWords.Keys);
            wrong.Remove(v);
            return Missing(word, template, v, Opts(v, PickSome(wrong, 2)));
        }

        // Listen and pick, with choices that differ only a little (other short vowels).
        Question GenListenVowel(SetBuilder b)
        {
            string v = RandomOf(new List<string>(ShortVowelWords.Keys));
            var pick = PickSome(ShortVowelWords[v], 1, b.used);
            if (pick.Count == 0) return null;
            var wrongPool = new List<string>();
            foreach (var kv in ShortVowelWords) if (kv.Key != v) wrongPool.AddRange(kv.Value);
            return Listen(pick[0], Opts(pick[0], PickSome(wrongPool, 2)));
        }

        List<Question> GenForest(bool hard)
        {
            var b = new SetBuilder();
            if (!hard)
            {
                b.Add(GenStartsWith(b));
                b.Add(GenListenVowel(b));
                b.Add(GenMissingVowel(b, false));
                b.Add(GenListenVowel(b));
                b.Add(GenMissingVowel(b, false));
            }
            else
            {
                b.Add(GenStartsWith(b));
                b.Add(GenMissingVowel(b, false));
                b.Add(GenListenVowel(b));
                b.Add(GenMissingVowel(b, true));
                b.Add(GenMissingVowel(b, false));
                b.Add(GenStartsWith(b));
                b.Add(GenMissingVowel(b, true));
                b.Add(GenMissingVowel(b, false));
            }
            return b.list;
        }

        // ------------------------------------------------------------------
        //  Sound Cave: sh ch th (wh) + blends for Big Sister
        // ------------------------------------------------------------------
        Question GenWhichDigraph(SetBuilder b, bool hard)
        {
            string snd = RandomOf(hard ? new List<string>(DigraphWords.Keys) : new List<string> { "sh", "ch", "th" });
            var pick = PickSome(DigraphWords[snd], 1, b.used);
            if (pick.Count == 0) return null;
            string word = pick[0];
            int at = word.IndexOf(snd);
            string template = word.Substring(0, at) + "#" + word.Substring(at + snd.Length);
            var wrong = new List<string> { "sh", "ch", "th" };
            if (hard) wrong.Add("wh");
            wrong.Remove(snd);
            return WhichSound(word, template, snd, Opts(snd, PickSome(wrong, 2)));
        }

        Question GenHasDigraph(SetBuilder b)
        {
            string snd = RandomOf(new List<string> { "sh", "ch", "th" });
            var pick = PickSome(DigraphWords[snd], 1, b.used);
            if (pick.Count == 0) return null;
            var wrongPool = new List<string>();
            foreach (var kv in DigraphWords)
            {
                if (kv.Key == snd) continue;
                foreach (string w in kv.Value) if (!w.Contains(snd)) wrongPool.Add(w);
            }
            return HasSound(pick[0], snd, DigraphIpa[snd], Opts(pick[0], PickSome(wrongPool, 2, b.used)));
        }

        Question GenListenDigraph(SetBuilder b)
        {
            var all = AllWords(DigraphWords);
            var pick = PickSome(all, 1, b.used);
            if (pick.Count == 0) return null;
            return Listen(pick[0], Opts(pick[0], PickSome(all, 2, new List<string>(pick))));
        }

        Question GenSpellDigraph(SetBuilder b)
        {
            var pool = new List<string>();
            foreach (var kv in DigraphWords)
                foreach (string w in kv.Value)
                    if (ChunkCount(w, kv.Key) <= 4) pool.Add(w);
            var pick = PickSome(pool, 1, b.used);
            if (pick.Count == 0) return null;
            string word = pick[0];
            string snd = "sh";
            foreach (string k in DigraphWords.Keys) if (word.Contains(k)) snd = k;
            return SpellWord(word, snd, new[] { "sh", "ch", "th", "wh" });
        }

        Question GenWhichBlend(SetBuilder b)
        {
            var keys = new List<string>(BlendWords.Keys);
            Shuffle(keys);
            foreach (string blend in keys)
            {
                var pick = PickSome(BlendWords[blend], 1, b.used);
                if (pick.Count == 0) continue;
                string word = pick[0];
                // Wrong choices share a letter with the right blend so the child has to listen.
                var close = new List<string>();
                foreach (string k in BlendWords.Keys)
                    if (k != blend && (k[0] == blend[0] || k[1] == blend[1])) close.Add(k);
                if (close.Count < 2) foreach (string k in BlendWords.Keys) if (k != blend) close.Add(k);
                var q = WhichSound(word, "#" + word.Substring(2), blend, Opts(blend, PickSome(close, 2)));
                q.prompt = "Which two letters do you hear?";
                q.zh = "開頭是哪兩個字母？";
                return q;
            }
            return null;
        }

        List<Question> GenCave(bool hard)
        {
            var b = new SetBuilder();
            if (!hard)
            {
                b.Add(GenWhichDigraph(b, false));
                b.Add(GenHasDigraph(b));
                b.Add(GenListenDigraph(b));
                b.Add(GenWhichDigraph(b, false));
                b.Add(GenHasDigraph(b));
            }
            else
            {
                b.Add(GenWhichDigraph(b, true));
                b.Add(GenHasDigraph(b));
                b.Add(GenSpellDigraph(b));
                b.Add(GenWhichBlend(b));
                b.Add(GenHasDigraph(b));
                b.Add(GenSpellDigraph(b));
                b.Add(GenWhichBlend(b));
                b.Add(GenWhichDigraph(b, true));
            }
            return b.list;
        }

        // ------------------------------------------------------------------
        //  Word Beach: find things + long vowel teams
        // ------------------------------------------------------------------
        string[] PickBeachItems()
        {
            return PickSome(BeachFindWords, 7).ToArray();
        }

        Question GenWhichTeam(SetBuilder b, string[] teams, string[] choicePool)
        {
            string team = RandomOf(teams);
            var pick = PickSome(TeamWords[team], 1, b.used);
            if (pick.Count == 0) return null;
            string word = pick[0];
            int at = word.IndexOf(team);
            string template = word.Substring(0, at) + "#" + word.Substring(at + team.Length);
            return WhichSound(word, template, team, Opts(team, TeamDistractors(team, choicePool)));
        }

        Question GenSpellTeam(SetBuilder b, string[] teams, string[] extraPool)
        {
            var pool = new List<string>();
            foreach (string t in teams)
                foreach (string w in TeamWords[t])
                    if (ChunkCount(w, t) <= 4) pool.Add(w);
            var pick = PickSome(pool, 1, b.used);
            if (pick.Count == 0) return null;
            string word = pick[0];
            string team = teams[0];
            foreach (string t in teams) if (word.Contains(t)) { team = t; break; }
            var extras = new List<string>(extraPool);
            foreach (string f in FamilyOf(team)) extras.Remove(f);
            return SpellWord(word, team, extras.ToArray());
        }

        List<Question> GenBeach(bool hard)
        {
            var b = new SetBuilder();
            var items = new List<string>(cur.findItems);
            Shuffle(items);
            int finds = hard ? 5 : 5;
            var teamChoices = new[] { "ee", "ai", "oo", "ow" };
            for (int i = 0; i < finds && i < items.Count; i++)
            {
                b.Add(Find(items[i]));
                if (hard && i == 1) b.Add(GenWhichTeam(b, new[] { "ee", "ea", "ai" }, teamChoices));
                if (hard && i == 3) b.Add(GenSpellTeam(b, new[] { "ee", "ea", "ai" }, new[] { "ee", "ai", "oo", "ow", "oi" }));
            }
            if (hard) b.Add(GenWhichTeam(b, new[] { "ee", "ea", "ai" }, teamChoices));
            return b.list;
        }

        // ------------------------------------------------------------------
        //  Spelling Valley: oo (moon / book), ow ou oi oy
        // ------------------------------------------------------------------
        Question GenSortOo(SetBuilder b)
        {
            var all = new List<string>(LongOoWords);
            all.AddRange(ShortOoWords);
            var pick = PickSome(all, 1, b.used);
            if (pick.Count == 0) return null;
            return Sort(pick[0]);
        }

        Question GenListenTeam(SetBuilder b)
        {
            var all = new List<string>();
            foreach (string t in new[] { "ow", "ou", "oi", "oy" }) all.AddRange(TeamWords[t]);
            var pick = PickSome(all, 1, b.used);
            if (pick.Count == 0) return null;
            return Listen(pick[0], Opts(pick[0], PickSome(all, 2, new List<string>(pick))));
        }

        List<Question> GenValley(bool hard)
        {
            var b = new SetBuilder();
            var teams = new[] { "ow", "ou", "oi", "oy" };
            var choicePool = new[] { "ow", "oi", "oo", "ee", "ai" };
            if (!hard)
            {
                b.Add(GenSortOo(b));
                b.Add(GenSortOo(b));
                b.Add(GenWhichTeam(b, teams, choicePool));
                b.Add(GenListenTeam(b));
                b.Add(GenSortOo(b));
            }
            else
            {
                b.Add(GenSortOo(b));
                b.Add(GenWhichTeam(b, teams, choicePool));
                b.Add(GenSpellTeam(b, teams, new[] { "ow", "oi", "oo", "ee", "ai" }));
                b.Add(GenSortOo(b));
                b.Add(GenSpellTeam(b, teams, new[] { "ow", "oi", "oo", "ee", "ai" }));
                b.Add(GenWhichTeam(b, teams, choicePool));
                b.Add(GenSpellTeam(b, teams, new[] { "ow", "oi", "oo", "ee", "ai" }));
                b.Add(GenSortOo(b));
            }
            return b.list;
        }

        // ------------------------------------------------------------------
        //  Sentence Village: sentences and plurals
        // ------------------------------------------------------------------
        Question GenWhatSee(SetBuilder b, bool whatIsIt)
        {
            var pick = PickSome(SentenceNouns, 3, b.used);
            if (pick.Count < 3) return null;
            string start = whatIsIt ? "It is " : "I see ";
            var opts = new List<string>();
            foreach (string n in pick) opts.Add(start + Article(n) + " " + n + ".");
            var q = Answer(pick[0], whatIsIt ? "What is it?" : "What do you see?", opts[0],
                whatIsIt ? "這是什麼？" : "你看到什麼？", opts.ToArray());
            q.answer = opts[0];
            b.used.Add(pick[0]);
            return q;
        }

        Question GenHave(SetBuilder b)
        {
            var pick = PickSome(SentenceNouns, 3, b.used);
            if (pick.Count < 3) return null;
            string art = Article(pick[0]);
            var q = Sentence(pick[0], "I have " + art + " #.", pick[0], "我有一個 " + pick[0] + "。", pick.ToArray());
            b.used.Add(pick[0]);
            return q;
        }

        Question GenLike(SetBuilder b)
        {
            var pick = PickSome(LikeNouns, 3, b.used);
            if (pick.Count < 3) return null;
            var opts = new List<string>();
            foreach (string n in pick) opts.Add(Plural(n));
            var q = Sentence(pick[0] + "*2", "I like #.", opts[0], "我喜歡 " + opts[0] + "。", opts.ToArray());
            b.used.Add(pick[0]);
            return q;
        }

        Question GenPlural(SetBuilder b, bool es)
        {
            var pick = PickSome(es ? PluralEs : PluralS, 1, b.used);
            if (pick.Count == 0) return null;
            string n = pick[0];
            string right = Plural(n);
            string wrong = es ? n + "s" : n + "es";
            string[] nums = { "two", "three" };
            string num = RandomOf(nums);
            var q = Sentence(n + (num == "two" ? "*2" : "*3"), "I see " + num + " #.", right,
                "我看到" + (num == "two" ? "兩" : "三") + "個 " + right + "。", right, n, wrong);
            q.hintSound = es ? "-es" : "-s";
            b.used.Add(n);
            return q;
        }

        Question GenWhere(SetBuilder b)
        {
            string[] containers = { "box", "bag", "bed", "chair", "cup" };
            string[] preps = { "in", "in", "on", "on", "in" };
            string[] animals = { "cat", "dog", "duck", "frog", "mouse", "chick" };
            var animal = PickSome(animals, 1, b.used);
            if (animal.Count == 0) return null;
            var order = new List<int> { 0, 1, 2, 3, 4 };
            Shuffle(order);
            var opts = new List<string>();
            for (int k = 0; k < 3; k++) opts.Add("It is " + preps[order[k]] + " the " + containers[order[k]] + ".");
            var q = Answer(containers[order[0]] + "+" + animal[0], "Where is the " + animal[0] + "?", opts[0],
                animal[0] + " 在哪裡？", opts.ToArray());
            b.used.Add(animal[0]);
            return q;
        }

        List<Question> GenVillage(bool hard)
        {
            var b = new SetBuilder();
            if (!hard)
            {
                b.Add(GenWhatSee(b, false));
                b.Add(GenHave(b));
                b.Add(GenWhatSee(b, true));
                b.Add(GenPlural(b, false));
                b.Add(GenLike(b));
            }
            else
            {
                b.Add(GenWhatSee(b, false));
                b.Add(GenHave(b));
                b.Add(GenPlural(b, false));
                b.Add(GenPlural(b, true));
                b.Add(GenWhere(b));
                b.Add(GenLike(b));
                b.Add(GenPlural(b, true));
                b.Add(GenWhere(b));
            }
            return b.list;
        }

        // ------------------------------------------------------------------
        //  Monster Castle: a bit of everything
        // ------------------------------------------------------------------
        List<Question> GenCastle(bool hard)
        {
            var pool = new List<Question>();
            pool.AddRange(GenForest(hard));
            pool.AddRange(GenCave(hard));
            pool.AddRange(GenValley(hard));
            pool.AddRange(GenVillage(hard));
            var b = new SetBuilder();
            b.Add(GenWhichTeam(b, new[] { "ee", "ea", "ai" }, new[] { "ee", "ai", "oo", "ow" }));
            Shuffle(pool);
            foreach (Question q in pool) if (!b.used.Contains(q.answer)) b.Add(q);
            Shuffle(b.list);
            return b.list;
        }
    }
}
