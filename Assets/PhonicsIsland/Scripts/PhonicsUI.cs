// ============================================================================
//  Phonics Island - on-screen UI (IMGUI: no Canvas or EventSystem needed)
//  Layout uses a virtual 720-pixel-high screen; width follows the window.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        GUIStyle styPanel, styText;
        Font fontEn, fontZh;
        Texture2D roundTex, starTex, lockTex;
        bool stylesReady, confirmReset;
        float gs = 1f, vw = 1280f;
        const float VH = 720f;
        static readonly Color Ink = new Color(0.25f, 0.18f, 0.38f);
        static readonly Color Cream = new Color(1f, 0.98f, 0.92f);
        static readonly Color CardBlue = new Color(0.88f, 0.93f, 1f);
        static readonly Color Green = new Color(0.55f, 0.9f, 0.55f);
        static readonly Color Coral = new Color(1f, 0.72f, 0.7f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
        static readonly Color Sky = new Color(0.6f, 0.8f, 1f);
        static readonly Color Purple = new Color(0.45f, 0.25f, 0.75f);
        readonly Dictionary<string, Texture2D> pics = new Dictionary<string, Texture2D>();

        // ------------------------------------------------------------------
        //  Drawing helpers
        // ------------------------------------------------------------------
        class Painter
        {
            public readonly int n;
            public readonly Color[] px;

            public Painter(int size)
            {
                n = size;
                px = new Color[n * n];
            }

            public void Fill(System.Func<float, float, bool> inside, Color c)
            {
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int cov = 0;
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                                if (inside((x + (sx + 0.5f) * 0.5f) / n, (y + (sy + 0.5f) * 0.5f) / n)) cov++;
                        if (cov == 0) continue;
                        float a = c.a * cov / 4f;
                        Color d = px[y * n + x];
                        float oa = a + d.a * (1f - a);
                        px[y * n + x] = new Color((c.r * a + d.r * d.a * (1f - a)) / oa, (c.g * a + d.g * d.a * (1f - a)) / oa,
                            (c.b * a + d.b * d.a * (1f - a)) / oa, oa);
                    }
                }
            }

            public Texture2D ToTexture()
            {
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                t.SetPixels(px);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Bilinear;
                t.Apply();
                return t;
            }
        }

        static System.Func<float, float, bool> Circ(float cx, float cy, float r)
        {
            return (u, v) => { float dx = u - cx, dy = v - cy; return dx * dx + dy * dy <= r * r; };
        }

        static System.Func<float, float, bool> Poly(params Vector2[] pts)
        {
            return (u, v) =>
            {
                bool pos = false, neg = false;
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    float c = (b.x - a.x) * (v - a.y) - (b.y - a.y) * (u - a.x);
                    if (c > 0f) pos = true;
                    else if (c < 0f) neg = true;
                }
                return !(pos && neg);
            };
        }

        static System.Func<float, float, bool> RRc(float x0, float y0, float x1, float y1, float r)
        {
            return (u, v) =>
            {
                if (u < x0 || u > x1 || v < y0 || v > y1) return false;
                float cx = Mathf.Clamp(u, x0 + r, x1 - r);
                float cy = Mathf.Clamp(v, y0 + r, y1 - r);
                float dx = u - cx, dy = v - cy;
                return dx * dx + dy * dy <= r * r;
            };
        }

        static Texture2D MakeStarTex()
        {
            var p = new Painter(96);
            Vector2 c = new Vector2(0.5f, 0.47f);
            for (int pass = 0; pass < 2; pass++)
            {
                float ro = pass == 0 ? 0.48f : 0.40f, ri = ro * 0.45f;
                Color col = pass == 0 ? new Color(0.9f, 0.55f, 0.1f) : new Color(1f, 0.85f, 0.2f);
                for (int i = 0; i < 5; i++)
                {
                    float a = (90f + i * 72f) * Mathf.Deg2Rad, d = 36f * Mathf.Deg2Rad;
                    Vector2 o = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ro;
                    Vector2 i0 = c + new Vector2(Mathf.Cos(a - d), Mathf.Sin(a - d)) * ri;
                    Vector2 i1 = c + new Vector2(Mathf.Cos(a + d), Mathf.Sin(a + d)) * ri;
                    p.Fill(Poly(c, i0, o, i1), col);
                }
            }
            return p.ToTexture();
        }

        static Texture2D MakeLockTex()
        {
            var p = new Painter(96);
            Color metal = new Color(0.55f, 0.55f, 0.65f);
            var outer = Circ(0.5f, 0.55f, 0.24f);
            var inner = Circ(0.5f, 0.55f, 0.15f);
            p.Fill((u, v) => v > 0.5f && outer(u, v) && !inner(u, v), metal);
            p.Fill((u, v) => v > 0.4f && v <= 0.56f && ((u > 0.26f && u < 0.35f) || (u > 0.65f && u < 0.74f)), metal);
            p.Fill(RRc(0.18f, 0.08f, 0.82f, 0.5f, 0.08f), new Color(1f, 0.75f, 0.25f));
            p.Fill(Circ(0.5f, 0.32f, 0.06f), new Color(0.35f, 0.25f, 0.2f));
            p.Fill(RRc(0.47f, 0.16f, 0.53f, 0.3f, 0.02f), new Color(0.35f, 0.25f, 0.2f));
            return p.ToTexture();
        }

        static Texture2D MakeRoundTex(int n, int r)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, r, n - r);
                    float cy = Mathf.Clamp(fy, r, n - r);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                }
            }
            t.SetPixels(px);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            roundTex = MakeRoundTex(48, 14);
            starTex = MakeStarTex();
            lockTex = MakeLockTex();
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browsers can't use the computer's fonts: use the bundled font (Noto Sans TC subset, see Tools/make_font.py).
            fontEn = Resources.Load<Font>("Fonts/GameFont");
            // 中文用佩佩喬治樂園打包的完整 Noto Sans TC (GameFont 只留了這個遊戲原本用到的字)
            fontZh = ZhText.Font;
#else
            fontEn = Font.CreateDynamicFontFromOSFont(new[] { "Comic Sans MS", "Arial" }, 32);
            fontZh = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei", "Arial" }, 32);
#endif
            styPanel = new GUIStyle();
            styPanel.normal.background = roundTex;
            styPanel.border = new RectOffset(16, 16, 16, 16);
            styText = new GUIStyle();
            styText.alignment = TextAnchor.MiddleCenter;
            styText.fontStyle = FontStyle.Bold;
            styText.wordWrap = true;
            styText.clipping = TextClipping.Overflow;
            stylesReady = true;
        }

        static bool HasCjk(string s)
        {
            foreach (char c in s) if (c >= 0x2E80) return true;
            return false;
        }

        void DrawBox(Rect r, Color c)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUI.Box(r, GUIContent.none, styPanel);
            GUI.backgroundColor = old;
        }

        void DrawText(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            bool zh = HasCjk(s);
            styText.font = zh ? fontZh : fontEn;
            styText.fontSize = zh ? Mathf.RoundToInt(size * 0.9f) : size;
            styText.alignment = anchor;
            styText.normal.textColor = c;
            GUI.Label(r, s, styText);
        }

        void DrawTextShadow(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            DrawText(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, size, new Color(0f, 0f, 0f, 0.45f * c.a), anchor);
            DrawText(r, s, size, c, anchor);
        }

        void DrawZh(Rect r, string s, TextAnchor anchor = TextAnchor.MiddleLeft, int size = 22)
        {
            if (showZh && !string.IsNullOrEmpty(s)) DrawText(r, s, size, new Color(0.45f, 0.4f, 0.55f), anchor);
        }

        // Returns true when clicked. Callers must only Defer() their reaction (see Update).
        bool DrawButton(Rect r, string label, Color tint, int size, bool enabled)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Color c = tint;
            if (!enabled) c = new Color(tint.r, tint.g, tint.b, 0.4f);
            else if (hover) c = Color.Lerp(tint, Color.white, 0.3f);
            DrawBox(new Rect(r.x, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, enabled ? 0.18f : 0.08f));
            DrawBox(r, c);
            DrawText(r, label, size, enabled ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) PlaySfx(sfxClick);
            return clicked;
        }

        void Defer(System.Action a) { pending = a; }

        void DrawGlow(Rect r)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f);
            DrawBox(new Rect(r.x - 8f, r.y - 8f, r.width + 16f, r.height + 16f), new Color(1f, 0.85f, 0.2f, 0.4f + 0.6f * pulse));
        }

        Texture2D Pic(string key)
        {
            Texture2D t;
            if (!pics.TryGetValue(key, out t))
            {
                t = Resources.Load<Texture2D>("Pics/" + key);
                pics[key] = t;
            }
            return t;
        }

        // "cat" = one picture, "cat*2" = two side by side, "box+cat" = cat inside a box
        void DrawPic(Rect r, string key, float alpha = 1f)
        {
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            int star = key.IndexOf('*');
            int plus = key.IndexOf('+');
            if (star > 0)
            {
                string name = key.Substring(0, star);
                int n = int.Parse(key.Substring(star + 1));
                float w = r.width / (n * 0.8f + 0.2f);
                for (int i = 0; i < n; i++)
                {
                    float y = r.y + r.height * 0.5f - w * 0.5f + (i % 2 == 1 ? w * 0.12f : -w * 0.06f);
                    DrawOnePic(new Rect(r.x + i * w * 0.8f, y, w, w), name);
                }
            }
            else if (plus > 0)
            {
                DrawOnePic(r, key.Substring(0, plus));
                float s = Mathf.Min(r.width, r.height) * 0.5f;
                DrawOnePic(new Rect(r.center.x - s * 0.5f, r.center.y - s * 0.35f, s, s), key.Substring(plus + 1));
            }
            else
            {
                DrawOnePic(r, key);
            }
            GUI.color = old;
        }

        void DrawOnePic(Rect r, string key)
        {
            Texture2D t = Pic(key);
            if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
            else DrawText(r, key, 36, Ink);
        }

        void DrawPictureCard(Rect r, string key, Color bg, float alpha = 1f)
        {
            DrawBox(r, bg);
            DrawPic(new Rect(r.x + 12f, r.y + 12f, r.width - 24f, r.height - 24f), key, alpha);
        }

        // ------------------------------------------------------------------
        //  Main
        // ------------------------------------------------------------------
        void OnGUI()
        {
            if (!stylesReady) InitStyles();
            gs = Screen.height / VH;
            vw = Screen.width / gs;
            GUI.matrix = Matrix4x4.Scale(new Vector3(gs, gs, 1f));

            DrawFloats();
            if (mode == Mode.Map)
            {
                DrawMapUI();
            }
            else
            {
                if (state == State.Quiz || state == State.Dialogue) DrawBossBar();
                switch (state)
                {
                    case State.Explore: DrawExploreUI(); break;
                    case State.Dialogue: DrawDialogueUI(); break;
                    case State.Quiz: DrawQuizUI(); break;
                    case State.Win: DrawWinUI(); break;
                }
            }
            if (settingsOpen) DrawSettings();

            // A click that no button used: walk / swim there.
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !settingsOpen)
            {
                Vector2 screen = new Vector2(e.mousePosition.x * gs, Screen.height - e.mousePosition.y * gs);
                Defer(() => WorldClick(screen));
                e.Use();
            }
            GUI.matrix = Matrix4x4.identity;
        }

        Vector2 ToGui(Vector3 world, out bool visible)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0f;
            return new Vector2(sp.x / gs, (Screen.height - sp.y) / gs);
        }

        void DrawFloats()
        {
            foreach (FloatText f in floats)
            {
                float u = Mathf.Clamp01(f.t / f.dur);
                bool vis;
                Vector2 g = ToGui(f.pos + Vector3.up * u, out vis);
                if (!vis) continue;
                Color c = f.col;
                c.a = 1f - u * u;
                DrawTextShadow(new Rect(g.x - 200f, g.y - 40f, 400f, 80f), f.text, f.size, c);
            }
        }

        // ------------------------------------------------------------------
        //  Map
        // ------------------------------------------------------------------
        void DrawMapUI()
        {
            // Labels wait until the opening title is gone, so text never overlaps.
            if (time >= 3f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((time - 3f) * 2f));
                DrawMapLabels();
                GUI.color = old;
            }

            if (time < 3f)
            {
                float a = Mathf.Clamp01(3f - time);
                DrawTextShadow(new Rect(vw * 0.5f - 500f, 150f, 1000f, 110f), "Phonics Island", 96, new Color(1f, 1f, 1f, a));
                DrawTextShadow(new Rect(vw * 0.5f - 500f, 260f, 1000f, 50f), "Capybara Adventure!", 40, new Color(1f, 0.95f, 0.6f, a));
                DrawTextShadow(new Rect(vw * 0.5f - 500f, 310f, 1000f, 44f), "卡皮巴拉自然發音大冒險", 34, new Color(1f, 0.95f, 0.8f, a));
            }

            DrawTextShadow(new Rect(20f, 12f, 700f, 44f), "Phonics Island", 36, Color.white, TextAnchor.MiddleLeft);
            DrawTextShadow(new Rect(20f, 54f, 900f, 30f), "Click an island to swim there!", 22, Color.white, TextAnchor.MiddleLeft);
            if (showZh) DrawTextShadow(new Rect(20f, 84f, 900f, 28f), "用滑鼠點小島，卡皮巴拉就會游過去！", 20, Color.white, TextAnchor.MiddleLeft);

            if (DrawButton(new Rect(vw - 210f, 16f, 190f, 56f), "Parents 家長", Gold, 26, !settingsOpen))
                Defer(() => { settingsOpen = true; confirmReset = false; });
            // 合併進佩佩喬治樂園：回到樂園
            if (DrawButton(new Rect(vw - 420f, 16f, 196f, 56f), "← 回樂園", Sky, 26, !settingsOpen))
                Defer(() => AppBoot.Load(AppBoot.ParkScene));

            DrawBox(new Rect(16f, 654f, 380f, 52f), new Color(1f, 1f, 1f, 0.6f));
            GUI.DrawTexture(new Rect(26f, 660f, 40f, 40f), starTex);
            DrawText(new Rect(72f, 654f, 320f, 52f), ClearedCount() + " / " + islands.Count + "   Hot spring friends", 22, Ink, TextAnchor.MiddleLeft);

            if (nearIsland >= 0 && !settingsOpen)
            {
                IslandDef d = islands[nearIsland];
                Rect r = new Rect(vw * 0.5f - 300f, 590f, 600f, 90f);
                if (nearIsland <= unlocked)
                {
                    if (DrawButton(r, "Visit " + d.nameEn + "!  (Space)", Gold, 34, true))
                    {
                        int idx = nearIsland;
                        Defer(() => TryEnterIsland(idx));
                    }
                }
                else
                {
                    DrawBox(r, new Color(1f, 1f, 1f, 0.7f));
                    DrawText(new Rect(r.x, r.y + 8f, r.width, 44f), "Locked! Finish " + islands[nearIsland - 1].nameEn + " first.", 28, Ink);
                    DrawText(new Rect(r.x, r.y + 50f, r.width, 32f), "先完成「" + islands[nearIsland - 1].nameZh + "」才能來喔！", 22, Ink);
                }
            }
        }

        // Places every island label near its island, then pushes overlapping labels apart.
        void DrawMapLabels()
        {
            var isles = new List<MapIsle>();
            var rects = new List<Rect>();
            foreach (MapIsle mi in mapIsles)
            {
                bool vis;
                Vector2 g = ToGui(mi.pos + (mi.index < 0 ? new Vector3(0f, 3f, 0f) : new Vector3(0f, 1f, -2.5f)), out vis);
                if (!vis) continue;
                Vector2 size = mi.index < 0 ? new Vector2(200f, 40f) : new Vector2(220f, 84f);
                isles.Add(mi);
                rects.Add(new Rect(g.x - size.x * 0.5f, g.y - size.y * 0.5f, size.x, size.y));
            }

            const float gap = 8f;
            for (int iter = 0; iter < 40; iter++)
            {
                bool moved = false;
                for (int i = 0; i < rects.Count; i++)
                {
                    for (int j = i + 1; j < rects.Count; j++)
                    {
                        Rect a = rects[i], b = rects[j];
                        float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) + gap;
                        float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) + gap;
                        if (ox <= 0f || oy <= 0f) continue;
                        moved = true;
                        // Push along the direction that needs the smaller move.
                        if (ox < oy)
                        {
                            float s = a.center.x < b.center.x ? -0.5f : 0.5f;
                            a.x += s * ox;
                            b.x -= s * ox;
                        }
                        else
                        {
                            float s = a.center.y < b.center.y ? -0.5f : 0.5f;
                            a.y += s * oy;
                            b.y -= s * oy;
                        }
                        rects[i] = a;
                        rects[j] = b;
                    }
                }
                // Keep labels on screen, below the title and above the bottom bar.
                for (int i = 0; i < rects.Count; i++)
                {
                    Rect r = rects[i];
                    r.x = Mathf.Clamp(r.x, 10f, vw - 10f - r.width);
                    r.y = Mathf.Clamp(r.y, 118f, VH - 76f - r.height);
                    rects[i] = r;
                }
                if (!moved) break;
            }

            for (int i = 0; i < isles.Count; i++) DrawIsleLabel(isles[i], rects[i]);
        }

        void DrawIsleLabel(MapIsle mi, Rect r)
        {
            if (mi.index < 0)
            {
                DrawBox(r, new Color(1f, 1f, 1f, 0.6f));
                DrawText(r, "Hot Spring 溫泉", 21, Ink);
                return;
            }
            IslandDef d = islands[mi.index];
            bool locked = mi.index > unlocked;
            bool hover = !settingsOpen && r.Contains(Event.current.mousePosition);
            DrawBox(r, locked ? new Color(0.85f, 0.85f, 0.95f, hover ? 0.7f : 0.5f) : new Color(1f, 1f, 1f, hover ? 0.85f : 0.6f));
            DrawText(new Rect(r.x, r.y + 2f, r.width, 28f), d.nameEn, 21, Ink);
            DrawText(new Rect(r.x, r.y + 28f, r.width, 24f), d.nameZh, 18, Ink);
            Rect status = new Rect(r.x, r.y + 53f, r.width, 28f);
            if (locked)
            {
                GUI.DrawTexture(new Rect(r.center.x - 15f, status.y, 30f, 30f), lockTex);
            }
            else if (cleared[mi.index])
            {
                GUI.DrawTexture(new Rect(r.x + 50f, status.y, 28f, 28f), starTex);
                DrawText(new Rect(r.x + 82f, status.y, 120f, 28f), "Cleared!", 19, new Color(0.2f, 0.6f, 0.3f), TextAnchor.MiddleLeft);
            }
            else
            {
                DrawText(status, d.focus, 18, Purple);
            }
            int idx = mi.index;
            if (!settingsOpen && GUI.Button(r, GUIContent.none, GUIStyle.none)) Defer(() => GoToIsland(idx));
        }

        void DrawSettings()
        {
            DrawBox(new Rect(0f, 0f, vw, VH), new Color(0f, 0f, 0f, 0.45f));
            Rect p = new Rect(vw * 0.5f - 380f, 90f, 760f, 540f);
            DrawBox(p, Cream);
            DrawText(new Rect(p.x, p.y + 16f, p.width, 50f), "Parents  家長設定", 38, Ink);

            DrawText(new Rect(p.x + 40f, p.y + 84f, 680f, 34f), "Level  難度", 26, Ink, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(p.x + 40f, p.y + 124f, 330f, 80f), "Little Brother\n弟弟模式 (圖片、單字)", easyMode ? Green : CardBlue, 22, true))
                Defer(() => { easyMode = true; SaveProgress(); });
            if (DrawButton(new Rect(p.x + 390f, p.y + 124f, 330f, 80f), "Big Sister\n姊姊模式 (句子、拼字)", !easyMode ? Green : CardBlue, 22, true))
                Defer(() => { easyMode = false; SaveProgress(); });

            DrawText(new Rect(p.x + 40f, p.y + 222f, 680f, 34f), "Chinese hints  中文提示", 26, Ink, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(p.x + 40f, p.y + 262f, 160f, 60f), "開 On", showZh ? Green : CardBlue, 24, true))
                Defer(() => { showZh = true; SaveProgress(); });
            if (DrawButton(new Rect(p.x + 220f, p.y + 262f, 160f, 60f), "關 Off", !showZh ? Green : CardBlue, 24, true))
                Defer(() => { showZh = false; SaveProgress(); });

            DrawText(new Rect(p.x + 40f, p.y + 340f, 680f, 34f), "Islands  小島", 26, Ink, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(p.x + 40f, p.y + 380f, 330f, 60f), "Open all 全部開放", Sky, 24, unlocked < islands.Count - 1))
                Defer(UnlockAll);
            string resetLabel = confirmReset ? "Sure? 確定重設？" : "Reset 重設進度";
            if (DrawButton(new Rect(p.x + 390f, p.y + 380f, 330f, 60f), resetLabel, confirmReset ? Coral : new Color(1f, 0.85f, 0.8f), 24, true))
            {
                if (confirmReset) Defer(() => { ResetProgress(); confirmReset = false; });
                else Defer(() => confirmReset = true);
            }

            if (DrawButton(new Rect(p.center.x - 120f, p.yMax - 82f, 240f, 62f), "Close 關閉", Green, 28, true))
                Defer(() => settingsOpen = false);

            DrawText(new Rect(p.x, p.yMax + 8f, p.width, 24f),
                "Credits: 3D capybara \"Capybara\" by Poly by Google (CC BY 3.0, poly.pizza)   Sounds: Kenney.nl (CC0)", 15, Color.white);
        }

        // ------------------------------------------------------------------
        //  Island
        // ------------------------------------------------------------------
        void DrawIslandHeader(string sub)
        {
            DrawTextShadow(new Rect(20f, 12f, 900f, 44f), cur.nameEn, 34, Color.white, TextAnchor.MiddleLeft);
            DrawTextShadow(new Rect(20f, 52f, 900f, 32f), cur.nameZh + "   " + cur.focus, 22, Color.white, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(sub)) DrawTextShadow(new Rect(20f, 86f, 900f, 30f), sub, 22, Color.white, TextAnchor.MiddleLeft);
        }

        bool DrawMapButton()
        {
            return DrawButton(new Rect(vw - 210f, 16f, 190f, 56f), "Map 地圖", Sky, 26, !busy);
        }

        void DrawExploreUI()
        {
            DrawIslandHeader("Click " + cur.npcName + " to talk!");
            if (showZh) DrawTextShadow(new Rect(20f, 118f, 900f, 28f), "點一下 " + cur.npcName + "，就會走過去聊天！", 20, Color.white, TextAnchor.MiddleLeft);
            if (DrawMapButton()) Defer(() => EnterMap(curIndex));
            Rect talk = new Rect(vw * 0.5f - 230f, 600f, 460f, 80f);
            if (DrawButton(talk, "Talk to " + cur.npcName + "!", Gold, 32, !busy)) Defer(GoTalk);
        }

        void DrawBossBar()
        {
            bool vis;
            Vector2 g = ToGui(boss.position + Vector3.up * (2.3f * cur.bossScale), out vis);
            if (!vis) return;
            Rect bar = new Rect(g.x - 110f, g.y - 14f, 220f, 28f);
            DrawBox(bar, new Color(0.2f, 0.1f, 0.3f, 0.8f));
            float frac = state == State.Dialogue ? 1f : (maxHp > 0 ? (float)hp / maxHp : 0f);
            if (frac > 0f)
            {
                DrawBox(new Rect(bar.x + 4f, bar.y + 4f, Mathf.Max(16f, (bar.width - 8f) * frac), bar.height - 8f),
                    Color.Lerp(new Color(1f, 0.4f, 0.5f), new Color(0.4f, 0.95f, 0.5f), frac));
            }
            DrawTextShadow(new Rect(bar.x - 60f, bar.y - 36f, bar.width + 120f, 32f), cur.bossName, 26, Color.white);
        }

        void DrawDialogueUI()
        {
            DrawIslandHeader(null);
            Line line = cur.intro[Mathf.Clamp(dialogueIndex, 0, cur.intro.Length - 1)];
            float pw = Mathf.Min(1040f, vw - 30f);
            Rect p = new Rect((vw - pw) * 0.5f, 470f, pw, 220f);
            DrawBox(p, Cream);
            Rect nameTag = new Rect(p.x + 20f, p.y - 26f, 240f, 52f);
            DrawBox(nameTag, new Color(1f, 0.7f, 0.3f));
            DrawText(nameTag, cur.npcName, 28, Color.white);
            DrawText(new Rect(p.x + 40f, p.y + 30f, p.width - 80f, 100f), line.text, 32, Ink, TextAnchor.MiddleLeft);
            DrawZh(new Rect(p.x + 40f, p.y + 128f, p.width - 340f, 34f), line.zh);
            if (DrawButton(new Rect(p.xMax - 420f, p.yMax - 78f, 160f, 60f), "Listen", Sky, 28, true)) Defer(() => Speak(line.say));
            bool last = dialogueIndex >= cur.intro.Length - 1;
            if (DrawButton(new Rect(p.xMax - 240f, p.yMax - 78f, 200f, 60f), last ? "Let's go!" : "Next", Green, 30, true)) Defer(AdvanceDialogue);
        }

        void DrawWinUI()
        {
            DrawIslandHeader(null);
            float pw = Mathf.Min(860f, vw - 30f);
            Rect p = new Rect((vw - pw) * 0.5f, 360f, pw, 330f);
            DrawBox(p, Cream);
            bool final = cur.isReview;
            float bounce = Mathf.Abs(Mathf.Sin(time * 3f)) * 10f;
            GUI.DrawTexture(new Rect(p.center.x - 45f, p.y - 60f - bounce, 90f, 90f), starTex);
            DrawText(new Rect(p.x, p.y + 30f, p.width, 80f), final ? "You saved Phonics Island!" : "Island cleared!", final ? 54 : 64, Ink);
            DrawText(new Rect(p.x, p.y + 110f, p.width, 46f), cur.bossName + " is your friend now!", 32, Ink);
            DrawZh(new Rect(p.x, p.y + 156f, p.width, 34f), cur.bossName + " 變成好朋友，去溫泉泡湯囉！", TextAnchor.MiddleCenter, 24);
            if (DrawButton(new Rect(p.center.x - 320f, p.yMax - 96f, 300f, 70f), "Back to map", Green, 32, true)) Defer(() => EnterMap(curIndex));
            if (DrawButton(new Rect(p.center.x + 20f, p.yMax - 96f, 300f, 70f), "Play again", Gold, 32, true)) Defer(ResetIsland);
        }

        void DrawQuizUI()
        {
            Question q = CurQ;
            if (q == null) return;
            DrawIslandHeader("Round " + (qIndex + 1) + " / " + questions.Count + (combo >= 2 ? "     Combo x" + combo + "!" : ""));

            if (q.type == QType.Find)
            {
                DrawFind(q);
                return;
            }

            float pw = Mathf.Min(1120f, vw - 30f);
            Rect panel = new Rect((vw - pw) * 0.5f, 400f, pw, 310f);
            if (!string.IsNullOrEmpty(hint))
            {
                Rect hb = new Rect(panel.x + 40f, 334f, pw - 80f, 56f);
                DrawBox(hb, new Color(1f, 0.93f, 0.6f));
                DrawText(hb, hint, 26, Ink);
            }
            DrawBox(panel, Cream);

            switch (q.type)
            {
                case QType.Pick: DrawPick(panel, q); break;
                case QType.Fill: DrawFill(panel, q); break;
                case QType.Spell: DrawSpell(panel, q); break;
                case QType.Sort: DrawSort(panel, q); break;
                case QType.Answer: DrawAnswer(panel, q); break;
            }
        }

        void DrawListenButton(Rect panel, Question q)
        {
            Rect r = new Rect(panel.xMax - 210f, panel.y + 14f, 180f, 54f);
            if (DrawButton(r, "Listen", Sky, 28, !busy)) Defer(() => SpeakQuestion(q));
        }

        void DrawPrompt(Rect r, Question q)
        {
            DrawText(new Rect(r.x, r.y, r.width, 50f), q.prompt, 32, Ink, TextAnchor.MiddleLeft);
            DrawZh(new Rect(r.x, r.y + 46f, r.width, 28f), q.zh);
        }

        void DrawPick(Rect p, Question q)
        {
            DrawPrompt(new Rect(p.x + 30f, p.y + 10f, p.width - 270f, 80f), q);
            DrawListenButton(p, q);

            int n = q.options.Length;
            const float cw = 200f, gap = 36f;
            float total = n * cw + (n - 1) * gap;
            float x0 = p.x + (p.width - total) * 0.5f;
            for (int d = 0; d < n; d++)
            {
                int oi = optOrder[d];
                string w = q.options[oi];
                Rect r = new Rect(x0 + d * (cw + gap), p.y + 88f, cw, 200f);
                bool wrong = optWrong[oi];
                bool isCorrect = answeredCorrect && w == q.answer;
                if (wrongCount >= 2 && w == q.answer && !answeredCorrect) DrawGlow(r);
                Color bg = isCorrect ? Green : (wrong ? Coral : CardBlue);
                DrawBox(r, bg);
                if (q.showWords)
                {
                    DrawPic(new Rect(r.x + 25f, r.y + 10f, r.width - 50f, 140f), w, wrong ? 0.4f : 1f);
                    DrawText(new Rect(r.x, r.yMax - 52f, r.width, 44f), w, 34, Ink);
                }
                else
                {
                    DrawPic(new Rect(r.x + 12f, r.y + 12f, r.width - 24f, r.height - 24f), w, wrong ? 0.4f : 1f);
                }
                if (!busy && !wrong && GUI.Button(r, GUIContent.none, GUIStyle.none)) Defer(() => OnPick(oi));
            }
        }

        string TemplateText(Question q, bool filled)
        {
            if (filled) return q.template.Replace("#", q.answer);
            if (q.template.Contains(" ")) return q.template.Replace("#", "____");
            var blanks = new List<string>();
            for (int i = 0; i < q.answer.Length; i++) blanks.Add("_");
            return q.template.Replace("#", " " + string.Join(" ", blanks.ToArray()) + " ").Trim();
        }

        void DrawFill(Rect p, Question q)
        {
            DrawPictureCard(new Rect(p.x + 40f, p.y + 40f, 220f, 240f), q.pic, CardBlue);
            float tx = p.x + 300f;
            float avail = p.width - 340f;
            DrawPrompt(new Rect(tx, p.y + 10f, avail - 210f, 80f), q);
            DrawListenButton(p, q);
            bool sentence = q.template.Contains(" ");
            DrawText(new Rect(tx, p.y + 86f, avail, 90f), TemplateText(q, answeredCorrect), sentence ? 52 : 72, Purple, TextAnchor.MiddleLeft);

            int n = q.options.Length;
            float bw = Mathf.Min(216f, (avail - 16f * (n - 1)) / n);
            for (int d = 0; d < n; d++)
            {
                int oi = optOrder[d];
                string w = q.options[oi];
                Rect r = new Rect(tx + d * (bw + 16f), p.y + 192f, bw, 82f);
                bool wrong = optWrong[oi];
                bool isCorrect = answeredCorrect && w == q.answer;
                if (wrongCount >= 2 && w == q.answer && !answeredCorrect) DrawGlow(r);
                Color tint = isCorrect ? Green : (wrong ? Coral : Gold);
                if (DrawButton(r, w, tint, 40, !busy && !wrong)) Defer(() => OnPick(oi));
            }
        }

        void DrawAnswer(Rect p, Question q)
        {
            DrawPictureCard(new Rect(p.x + 40f, p.y + 40f, 220f, 240f), q.pic, CardBlue);
            float tx = p.x + 300f;
            float avail = p.width - 340f;
            DrawText(new Rect(tx, p.y + 10f, avail - 210f, 56f), q.prompt, 42, Purple, TextAnchor.MiddleLeft);
            DrawZh(new Rect(tx, p.y + 62f, avail - 210f, 28f), q.zh);
            DrawListenButton(p, q);

            for (int d = 0; d < q.options.Length; d++)
            {
                int oi = optOrder[d];
                string w = q.options[oi];
                Rect r = new Rect(tx, p.y + 104f + d * 66f, avail, 56f);
                bool wrong = optWrong[oi];
                bool isCorrect = answeredCorrect && w == q.answer;
                if (wrongCount >= 2 && w == q.answer && !answeredCorrect) DrawGlow(r);
                Color tint = isCorrect ? Green : (wrong ? Coral : Gold);
                if (DrawButton(r, w, tint, 32, !busy && !wrong)) Defer(() => OnPick(oi));
            }
        }

        void DrawSort(Rect p, Question q)
        {
            DrawPrompt(new Rect(p.x + 30f, p.y + 10f, p.width - 270f, 80f), q);
            DrawListenButton(p, q);

            Rect word = new Rect(p.center.x - 95f, p.y + 90f, 190f, 200f);
            DrawPictureCard(word, q.pic, answeredCorrect ? Green : CardBlue);

            for (int d = 0; d < 2; d++)
            {
                int oi = d;
                string key = q.options[oi];
                float bx = d == 0 ? p.x + 50f : p.xMax - 350f;
                Rect r = new Rect(bx, p.y + 100f, 300f, 180f);
                bool wrong = optWrong[oi];
                bool isCorrect = answeredCorrect && key == q.answer;
                if (wrongCount >= 2 && key == q.answer && !answeredCorrect) DrawGlow(r);
                Color tint = isCorrect ? Green : (wrong ? Coral : Gold);
                bool hover = !busy && !wrong && r.Contains(Event.current.mousePosition);
                DrawBox(new Rect(r.x, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, 0.18f));
                DrawBox(r, hover ? Color.Lerp(tint, Color.white, 0.3f) : tint);
                DrawPic(new Rect(r.x + 20f, r.y + 20f, 120f, 120f), key, wrong ? 0.4f : 1f);
                DrawText(new Rect(r.x + 140f, r.y + 20f, 150f, 70f), "oo", 60, Purple);
                DrawText(new Rect(r.x + 140f, r.y + 90f, 150f, 50f), "like " + key, 28, Ink);
                DrawText(new Rect(r.x, r.y + 140f, r.width, 36f), key == "moon" ? "long  長音" : "short  短音", 22, Ink);
                if (!busy && !wrong && GUI.Button(r, GUIContent.none, GUIStyle.none)) Defer(() => OnPick(oi));
            }
        }

        void DrawSpell(Rect p, Question q)
        {
            DrawPrompt(new Rect(p.x + 30f, p.y + 10f, 480f, 80f), q);
            if (wrongCount >= 3) DrawText(new Rect(p.x + 520f, p.y + 12f, 200f, 56f), q.answer, 46, Purple);
            DrawListenButton(p, q);

            DrawPictureCard(new Rect(p.x + 30f, p.y + 88f, 190f, 200f), q.pic, CardBlue);

            float sx0 = p.x + 260f;
            int n = q.chunks.Length;
            float sw = Mathf.Min(118f, (p.width - 290f - 14f * (n - 1)) / n);
            for (int k = 0; k < n; k++)
            {
                Rect r = new Rect(sx0 + k * (sw + 14f), p.y + 88f, sw, 90f);
                Color sc;
                if (spellGreen) sc = Green;
                else if (spellRed) sc = Coral;
                else if (slot[k] >= 0 && slotLocked[k]) sc = new Color(1f, 0.9f, 0.5f);
                else if (slot[k] >= 0) sc = CardBlue;
                else sc = new Color(0.85f, 0.85f, 0.92f, 0.8f);
                DrawBox(r, sc);
                if (slot[k] >= 0)
                {
                    DrawText(r, tileLabel[slot[k]], 52, Ink);
                    int ck = k;
                    if (!busy && !slotLocked[k] && GUI.Button(r, GUIContent.none, GUIStyle.none)) Defer(() => OnSlot(ck));
                }
            }

            int tn = tileLabel.Length;
            float tw = Mathf.Min(110f, (p.width - 290f - 14f * (tn - 1)) / tn);
            int glowTile = wrongCount >= 3 ? HintTile(q) : -1;
            for (int i = 0; i < tn; i++)
            {
                Rect r = new Rect(sx0 + i * (tw + 14f), p.y + 198f, tw, 90f);
                if (tileUsed[i])
                {
                    DrawBox(r, new Color(0.85f, 0.85f, 0.9f, 0.35f));
                    continue;
                }
                if (i == glowTile) DrawGlow(r);
                int ci = i;
                if (DrawButton(r, tileLabel[i], Gold, 52, !busy)) Defer(() => OnTile(ci));
            }
        }

        void DrawFind(Question q)
        {
            for (int i = 0; i < findSpots.Count; i++)
            {
                FindSpot s = findSpots[i];
                if (s.collected) continue;
                bool vis;
                Vector2 g = ToGui(s.pos + Vector3.up * 1.5f, out vis);
                if (!vis) continue;
                bool near = i == nearSpot;
                float size = near ? 120f : 84f;
                Rect r = new Rect(g.x - size * 0.5f, g.y - size, size, size);
                if (wrongCount >= 2 && s.key == q.answer) DrawGlow(r);
                Color bg = time - s.wrongFlash < 0.8f && s.wrongFlash > 0f ? Coral : (near ? Gold : new Color(1f, 1f, 1f, 0.6f));
                DrawPictureCard(r, s.key, bg);
                int si = i;
                if (near && DrawButton(new Rect(g.x - 110f, g.y + 6f, 220f, 52f), "This one!", Green, 24, !busy)) Defer(() => OnFindPick(si));
                if (!busy && GUI.Button(r, GUIContent.none, GUIStyle.none)) Defer(() => GoToSpot(si));
            }

            // Banner at the bottom, drawn last so the picture cards never cover it.
            Rect b = new Rect(vw * 0.5f - 330f, 590f, 660f, 112f);
            DrawBox(b, Cream);
            bool showWord = !easyMode || wrongCount >= 1;
            DrawText(new Rect(b.x + 30f, b.y + 8f, 420f, 60f), showWord ? "Find: " + q.answer : "Listen and find!", 40, Purple, TextAnchor.MiddleLeft);
            DrawZh(new Rect(b.x + 30f, b.y + 66f, 420f, 34f), q.zh);
            if (DrawButton(new Rect(b.xMax - 210f, b.y + 28f, 180f, 56f), "Listen", Sky, 28, !busy)) Defer(() => SpeakQuestion(q));
            if (!string.IsNullOrEmpty(hint))
            {
                Rect hb = new Rect(b.x + 20f, b.y - 60f, b.width - 40f, 50f);
                DrawBox(hb, new Color(1f, 0.93f, 0.6f));
                DrawText(hb, hint, 24, Ink);
            }
        }
    }
}
