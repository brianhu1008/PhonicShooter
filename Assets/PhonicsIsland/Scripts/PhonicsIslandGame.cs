// ============================================================================
//  Phonics Island - Capybara Adventure  (core: flow, movement, camera, progress)
//
//  HOW TO USE
//   Open the scene "SampleScene" and press Play (click the Game view once so
//   the keyboard works). Everything is created from code; no scene setup.
//   Build an .exe with the menu  Phonics Island > Build Windows EXE.
//
//  CONTROLS   Move / swim: W A S D or arrow keys   |   Talk / Pick: Space or mouse
//
//  FILES      PhonicsIslandGame.cs  this file
//             PhonicsContent.cs     islands, lessons, questions
//             PhonicsWorld.cs       map, islands and capybaras built from primitives
//             PhonicsQuiz.cs        questions, attacks, hints
//             PhonicsAudio.cs       voice (recorded clips or Windows TTS) and sound effects
//             PhonicsUI.cs          all on-screen UI
//
//  Progress (unlocked islands, settings) is saved on this computer only.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame : MonoBehaviour
    {
        static readonly string[] AutoScenes = { "SampleScene", "PhonicsIsland" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (System.Array.IndexOf(AutoScenes, sceneName) < 0) return;
            if (FindAnyObjectByType<PhonicsIslandGame>() != null) return;
            new GameObject("PhonicsIslandGame").AddComponent<PhonicsIslandGame>();
        }

        enum Mode { Map, Island }
        enum State { Explore, Dialogue, Quiz, Win }

        class FloatText
        {
            public string text;
            public Vector3 pos;
            public float t, dur;
            public Color col;
            public int size;
        }

        class Obstacle
        {
            public Vector3 pos;
            public float r;
        }

        // ------------------------------------------------------------------
        //  State
        // ------------------------------------------------------------------
        Mode mode = Mode.Map;
        State state = State.Explore;
        int curIndex = -1;          // island being played
        IslandDef cur;
        bool busy, walking, nearNpc, settingsOpen, devCloseup;
        string devModelView;
        float time;
        int dialogueIndex;
        System.Action pending;
        Vector3 lookPoint;
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<FloatText> floats = new List<FloatText>();

        const float IslandWalkRadius = 6.7f;
        const float SeaRadius = 36f;
        const float SwimY = -0.45f;
        static readonly Vector3 MapCamOffset = new Vector3(0f, 27f, -11f);
        static readonly Vector3 MapLookAhead = new Vector3(0f, 0.5f, 3f);

        // Saved progress
        int unlocked;               // highest island index that can be entered
        bool[] cleared;
        bool easyMode = true;       // Little Brother (easy) / Big Sister (full)
        bool showZh = true;

        // ------------------------------------------------------------------
        //  Unity callbacks
        // ------------------------------------------------------------------
        void Awake()
        {
            SetupScene();
            InitAudio();
            BuildContent();
            LoadProgress();
            BuildHero();
            BuildMap();
            EnterMap(-1);
            DevStart();
            Debug.Log("[PhonicsIsland] Started. W A S D / arrow keys to swim, Space to visit an island.");
        }

        // Saves a screenshot of the game itself after a few seconds, then quits.
        // (Invoke, because entering an island stops all coroutines.)
        string devShotPath;
        void DevShotNow() { ScreenCapture.CaptureScreenshot(devShotPath); Invoke(nameof(DevQuit), 1f); }
        void DevQuit() { Application.Quit(); }

        // Builds many random question sets and checks them (counts, answers, pictures). Results go to the log.
        void SelfTest()
        {
            int problems = 0, sets = 0;
            var seen = new HashSet<string>();
            for (int isl = 0; isl < islands.Count; isl++)
            {
                cur = islands[isl];
                for (int run = 0; run < 300; run++)
                {
                    easyMode = run % 2 == 0;
                    if (cur.findItems != null) cur.findItems = PickBeachItems();
                    List<Question> qs;
                    try { qs = MakeQuestionSet(cur); }
                    catch (System.Exception e) { Debug.Log("[SelfTest] island " + isl + " crashed: " + e); problems++; continue; }
                    sets++;
                    int want = easyMode ? cur.easyCount : cur.hardCount;
                    if (qs.Count < want) { Debug.Log("[SelfTest] island " + isl + " only " + qs.Count + "/" + want + " questions"); problems++; }
                    foreach (Question q in qs)
                    {
                        seen.Add(q.answer);
                        var keys = new List<string>();
                        if (q.pic != null) keys.Add(q.pic);
                        if (q.type == QType.Pick || q.type == QType.Sort) keys.AddRange(q.options);
                        if (q.type == QType.Find) keys.Add(q.answer);
                        foreach (string k in keys)
                            foreach (string part in k.Split('+'))
                            {
                                string name = part.Split('*')[0];
                                if (Pic(name) == null) { Debug.Log("[SelfTest] missing picture: " + name); problems++; }
                            }
                        if (q.options != null && System.Array.IndexOf(q.options, q.answer) < 0) { Debug.Log("[SelfTest] answer not in options: " + q.answer); problems++; }
                        if (q.options != null && new HashSet<string>(q.options).Count != q.options.Length) { Debug.Log("[SelfTest] duplicate options: " + string.Join(",", q.options)); problems++; }
                        if (q.type == QType.Spell && string.Join("", q.chunks) != q.answer) { Debug.Log("[SelfTest] bad chunks: " + q.answer); problems++; }
                    }
                }
            }
            Debug.Log("[SelfTest] done: " + sets + " sets, " + seen.Count + " different answers, " + problems + " problems");
        }

        // Testing shortcuts:  PhonicsIsland.exe -island 2 -q 3 [-full] [-win] [-dialogue]
        void DevStart()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int island = -1, q = -1;
            bool win = false, dialogue = false, talk = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-island" && i + 1 < args.Length) int.TryParse(args[i + 1], out island);
                else if (args[i] == "-q" && i + 1 < args.Length) int.TryParse(args[i + 1], out q);
                else if (args[i] == "-full") easyMode = false;
                else if (args[i] == "-easy") easyMode = true;
                else if (args[i] == "-win") win = true;
                else if (args[i] == "-dialogue") dialogue = true;
                else if (args[i] == "-closeup") devCloseup = true;
                else if (args[i] == "-parents") settingsOpen = true;
                else if (args[i] == "-goto" && i + 1 < args.Length) { int g; if (int.TryParse(args[i + 1], out g)) GoToIsland(g); }
                else if (args[i] == "-talk") talk = true;
                else if (args[i] == "-selftest") { SelfTest(); Application.Quit(); return; }
                else if (args[i] == "-modelview" && i + 1 < args.Length) devModelView = args[i + 1];
                else if (args[i] == "-shot" && i + 1 < args.Length) { devShotPath = args[i + 1]; Invoke(nameof(DevShotNow), 4f); }
            }
            if (devModelView != null)
            {
                // Orthographic view of a capybara: 360 px per unit at 720p, centered on (y 1.0, z 0.5) / (x 0, y 1.0).
                mapRoot.gameObject.SetActive(false);
                Capy probe = BuildCapy("Probe", null, Vector3.zero, Color.white, 1f, true, Color.green, Acc.Hat, Acc.Yuzu);
                hero.gameObject.SetActive(false);
                cam.orthographic = true;
                cam.orthographicSize = 1f;
                bool side = devModelView == "side";
                cam.transform.position = side ? new Vector3(10f, 1f, 0.5f) : new Vector3(0f, 1f, 10f);
                cam.transform.rotation = Quaternion.LookRotation(side ? Vector3.left : Vector3.back);
                enabled = false;
                return;
            }
            if (island < 0 || island >= islands.Count) return;
            time = 10f;
            EnterIsland(island);
            if (dialogue) { BeginDialogue(); return; }
            if (talk) { GoTalk(); return; }
            if (q < 0 && !win) return;
            hero.position = battleSpot;
            StartQuiz();
            if (q > 0 && q < questions.Count) { qIndex = q; StartQuestion(); }
            if (win) StartCoroutine(WinRoutine());
        }

        void OnDestroy()
        {
            StopTts();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;

            if (pending != null)
            {
                System.Action a = pending;
                pending = null;
                a();
            }

            for (int i = floats.Count - 1; i >= 0; i--)
            {
                floats[i].t += dt;
                if (floats[i].t > floats[i].dur) floats.RemoveAt(i);
            }

            RunAnims(dt);

            walking = false;
            if (settingsOpen)
            {
                if (BackPressed()) settingsOpen = false;
            }
            else if (mode == Mode.Map)
            {
                if (BackPressed()) { AppBoot.Load(AppBoot.ParkScene); return; } // Esc 回佩佩喬治樂園
                HandleSwim(dt);
                CheckClickArrival();
            }
            else
            {
                switch (state)
                {
                    case State.Explore:
                        HandleWalk(dt);
                        CheckClickArrival();
                        if (state == State.Explore && nearNpc && ConfirmPressed()) BeginDialogue();
                        break;
                    case State.Dialogue:
                        if (ConfirmPressed()) AdvanceDialogue();
                        break;
                    case State.Quiz:
                        Question q = CurQ;
                        if (q != null && q.type == QType.Find && !busy)
                        {
                            HandleWalk(dt);
                            CheckClickArrival();
                            if (!busy && nearSpot >= 0 && ConfirmPressed()) OnFindPick(nearSpot);
                        }
                        break;
                }
                FaceTargets(dt);
            }

            float bob;
            if (mode == Mode.Map) bob = Mathf.Sin(time * (walking ? 7f : 2f)) * 0.06f;
            else bob = walking ? Mathf.Abs(Mathf.Sin(time * 10f)) * 0.12f : Mathf.Abs(Mathf.Sin(time * 2f)) * 0.03f;
            heroCapy.visual.localPosition = new Vector3(0f, bob, 0f);
        }

        void LateUpdate()
        {
            Vector3 desiredPos, desiredLook;
            if (mode == Mode.Map)
            {
                desiredLook = hero.position + MapLookAhead;
                desiredPos = hero.position + MapCamOffset;
            }
            else if (CurQ != null && CurQ.type == QType.Find)
            {
                // Higher up so more of the beach is visible while searching.
                desiredLook = hero.position * 0.5f + Vector3.up * 0.5f;
                desiredPos = hero.position * 0.5f + new Vector3(0f, 10.5f, -9f);
            }
            else if (state == State.Explore || state == State.Win)
            {
                desiredLook = hero.position + Vector3.up * 1f;
                desiredPos = hero.position + new Vector3(0f, 7f, -9f);
            }
            else
            {
                Vector3 mid = (battleSpot + bossPos) * 0.5f;
                desiredLook = mid + new Vector3(0f, -2.0f, 0f);
                desiredPos = mid + new Vector3(0f, 2.8f, -8.5f) * Mathf.Lerp(1f, 1.2f, (cur.bossScale - 1f) / 0.6f);
            }
            if (devCloseup && mode == Mode.Island)
            {
                desiredLook = (battleSpot + bossPos) * 0.5f + Vector3.up * 1.1f;
                desiredPos = desiredLook + new Vector3(-0.5f, 0.9f, -4.2f);
            }
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPos, k);
            lookPoint = Vector3.Lerp(lookPoint, desiredLook, k);
            cam.transform.LookAt(lookPoint);
        }

        void SnapCamera()
        {
            if (mode == Mode.Map)
            {
                lookPoint = hero.position + MapLookAhead;
                cam.transform.position = hero.position + MapCamOffset;
            }
            else
            {
                lookPoint = hero.position + Vector3.up;
                cam.transform.position = hero.position + new Vector3(0f, 7f, -9f);
            }
            cam.transform.LookAt(lookPoint);
        }

        // ------------------------------------------------------------------
        //  Input (works with the new Input System or the old Input Manager)
        // ------------------------------------------------------------------
        static Vector2 ReadMove()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
#endif
            return new Vector2(x, y);
        }

        static bool ConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ------------------------------------------------------------------
        //  Moving around
        // ------------------------------------------------------------------
        // Moves the hero on the ground, pushes it out of obstacles and keeps it inside maxRadius.
        // Keys move directly; otherwise the hero heads to the spot the player clicked.
        bool MoveHero(float dt, float speed, float maxRadius, float y)
        {
            Vector2 m = ReadMove();
            Vector3 dir = new Vector3(m.x, 0f, m.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (dir.sqrMagnitude > 0.01f)
            {
                ClearClickMove();
            }
            else if (hasClickTarget)
            {
                Vector3 d = clickTarget - hero.position;
                d.y = 0f;
                if (d.magnitude < 0.3f || stuckTime > 0.6f)
                {
                    hasClickTarget = false;
                    return false;
                }
                dir = d.normalized;
            }
            if (dir.sqrMagnitude < 0.01f) return false;
            Vector3 before = hero.position;

            Vector3 np = hero.position + dir * speed * dt;
            foreach (Obstacle o in obstacles)
            {
                Vector3 d = np - o.pos;
                d.y = 0f;
                float minD = o.r + 0.5f;
                if (d.sqrMagnitude < minD * minD)
                {
                    if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
                    np = o.pos + d.normalized * minD;
                }
            }
            Vector2 flat = new Vector2(np.x, np.z);
            if (flat.magnitude > maxRadius) flat = flat.normalized * maxRadius;
            hero.position = new Vector3(flat.x, y, flat.y);
            hero.rotation = Quaternion.Slerp(hero.rotation, Quaternion.LookRotation(dir), 14f * dt);
            // Blocked by an obstacle or the shore: give up on a click target after a moment.
            if ((hero.position - before).magnitude < speed * dt * 0.2f) stuckTime += dt;
            else stuckTime = 0f;
            return true;
        }

        // ------------------------------------------------------------------
        //  Click / tap to move (for small kids who can't use the arrow keys yet)
        // ------------------------------------------------------------------
        bool hasClickTarget;
        Vector3 clickTarget;
        float stuckTime;
        int clickIsland = -1;   // map: enter this island on arrival
        bool clickTalk;         // island: talk to the helper on arrival
        int clickSpot = -1;     // beach: pick this spot on arrival

        void ClearClickMove()
        {
            hasClickTarget = false;
            clickIsland = -1;
            clickTalk = false;
            clickSpot = -1;
        }

        void SetClickTarget(Vector3 p)
        {
            hasClickTarget = true;
            clickTarget = p;
            stuckTime = 0f;
        }

        // Called from OnGUI when a click is not on any button. screen = pixel position (origin bottom-left).
        void WorldClick(Vector2 screen)
        {
            if (settingsOpen || busy) return;
            Ray ray = cam.ScreenPointToRay(screen);
            float groundY = mode == Mode.Map ? SwimY : 0f;
            if (Mathf.Abs(ray.direction.y) < 0.001f) return;
            float t = (groundY - ray.origin.y) / ray.direction.y;
            if (t < 0f) return;
            Vector3 p = ray.origin + ray.direction * t;

            if (mode == Mode.Map)
            {
                foreach (MapIsle mi in mapIsles)
                {
                    Vector3 d = p - mi.pos;
                    d.y = 0f;
                    if (d.magnitude < mi.r + 1f && mi.index >= 0)
                    {
                        GoToIsland(mi.index);
                        return;
                    }
                }
                ClearClickMove();
                SetClickTarget(p);
            }
            else if (state == State.Explore)
            {
                ClearClickMove();
                Vector3 toNpc = p - npc.position;
                toNpc.y = 0f;
                if (toNpc.magnitude < 1.6f)
                {
                    clickTalk = true;
                    SetClickTarget(npc.position + (hero.position - npc.position).normalized * 1.6f);
                }
                else SetClickTarget(p);
            }
            else if (state == State.Quiz && CurQ != null && CurQ.type == QType.Find)
            {
                ClearClickMove();
                for (int i = 0; i < findSpots.Count; i++)
                {
                    Vector3 d = p - findSpots[i].pos;
                    d.y = 0f;
                    if (!findSpots[i].collected && d.magnitude < 1.3f)
                    {
                        GoToSpot(i);
                        return;
                    }
                }
                SetClickTarget(p);
            }
        }

        void GoToIsland(int i)
        {
            if (i > unlocked)
            {
                TryEnterIsland(i);   // shows "Locked!"
                return;
            }
            if (nearIsland == i)
            {
                TryEnterIsland(i);
                return;
            }
            MapIsle mi = mapIsles.Find(m => m.index == i);
            Vector3 from = hero.position - mi.pos;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = Vector3.back;
            ClearClickMove();
            clickIsland = i;
            SetClickTarget(mi.pos + from.normalized * (mi.r + 1.2f) + Vector3.up * SwimY);
        }

        void GoToSpot(int i)
        {
            if (nearSpot == i)
            {
                OnFindPick(i);
                return;
            }
            ClearClickMove();
            clickSpot = i;
            Vector3 from = hero.position - findSpots[i].pos;
            from.y = 0f;
            SetClickTarget(findSpots[i].pos + (from.sqrMagnitude > 0.01f ? from.normalized : Vector3.back) * 1.0f);
        }

        void GoTalk()
        {
            if (nearNpc)
            {
                BeginDialogue();
                return;
            }
            ClearClickMove();
            clickTalk = true;
            SetClickTarget(npc.position + (hero.position - npc.position).normalized * 1.6f);
        }

        // Finishes a click-move once the hero has arrived (or got as close as it can).
        void CheckClickArrival()
        {
            if (clickIsland >= 0 && nearIsland == clickIsland)
            {
                int i = clickIsland;
                ClearClickMove();
                TryEnterIsland(i);
            }
            else if (clickTalk && nearNpc)
            {
                ClearClickMove();
                BeginDialogue();
            }
            else if (clickSpot >= 0 && nearSpot == clickSpot)
            {
                int i = clickSpot;
                ClearClickMove();
                OnFindPick(i);
            }
            else if (!hasClickTarget)
            {
                ClearClickMove();
            }
        }

        void HandleSwim(float dt)
        {
            walking = MoveHero(dt, 5.5f, SeaRadius, SwimY);
            nearIsland = -2;
            float best = float.MaxValue;
            for (int i = 0; i < mapIsles.Count; i++)
            {
                MapIsle mi = mapIsles[i];
                Vector3 d = hero.position - mi.pos;
                d.y = 0f;
                float dist = d.magnitude - mi.r;
                if (dist < 2.8f && dist < best)
                {
                    best = dist;
                    nearIsland = mi.index;
                }
            }
            if (nearIsland >= 0 && ConfirmPressed()) TryEnterIsland(nearIsland);
        }

        void HandleWalk(float dt)
        {
            walking = MoveHero(dt, 4.2f, IslandWalkRadius, 0f);

            Vector3 toNpc = npc.position - hero.position;
            toNpc.y = 0f;
            nearNpc = state == State.Explore && toNpc.magnitude < 3.4f;

            nearSpot = -1;
            if (state == State.Quiz)
            {
                float best = 1.7f;
                for (int i = 0; i < findSpots.Count; i++)
                {
                    if (findSpots[i].collected) continue;
                    Vector3 d = findSpots[i].pos - hero.position;
                    d.y = 0f;
                    if (d.magnitude < best)
                    {
                        best = d.magnitude;
                        nearSpot = i;
                    }
                }
            }
        }

        void FaceTargets(float dt)
        {
            Vector3 toHeroFromNpc = hero.position - npc.position;
            toHeroFromNpc.y = 0f;
            if (toHeroFromNpc.sqrMagnitude > 0.01f && (state != State.Explore || toHeroFromNpc.magnitude < 7f))
                npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(toHeroFromNpc), 6f * dt);

            Vector3 toHeroFromBoss = hero.position - boss.position;
            toHeroFromBoss.y = 0f;
            if (toHeroFromBoss.sqrMagnitude > 0.01f)
                boss.rotation = Quaternion.Slerp(boss.rotation, Quaternion.LookRotation(toHeroFromBoss), 5f * dt);

            bool battling = state == State.Dialogue || (state == State.Quiz && CurQ != null && CurQ.type != QType.Find);
            if (battling)
            {
                Vector3 toBoss = boss.position - hero.position;
                toBoss.y = 0f;
                if (toBoss.sqrMagnitude > 0.01f)
                    hero.rotation = Quaternion.Slerp(hero.rotation, Quaternion.LookRotation(toBoss), 8f * dt);
            }
        }

        // ------------------------------------------------------------------
        //  Map <-> island
        // ------------------------------------------------------------------
        void TryEnterIsland(int i)
        {
            if (i > unlocked)
            {
                AddFloat("Locked!", hero.position + Vector3.up * 2f, new Color(1f, 0.8f, 0.8f), 44, 1.2f);
                PlaySfx(sfxBoing);
                return;
            }
            EnterIsland(i);
        }

        void EnterMap(int fromIsland)
        {
            StopAllCoroutines();
            ClearClickMove();
            ClearIsland();
            mode = Mode.Map;
            state = State.Explore;
            busy = false;
            curIndex = -1;
            cur = null;
            floats.Clear();
            mapRoot.gameObject.SetActive(true);
            ApplyMapLook();
            RefreshMap();

            obstacles.Clear();
            foreach (MapIsle mi in mapIsles) obstacles.Add(new Obstacle { pos = mi.pos, r = mi.r });

            if (fromIsland >= 0)
            {
                MapIsle mi = mapIsles.Find(m => m.index == fromIsland);
                Vector3 outDir = (mi.pos.sqrMagnitude > 0.01f ? mi.pos : Vector3.back).normalized;
                outDir = (outDir + Vector3.back * 0.6f).normalized;
                hero.position = mi.pos + outDir * (mi.r + 1.4f) + Vector3.up * SwimY;
                hero.rotation = Quaternion.LookRotation(outDir);
            }
            else
            {
                hero.position = new Vector3(0f, SwimY, -7f);
                hero.rotation = Quaternion.identity;
            }
            heroCapy.visual.localRotation = Quaternion.identity;
            cam.fieldOfView = 68f;
            SnapCamera();
        }

        void EnterIsland(int i)
        {
            cam.fieldOfView = 60f;
            StopAllCoroutines();
            mode = Mode.Island;
            curIndex = i;
            cur = islands[i];
            floats.Clear();
            mapRoot.gameObject.SetActive(false);
            if (cur.findItems != null) cur.findItems = PickBeachItems();   // different things to find each visit
            BuildIsland(cur);
            ResetIsland();
            Speak("Welcome to " + cur.nameEn + "!");
        }

        void ResetIsland()
        {
            StopAllCoroutines();
            ClearClickMove();
            state = State.Explore;
            busy = false;
            nearNpc = false;
            hint = "";
            dialogueIndex = 0;
            questions = null;
            foreach (FindSpot s in findSpots)
            {
                s.collected = false;
                if (s.root != null) s.root.gameObject.SetActive(true);
            }
            hero.position = playerStart;
            hero.rotation = Quaternion.identity;
            heroCapy.visual.localRotation = Quaternion.identity;
            boss.position = bossPos;
            boss.rotation = Quaternion.LookRotation(new Vector3(-1f, 0f, -0.2f));
            SetBad(bossCapy, true);
            bossCapy.visual.localScale = Vector3.one;
            bossCapy.visual.localPosition = Vector3.zero;
            bossCapy.visual.localRotation = Quaternion.identity;
            SnapCamera();
        }

        // ------------------------------------------------------------------
        //  Progress (PlayerPrefs = saved on this computer)
        // ------------------------------------------------------------------
        void LoadProgress()
        {
            cleared = new bool[islands.Count];
            unlocked = Mathf.Clamp(PlayerPrefs.GetInt("pi_unlocked", 0), 0, islands.Count - 1);
            for (int i = 0; i < cleared.Length; i++) cleared[i] = PlayerPrefs.GetInt("pi_cleared_" + i, 0) == 1;
            easyMode = PlayerPrefs.GetInt("pi_easy", 1) == 1;
            showZh = PlayerPrefs.GetInt("pi_zh", 1) == 1;
        }

        void SaveProgress()
        {
            PlayerPrefs.SetInt("pi_unlocked", unlocked);
            for (int i = 0; i < cleared.Length; i++) PlayerPrefs.SetInt("pi_cleared_" + i, cleared[i] ? 1 : 0);
            PlayerPrefs.SetInt("pi_easy", easyMode ? 1 : 0);
            PlayerPrefs.SetInt("pi_zh", showZh ? 1 : 0);
            PlayerPrefs.Save();
        }

        void MarkCleared(int i)
        {
            cleared[i] = true;
            unlocked = Mathf.Max(unlocked, Mathf.Min(i + 1, islands.Count - 1));
            SaveProgress();
        }

        void ResetProgress()
        {
            for (int i = 0; i < cleared.Length; i++) cleared[i] = false;
            unlocked = 0;
            SaveProgress();
            RefreshMap();
        }

        void UnlockAll()
        {
            unlocked = islands.Count - 1;
            SaveProgress();
            RefreshMap();
        }

        int ClearedCount()
        {
            int n = 0;
            foreach (bool c in cleared) if (c) n++;
            return n;
        }

        void AddFloat(string text, Vector3 pos, Color col, int size, float dur)
        {
            floats.Add(new FloatText { text = text, pos = pos, t = 0f, dur = dur, col = col, size = size });
        }
    }
}
