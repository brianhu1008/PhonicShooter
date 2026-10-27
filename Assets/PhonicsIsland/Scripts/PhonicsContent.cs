// ============================================================================
//  Phonics Island - learning content: islands, lessons and questions.
//
//  Course order (one island each, the castle reviews everything):
//   1 Letter Forest    short vowels a e i o u
//   2 Sound Cave       sh ch th
//   3 Word Beach       ee ea ai ay + walk around and find the word
//   4 Spelling Valley  oo (moon / book), ou ow oi oy, spelling
//   5 Sentence Village I see / I like / I have / It is / Where is + -s / -es
//   6 Monster Castle   mixed review, final boss
//
//  Text in {text:ipa} is spoken as that IPA sound, e.g. {a:æ} says the short /a/.
//  Picture keys are files in Assets/Resources/Pics (see Tools/make_pictures.ps1).
//  "cat*2" draws two cats, "box+cat" draws a cat inside a box.
//  The actual questions are built at random from the word banks in PhonicsWordBank.cs.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        enum QType { Pick, Fill, Spell, Sort, Find, Answer }
        enum Theme { Forest, Cave, Beach, Valley, Village, Castle }

        class Question
        {
            public QType type;
            public string answer;       // option / picture key that is correct
            public string say;          // spoken when the question starts
            public string sayCorrect;   // spoken after a correct answer
            public string prompt;       // short English instruction
            public string zh;           // Chinese hint (parents can switch it off)
            public string pic;          // picture for Fill / Spell / Sort / Answer
            public string template;     // Fill: "c#t" or "I see a #."
            public string[] options;
            public string[] chunks;     // Spell: correct pieces in order
            public string[] extras;     // Spell: wrong pieces
            public string hintSound;    // shown after a wrong answer
            public bool showWords;      // Pick: print the word under each picture
            public bool hard;

            public Question H() { hard = true; return this; }
        }

        class Line
        {
            public string text, zh, say;
        }

        class IslandDef
        {
            public string nameEn, nameZh, focus, focusZh;
            public Theme theme;
            public string npcName, bossName;
            public Acc npcAcc, bossAcc;
            public Color npcFur, npcAccColor, bossAccColor;
            public float bossScale = 1.15f;
            public Line[] intro;
            public System.Func<bool, List<Question>> make;   // builds a random question set (true = Big Sister)
            public string[] findItems;  // Find questions: pictures hidden around the island
            public bool isReview;
            public int easyCount = 5, hardCount = 8;
        }

        List<IslandDef> islands;



        // ------------------------------------------------------------------
        //  Question helpers
        // ------------------------------------------------------------------
        static Line L(string text, string zh, string say = null)
        {
            return new Line { text = text, zh = zh, say = say ?? text };
        }

        static Question Pick(string answer, string say, string prompt, string zh, params string[] options)
        {
            return new Question { type = QType.Pick, answer = answer, say = say, sayCorrect = answer, prompt = prompt, zh = zh, options = options };
        }

        static Question Listen(string word, params string[] options)
        {
            return Pick(word, word, "Listen! Tap the picture.", "聽一聽，點出正確的圖。", options);
        }

        static Question StartsWith(string answer, string letter, string ipa, params string[] options)
        {
            string snd = "{" + letter + ":" + ipa + "}";
            var q = Pick(answer, "Which one starts with " + snd + "? " + snd + ", " + snd + ".",
                "Which one starts with /" + letter + "/?", "哪一個是 /" + letter + "/ 開頭的？", options);
            q.hintSound = letter;
            return q;
        }

        static Question HasSound(string answer, string letters, string ipa, params string[] options)
        {
            string snd = "{" + letters + ":" + ipa + "}";
            var q = Pick(answer, "Find the " + snd + " word. " + snd + ".", "Find the word with " + letters,
                "找出有 " + letters + " 聲音的字。", options);
            q.hintSound = letters;
            q.showWords = true;
            return q;
        }

        static Question Fill(string pic, string template, string answer, string say, string sayCorrect, string prompt, string zh, params string[] options)
        {
            return new Question
            {
                type = QType.Fill, pic = pic, template = template, answer = answer, say = say, sayCorrect = sayCorrect,
                prompt = prompt, zh = zh, options = options
            };
        }

        static Question Missing(string word, string template, string answer, params string[] options)
        {
            var q = Fill(word, template, answer, word, word, "Which letter is missing?", "缺了哪個字母？", options);
            q.hintSound = answer;
            return q;
        }

        static Question WhichSound(string word, string template, string answer, params string[] options)
        {
            var q = Fill(word, template, answer, word, word, "Which sound do you hear?", "你聽到哪個聲音？", options);
            q.hintSound = answer;
            return q;
        }

        static Question Sentence(string pic, string template, string answer, string zh, params string[] options)
        {
            string start = template.Substring(0, template.IndexOf('#')).Trim();
            string full = template.Replace("#", answer);
            return Fill(pic, template, answer, start, full, "Which word fits?", zh, options);
        }

        static Question Spell(string word, string[] chunks, string[] extras)
        {
            return new Question
            {
                type = QType.Spell, pic = word, answer = word, say = word, sayCorrect = word,
                prompt = "Listen! Build the word.", zh = "聽一聽，把字拼出來。", chunks = chunks, extras = extras
            };
        }

        static Question Sort(string word)
        {
            bool longOo = System.Array.IndexOf(LongOoWords, word) >= 0;
            return new Question
            {
                type = QType.Sort, pic = word, answer = longOo ? "moon" : "book", say = word,
                sayCorrect = word + ". Like " + (longOo ? "moon" : "book") + ".",
                prompt = "oo like moon, or oo like book?", zh = "是 moon 的 oo，還是 book 的 oo？",
                options = new[] { "moon", "book" }, hintSound = "oo"
            };
        }

        static Question Find(string item)
        {
            return new Question
            {
                type = QType.Find, answer = item, say = "Find the " + item + "!", sayCorrect = item,
                prompt = "Walk and find it!", zh = "走過去，找到它！"
            };
        }

        static Question Answer(string pic, string question, string answer, string zh, params string[] options)
        {
            return new Question
            {
                type = QType.Answer, pic = pic, prompt = question, answer = answer, say = question,
                sayCorrect = answer, zh = zh, options = options
            };
        }

        // ------------------------------------------------------------------
        //  Islands
        // ------------------------------------------------------------------
        void BuildContent()
        {
            islands = new List<IslandDef>();

            var forest = new IslandDef
            {
                nameEn = "Letter Forest", nameZh = "字母森林", focus = "a e i o u", focusZh = "字母音、短母音",
                theme = Theme.Forest, npcName = "Momo", npcAcc = Acc.Flower, npcFur = new Color(1f, 0.88f, 0.78f),
                npcAccColor = new Color(1f, 0.55f, 0.75f), bossName = "Grumpy Gus", bossAcc = Acc.Hat,
                bossAccColor = new Color(0.3f, 0.55f, 0.3f),
                intro = new[]
                {
                    L("Hi! I am Momo. Welcome to Letter Forest!", "嗨！我是 Momo，歡迎來到字母森林！"),
                    L("Short vowels say: /a/ /e/ /i/ /o/ /u/.", "短母音的聲音：a、e、i、o、u。",
                        "Short vowels say: {a:æ}. {e:ɛ}. {i:ɪ}. {o:ɑ}. {u:ʌ}."),
                    L("Oh no! Grumpy Gus is here! Answer right to throw a yuzu at him!", "糟糕！壞卡皮巴拉 Gus 來了！答對就丟柚子打他！")
                }
            };
            forest.make = GenForest;
            islands.Add(forest);

            var cave = new IslandDef
            {
                nameEn = "Sound Cave", nameZh = "組合音洞穴", focus = "sh ch th", focusZh = "子音組合",
                theme = Theme.Cave, npcName = "Kiki", npcAcc = Acc.Glasses, npcFur = new Color(1f, 0.97f, 0.85f),
                npcAccColor = new Color(0.2f, 0.2f, 0.3f), bossName = "Sneaky Sid", bossAcc = Acc.Bandana,
                bossAccColor = new Color(0.55f, 0.3f, 0.75f),
                intro = new[]
                {
                    L("Hello! I am Kiki. Two letters can make one sound!", "哈囉！我是 Kiki。兩個字母可以一起發一個聲音！"),
                    L("s + h = /sh/,  c + h = /ch/,  t + h = /th/.", "sh 像「噓」，ch 像「去」，th 要輕咬舌頭。",
                        "s, h, {sh:ʃ}. c, h, {ch:tʃə}. t, h, {th:θə}."),
                    L("Sneaky Sid is hiding in the cave! Let's go!", "鬼鬼祟祟的 Sid 躲在洞穴裡！出發！")
                }
            };
            cave.make = GenCave;
            islands.Add(cave);

            var beach = new IslandDef
            {
                nameEn = "Word Beach", nameZh = "單字海灘", focus = "ee ea ai ay", focusZh = "長母音組合、單字聽辨",
                theme = Theme.Beach, npcName = "Sunny", npcAcc = Acc.SunHat, npcFur = new Color(1f, 0.82f, 0.66f),
                npcAccColor = new Color(1f, 0.85f, 0.45f), bossName = "Salty Sam", bossAcc = Acc.TopHat,
                bossAccColor = new Color(0.2f, 0.35f, 0.7f),
                findItems = new[] { "shell", "crab", "tree", "bee", "leaf", "snail", "crayon" },
                intro = new[]
                {
                    L("Hi! I am Sunny. Two vowels together can say a long sound!", "嗨！我是 Sunny。兩個母音在一起會發長音！"),
                    L("ee, ea say /ee/.  ai, ay say /ay/.", "ee、ea 念 /i/，ai、ay 念 /e/。",
                        "{ee:i}, {ee:i}, like tree and leaf. {ay:eɪ}, {ay:eɪ}, like rain and crayon."),
                    L("Salty Sam hid things on the beach. Listen, then walk and find them!", "Salty Sam 把東西藏在沙灘上。聽一聽，走過去找出來！")
                }
            };
            beach.make = GenBeach;
            islands.Add(beach);

            var valley = new IslandDef
            {
                nameEn = "Spelling Valley", nameZh = "拼字山谷", focus = "oo ou ow oi oy", focusZh = "母音組合、看圖拼字",
                theme = Theme.Valley, npcName = "Pebble", npcAcc = Acc.Hat, npcFur = new Color(0.88f, 0.84f, 0.8f),
                npcAccColor = new Color(0.9f, 0.4f, 0.35f), bossName = "Thorny Tom", bossAcc = Acc.Bandana,
                bossAccColor = new Color(0.95f, 0.55f, 0.15f),
                intro = new[]
                {
                    L("Hello! I am Pebble. oo has two sounds!", "哈囉！我是 Pebble。oo 有兩種念法！"),
                    L("oo in moon is long. oo in book is short.", "moon 的 oo 拉長音，book 的 oo 短短的。",
                        "Moon, {oo:u}, {oo:u}, long. Book, {oo:ʊ}, {oo:ʊ}, short."),
                    L("ow in cow, oi in coin, oy in boy. Let's spell with Thorny Tom!", "cow 的 ow、coin 的 oi、boy 的 oy。來跟 Tom 比拼字！",
                        "{ow:aʊ}, like cow. {oi:ɔɪ}, like coin and boy. Let's spell with Thorny Tom!")
                }
            };
            valley.make = GenValley;
            islands.Add(valley);

            var village = new IslandDef
            {
                nameEn = "Sentence Village", nameZh = "句型村莊", focus = "I see...  -s / -es", focusZh = "簡單句型、複數",
                theme = Theme.Village, npcName = "Mayor Bun", npcAcc = Acc.TopHat, npcFur = new Color(1f, 0.92f, 0.9f),
                npcAccColor = new Color(0.25f, 0.2f, 0.3f), bossName = "Bossy Bea", bossAcc = Acc.Hat,
                bossAccColor = new Color(0.95f, 0.45f, 0.65f),
                intro = new[]
                {
                    L("Welcome! I am Mayor Bun. Let's talk in sentences!", "歡迎！我是村長 Bun。我們用句子說話吧！"),
                    L("One cat, two cats. One box, two boxes.", "一隻貓 cat，兩隻貓 cats。一個箱子 box，兩個 boxes。"),
                    L("Bossy Bea took our words! Help me get them back!", "霸道的 Bea 把我們的字搶走了！幫我拿回來！")
                }
            };
            village.make = GenVillage;
            islands.Add(village);

            var castle = new IslandDef
            {
                nameEn = "Monster Castle", nameZh = "怪物城堡", focus = "Review all!", focusZh = "混合複習",
                theme = Theme.Castle, npcName = "Grandpa Yuzu", npcAcc = Acc.Glasses, npcFur = new Color(0.82f, 0.82f, 0.82f),
                npcAccColor = new Color(0.25f, 0.2f, 0.2f), bossName = "King Grumble", bossAcc = Acc.Crown,
                bossAccColor = new Color(1f, 0.82f, 0.2f), bossScale = 1.6f, isReview = true, easyCount = 6, hardCount = 10,
                intro = new[]
                {
                    L("You came so far! I am Grandpa Yuzu.", "你走了好遠！我是柚子爺爺。"),
                    L("King Grumble is the boss of the bad capybaras.", "Grumble 國王是壞卡皮巴拉的老大。"),
                    L("Use everything you learned. You can do it!", "用上你學過的全部本領，你一定可以！")
                }
            };
            castle.make = GenCastle;
            islands.Add(castle);
        }
    }
}
