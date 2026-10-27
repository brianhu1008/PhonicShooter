#if UNITY_WEBGL && !UNITY_EDITOR
#define WEB_BUILD   // 網頁版 (iPad Safari)：不能用麥克風、不需要離開遊戲
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Menu, ModeSelect, Playing, Paused, LevelClear, AllClear, GameOver, Recorder }

public enum LevelMode
{
    Symbols,        // 認識這一組的注音符號
    HearAndRead,    // 看拼音 + 聽聲音
    CharToZhuyin,   // 看國字找注音
    ListenOnly,     // 只聽聲音
}

public class LevelDef
{
    public string title;
    public string desc;
    public LevelMode mode;
    public int goal;
    public float monsterSpeed;
}

// 遊戲主控：建立場景、關卡流程、介面 (IMGUI)、聲音。
// 先選一組注音 (ㄅㄆㄇㄈ、ㄉㄊㄋㄌ…)，再選玩法；
// 玩家會沿著森林小路自動往前走，每走一段就遇到一群注音怪獸。
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const float LaneHalfWidth = 6f; // 怪獸左右活動範圍
    const int WaveSize = 5;
    const float WalkBetweenWaves = 3.5f;
    const float ChunkLength = 8f;
    const int AllGroups = -1;

    public GameState State { get; private set; } = GameState.Menu;
    public Font ZhFont => ZhText.Font;

    // 怪獸牌子上的字用的材質：會被前面的怪獸擋住 (內建字型材質會穿透)
    public Material TextMaterial => ZhText.Material;
    public Vector3 PlayerPos => player.transform.position;
    public IReadOnlyList<Monster> Monsters => monsters;

    static readonly LevelDef[] Levels =
    {
        new LevelDef { title = "認識注音", desc = "找到上面那隻注音怪獸，把它打倒！", mode = LevelMode.Symbols, goal = 8, monsterSpeed = 0.9f },
        new LevelDef { title = "聽一聽、拼一拼", desc = "聽聲音、看拼音，找到對的怪獸！", mode = LevelMode.HearAndRead, goal = 10, monsterSpeed = 1.0f },
        new LevelDef { title = "看字找注音", desc = "這個字怎麼拼？打倒正確的注音怪獸！", mode = LevelMode.CharToZhuyin, goal = 10, monsterSpeed = 1.1f },
        new LevelDef { title = "耳朵大挑戰", desc = "只聽聲音，找出正確的注音！", mode = LevelMode.ListenOnly, goal = 10, monsterSpeed = 1.2f },
    };

    PlayerController player;
    readonly List<Monster> monsters = new List<Monster>();

    const int MaxHp = 100;

    // 難度：簡單 = 怪獸走得慢、蓄力久、打得輕；困難 = 原本的設定
    public bool Hard { get; private set; }
    public float MonsterSpeedScale => Hard ? 1f : 0.6f;
    public float DamageScale => Hard ? 1f : 0.5f;
    public float WindUpScale => Hard ? 1f : 1.6f;
    public float CooldownScale => Hard ? 1f : 1.6f;
    int MaxAttackers => Hard ? 3 : 1; // 同時可以出手的怪獸數

    readonly HashSet<Monster> attackers = new HashSet<Monster>();

    // 怪獸要出手前先排隊，避免全部一起打
    public bool TryStartAttack(Monster m)
    {
        attackers.RemoveWhere(a => a == null || !a.Alive);
        if (attackers.Contains(m)) return true;
        if (attackers.Count >= MaxAttackers) return false;
        attackers.Add(m);
        return true;
    }

    public void EndAttack(Monster m) => attackers.Remove(m);
    int HealOnCorrect => Hard ? 10 : 20;
    const string HardPrefKey = "hard";
    int hp;
    float hurtFlash;
    float hpShown;    // 血條平滑減少用

    int groupIndex;   // 0-9，或 AllGroups
    int levelIndex;
    int score;
    int mistakes;
    Answer target;
    bool roundActive;
    int roundToken;

    string message;
    float messageTime;
    string bannerTop, bannerBottom;
    float bannerTime;

    AudioSource voiceSource, sfxSource;
    AudioClip sfxBang, sfxHit;
    readonly Dictionary<string, AudioClip> voiceCache = new Dictionary<string, AudioClip>();
    Coroutine voiceRoutine;

    Transform ground, road;
    Transform decorRoot;
    readonly Queue<GameObject> chunks = new Queue<GameObject>();
    float nextChunkZ;

    LevelDef Level => Levels[levelIndex];
    string GroupName => groupIndex == AllGroups ? "全部注音" : PhonicData.GroupNames[groupIndex];

    // 由 AppBoot 在 Main 場景載入時建立
    void Awake()
    {
        Instance = this;
        Hard = PlayerPrefs.GetInt(HardPrefKey, 0) == 1;

        // 注音語音，和開槍的「蹦」分開播放，互不打斷
        voiceSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxBang = SfxSynth.Bang();
        sfxHit = SfxSynth.Hit();

        BuildWorld();

        var playerGo = new GameObject("Player");
        player = playerGo.AddComponent<PlayerController>();
        player.Init(this);
        player.ResetPose();
        ResetDecor();

        ShowMenu();
        StartAutoTestIfRequested();
    }

    // ───────────────────────── 場景 ─────────────────────────

    void BuildWorld()
    {
        var root = new GameObject("World").transform;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.85f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0);
        sun.transform.SetParent(root);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.6f, 0.82f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 60f;

        // 草地和小路跟著玩家移動 (顏色單一，看不出來在動)
        ground = Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, -0.5f, 0), new Vector3(80f, 1f, 160f), new Color(0.3f, 0.6f, 0.22f), true).transform;
        road = Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, -0.49f, 0), new Vector3(3.5f, 1f, 160f), new Color(0.75f, 0.6f, 0.4f)).transform;

        decorRoot = new GameObject("Decor").transform;
        decorRoot.SetParent(root);
    }

    void ResetDecor()
    {
        while (chunks.Count > 0) Destroy(chunks.Dequeue());
        nextChunkZ = -10f;
        UpdateWorld();
    }

    // 往前生出新的樹林，刪掉後面看不到的
    void UpdateWorld()
    {
        float pz = PlayerPos.z;
        ground.position = new Vector3(0, -0.5f, pz + 50f);
        road.position = new Vector3(0, -0.49f, pz + 50f);

        while (nextChunkZ < pz + 70f)
        {
            chunks.Enqueue(MakeChunk(nextChunkZ));
            nextChunkZ += ChunkLength;
        }
        while (chunks.Count > 0 && chunks.Peek().transform.position.z < pz - 15f)
            Destroy(chunks.Dequeue());
    }

    GameObject MakeChunk(float z)
    {
        var chunk = new GameObject("Chunk");
        chunk.transform.SetParent(decorRoot);
        chunk.transform.position = new Vector3(0, 0, z);
        var t = chunk.transform;
        var rng = new System.Random(Mathf.RoundToInt(z * 13f) + 1);

        for (int side = -1; side <= 1; side += 2)
        {
            int count = 2 + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                float x = side * (LaneHalfWidth + 2f + (float)rng.NextDouble() * 12f);
                var p = new Vector3(x, 0, z + (float)rng.NextDouble() * ChunkLength);
                int kind = rng.Next(5);
                if (kind <= 2) Tree(t, p, 0.8f + (float)rng.NextDouble() * 0.7f);
                else if (kind == 3) Mushroom(t, p, Color.HSVToRGB((float)rng.NextDouble(), 0.6f, 1f));
                else Shapes.Part(PrimitiveType.Sphere, t, p - t.position, new Vector3(1.4f, 0.8f, 1.1f), new Color(0.7f, 0.7f, 0.75f));
            }
            // 路邊小花
            for (int i = 0; i < 3; i++)
            {
                float x = side * (2.2f + (float)rng.NextDouble() * 3f);
                var p = new Vector3(x, 0.1f, z + (float)rng.NextDouble() * ChunkLength);
                Shapes.Part(PrimitiveType.Sphere, t, p - t.position, Vector3.one * 0.22f, Color.HSVToRGB((float)rng.NextDouble(), 0.5f, 1f));
            }
        }
        return chunk;
    }

    static void Tree(Transform parent, Vector3 worldPos, float s)
    {
        Vector3 p = worldPos - parent.position;
        Shapes.Part(PrimitiveType.Cylinder, parent, p + Vector3.up * 1.2f * s, new Vector3(0.4f, 1.2f, 0.4f) * s, new Color(0.55f, 0.35f, 0.2f));
        Shapes.Part(PrimitiveType.Sphere, parent, p + Vector3.up * 3f * s, Vector3.one * 2.4f * s, new Color(0.25f, 0.65f, 0.3f));
    }

    static void Mushroom(Transform parent, Vector3 worldPos, Color cap)
    {
        Vector3 p = worldPos - parent.position;
        Shapes.Part(PrimitiveType.Cylinder, parent, p + Vector3.up * 0.5f, new Vector3(0.4f, 0.5f, 0.4f), new Color(1f, 0.95f, 0.85f));
        Shapes.Part(PrimitiveType.Sphere, parent, p + Vector3.up * 1.05f, new Vector3(1.4f, 0.7f, 1.4f), cap);
    }

    // ───────────────────────── 流程 ─────────────────────────

    void Update()
    {
        if (messageTime > 0f) messageTime -= Time.deltaTime;
        if (bannerTime > 0f) bannerTime -= Time.deltaTime;
        if (hurtFlash > 0f) hurtFlash -= Time.unscaledDeltaTime;
        hpShown = Mathf.MoveTowards(hpShown, hp, Time.unscaledDeltaTime * 40f);

        if (State == GameState.Playing)
        {
            UpdateWorld();
            if (GameInput.Pause) SetPaused(true);
            else if (GameInput.Replay && roundActive) PlayTargetVoice();
        }
        else if (State == GameState.Paused && GameInput.Pause)
        {
            SetPaused(false);
        }
        else if (State == GameState.ModeSelect && GameInput.Pause)
        {
            ShowMenu();
        }
        else if (State == GameState.Recorder)
        {
            UpdateRecorder();
            if (GameInput.Pause) LeaveRecorder();
        }
    }

    void ShowMenu()
    {
        roundToken++;
        ClearMonsters();
        Time.timeScale = 1f;
        State = GameState.Menu;
        ShowCursor(true);
    }

    void SelectGroup(int group)
    {
        groupIndex = group;
        State = GameState.ModeSelect;
    }

    void StartLevel(int index)
    {
        levelIndex = index;
        score = 0;
        mistakes = 0;
        hp = MaxHp;
        hpShown = MaxHp;
        hurtFlash = 0f;
        message = null;
        bannerTime = 0f;
        target = null;
        roundToken++;
        ClearMonsters();
        player.ResetPose();
        ResetDecor();
        Time.timeScale = 1f;
        State = GameState.Playing;
        ShowCursor(false);
        StartCoroutine(WalkThenSpawn(roundToken, 2f));
    }

    void SetPaused(bool paused)
    {
        State = paused ? GameState.Paused : GameState.Playing;
        Time.timeScale = paused ? 0f : 1f;
        ShowCursor(paused);
    }

    // 遊戲中隱藏滑鼠游標，改用畫面上的準星；游標限制在視窗內
    void ShowCursor(bool show)
    {
        Cursor.visible = show;
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Confined;
    }

    void ClearMonsters()
    {
        foreach (var m in monsters)
            if (m != null) Destroy(m.gameObject);
        monsters.Clear();
        attackers.Clear();
    }

    IEnumerator WalkThenSpawn(int token, float walkTime)
    {
        player.Walking = true;
        yield return new WaitForSeconds(walkTime);
        if (token != roundToken || State == GameState.Menu) yield break;
        player.Walking = false;
        NextRound();
    }

    void NextRound()
    {
        ClearMonsters();
        var answers = BuildWave(out target);
        Shuffle(answers);
        var slots = new List<float> { -5f, -2.5f, 0f, 2.5f, 5f };
        Shuffle(slots);
        for (int i = 0; i < answers.Count; i++) SpawnMonster(answers[i], slots[i % slots.Count]);
        roundActive = true;
        StartCoroutine(DelayedTargetVoice(roundToken));
    }

    IEnumerator DelayedTargetVoice(int token)
    {
        yield return new WaitForSeconds(0.5f);
        if (token == roundToken && roundActive) PlayTargetVoice();
    }

    // 產生一波怪獸：一隻 (認識注音時可能兩隻) 正確答案，其他是容易混淆的選項
    List<Answer> BuildWave(out Answer correct)
    {
        var list = new List<Answer>();
        string lastId = target != null ? target.id : null;

        if (Level.mode == LevelMode.Symbols)
        {
            var groupSymbols = PhonicData.SymbolsInGroup(groupIndex);
            Symbol t;
            do t = groupSymbols[Random.Range(0, groupSymbols.Count)];
            while (t.id == lastId && groupSymbols.Count > 1);
            correct = PhonicData.SymbolAnswer(t);
            list.Add(correct);
            if (Random.value < 0.5f) list.Add(PhonicData.SymbolAnswer(t));

            // 先用同一組的其他符號，不夠再從同類 (聲母/韻母) 的其他組補
            var sameGroup = groupSymbols.FindAll(s => s != t);
            var others = PhonicData.Symbols.FindAll(s => s.group != t.group && s.IsInitial == t.IsInitial);
            Shuffle(sameGroup);
            Shuffle(others);
            var pool = new List<Symbol>(sameGroup);
            pool.AddRange(others);
            int k = 0;
            while (list.Count < WaveSize) list.Add(PhonicData.SymbolAnswer(pool[k++ % pool.Count]));
            return list;
        }

        var candidates = PhonicData.SyllablesForGroup(groupIndex);
        Syllable ts;
        do ts = candidates[Random.Range(0, candidates.Count)];
        while (ts.id == lastId && candidates.Count > 1);
        correct = PhonicData.SyllableAnswer(ts);
        list.Add(correct);

        // 同韻不同聲母 (ㄅㄚ / ㄆㄚ)、同聲母不同韻 (ㄇㄚ / ㄇㄠ)，再從同一組補
        var sameRhyme = new List<Syllable>();
        var sameInitial = new List<Syllable>();
        var rest = new List<Syllable>();
        foreach (var s in PhonicData.Syllables)
        {
            if (s.Core == ts.Core) continue; // 只差聲調的不要，太難分
            if (s.rhyme == ts.rhyme) sameRhyme.Add(s);
            else if (s.initial == ts.initial && s.initial != "") sameInitial.Add(s);
        }
        foreach (var s in candidates) if (s.Core != ts.Core && !sameRhyme.Contains(s) && !sameInitial.Contains(s)) rest.Add(s);
        Shuffle(sameRhyme);
        Shuffle(sameInitial);
        Shuffle(rest);

        var picks = new List<Syllable>();
        void Pick(List<Syllable> from, int upTo)
        {
            foreach (var s in from)
            {
                if (picks.Count >= upTo) return;
                if (!picks.Exists(p => p.Core == s.Core)) picks.Add(s);
            }
        }
        Pick(sameRhyme, 2);
        Pick(sameInitial, 3);
        Pick(rest, WaveSize - 1);
        Pick(sameRhyme, WaveSize - 1);
        Pick(sameInitial, WaveSize - 1);
        foreach (var p in picks) list.Add(PhonicData.SyllableAnswer(p));
        return list;
    }

    void SpawnMonster(Answer a, float x)
    {
        var pos = new Vector3(x + Random.Range(-0.3f, 0.3f), 0, PlayerPos.z + Random.Range(10f, 13f));
        var go = new GameObject("Monster " + a.display);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(Vector3.back);
        var monster = go.AddComponent<Monster>();
        monster.Init(this, a, Level.monsterSpeed * MonsterSpeedScale);
        monsters.Add(monster);
    }

    public void OnMonsterShot(Monster m)
    {
        if (!roundActive || State != GameState.Playing) return;

        if (m.Answer.id == target.id)
        {
            roundActive = false;
            score++;
            hp = Mathf.Min(MaxHp, hp + HealOnCorrect); // 答對補一點血
            m.Pop();
            monsters.Remove(m);
            foreach (var other in monsters) if (other != null) other.Poof();
            monsters.Clear();
            messageTime = 0f;
            ShowCorrectBanner();
            StartCoroutine(AfterCorrect(roundToken));
        }
        else
        {
            mistakes++;
            m.Wrong();
            PlayVoice(m.Answer.audioId);
            bool hideTarget = Level.mode == LevelMode.ListenOnly && HasVoice(target.audioId);
            message = hideTarget
                ? "這隻是「" + m.Answer.display + "」，再仔細聽一次！"
                : "這隻是「" + m.Answer.display + "」，要找的是「" + target.display + "」喔！";
            messageTime = 2.5f;
        }
    }

    void ShowCorrectBanner()
    {
        // 符號題：拿一個含有這個符號的組合音當例子 (不用和符號預設念法同一個字)
        var s = target.syllable;
        if (s == null)
        {
            var sym = target.symbol;
            var examples = PhonicData.Syllables.FindAll(e => e.Contains(sym) && e.ch != sym.defaultCh && PhonicData.PartsOf(e).Count >= 2);
            if (examples.Count == 0) examples = PhonicData.Syllables.FindAll(e => e.Contains(sym));
            s = examples[Random.Range(0, examples.Count)];
        }

        // 拼讀：每個符號分開念，合起來，最後念例詞，例如 ㄐ ＋ ㄧ ＋ ㄣ → ㄐㄧㄣ 念「雞、衣、恩、金、金色」
        var parts = PhonicData.PartsOf(s);
        var names = new List<string>();
        var ids = new List<string>();
        foreach (var p in parts) { names.Add(p.ch); ids.Add(p.id); }
        if (s.tone != "") names.Add(s.tone);
        ids.Add(s.id);
        ids.Add("w_" + s.id);
        bannerTop = string.Join(" ＋ ", names) + " → " + s.Display;
        if (names.Count >= 4) bannerTop = "<size=60>" + bannerTop + "</size>"; // 三個符號 + 聲調時字小一點才放得下
        bannerBottom = s.ch + "　（" + s.word + "）";
        PlayVoiceSequence(ids.ToArray());

        // 念完再收起來
        float speak = 0.3f;
        foreach (var id in ids) { var clip = GetVoice(id); if (clip != null) speak += clip.length + 0.15f; }
        bannerTime = Mathf.Max(2.6f, speak);
    }

    IEnumerator AfterCorrect(int token)
    {
        yield return new WaitForSeconds(2.2f);
        if (token != roundToken || State != GameState.Playing) yield break;

        if (score >= Level.goal)
        {
            State = levelIndex == Levels.Length - 1 ? GameState.AllClear : GameState.LevelClear;
            ShowCursor(true);
        }
        else
        {
            StartCoroutine(WalkThenSpawn(token, WalkBetweenWaves));
        }
    }

    // ───────────────────────── 語音 ─────────────────────────
    // 全部是教育部國語辭典的錄音，只留字音 (Assets/Resources/Audio/*.wav，由 AudioTrimmer 從 Assets/MoeAudio 裁切)；
    // 家長在「錄製注音」畫面錄的聲音優先使用

    AudioClip GetVoice(string id)
    {
        var recorded = VoiceStore.Get(id);
        if (recorded != null) return recorded;
        if (!voiceCache.TryGetValue(id, out var clip))
        {
            clip = GameAudio.Load("Audio/" + id);
            voiceCache[id] = clip;
        }
        return clip;
    }

    bool HasVoice(string id) => GetVoice(id) != null;

    public void PlayShootSfx() => sfxSource.PlayOneShot(sfxBang, 0.6f);

    // 被怪獸打到：扣血、畫面變紅晃動，血沒了就輸了
    public void DamagePlayer(int amount)
    {
        if (State != GameState.Playing || !roundActive) return;
        hp = Mathf.Max(0, hp - Mathf.Max(1, Mathf.RoundToInt(amount * DamageScale)));
        hurtFlash = 0.45f;
        sfxSource.PlayOneShot(sfxHit, 0.8f);
        player.Shake(0.35f);
        if (hp > 0) return;

        roundToken++;
        roundActive = false;
        player.Walking = false;
        State = GameState.GameOver;
        ShowCursor(true);
    }

    void PlayTargetVoice() => PlayVoice(target.audioId);

    void PlayVoice(string id) => PlayVoiceSequence(id);

    void PlayVoiceSequence(params string[] ids)
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        voiceRoutine = StartCoroutine(VoiceSequence(ids));
    }

    IEnumerator VoiceSequence(string[] ids)
    {
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var clip = GetVoice(id);
            if (clip == null) continue;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
            yield return new WaitForSecondsRealtime(clip.length + 0.15f);
        }
        voiceRoutine = null;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ───────────────────────── 錄音 (家長用) ─────────────────────────
    // 用學校教的念法錄每個注音符號，遊戲中念符號時會改播錄音

    const int RecordSeconds = 3;
    const int RecordFrequency = 44100;
    string recordingId;
    AudioClip micClip;
    float recordStart;
    string recorderMessage;
    int recorderGroup;

    void OpenRecorder()
    {
#if WEB_BUILD
        recorderMessage = "網頁版不能錄音，請在電腦版錄好再打包。";
#else
        recorderMessage = Microphone.devices.Length == 0 ? "找不到麥克風，請先接上麥克風再打開這個畫面。" : null;
#endif
        State = GameState.Recorder;
        ShowCursor(true);
    }

    void LeaveRecorder()
    {
        if (recordingId != null) StopRecording(false);
        ShowMenu();
    }

    void StartRecording(string id)
    {
#if WEB_BUILD
        recorderMessage = "網頁版不能錄音。";
    }
#else
        if (Microphone.devices.Length == 0)
        {
            recorderMessage = "找不到麥克風，請先接上麥克風。";
            return;
        }
        voiceSource.Stop();
        micClip = Microphone.Start(null, false, RecordSeconds, RecordFrequency);
        if (micClip == null)
        {
            recorderMessage = "無法使用麥克風，請到 Windows「設定 > 隱私權 > 麥克風」允許應用程式使用。";
            return;
        }
        recordingId = id;
        recordStart = Time.unscaledTime;
        recorderMessage = null;
    }
#endif

    void UpdateRecorder()
    {
#if WEB_BUILD
    }

    void StopRecording(bool save)
    {
        recordingId = null;
    }
#else
        // 時間到自動停止
        if (recordingId != null && (Time.unscaledTime - recordStart >= RecordSeconds || !Microphone.IsRecording(null)))
            StopRecording(true);
    }

    void StopRecording(bool save)
    {
        string id = recordingId;
        recordingId = null;
        int frames = Microphone.GetPosition(null);
        bool stillRecording = Microphone.IsRecording(null);
        Microphone.End(null);
        if (!save || micClip == null) return;
        if (frames <= 0 || !stillRecording) frames = micClip.samples; // 錄滿 3 秒時位置會歸零

        var raw = new float[micClip.samples * micClip.channels];
        micClip.GetData(raw, 0);
        var mono = new float[frames];
        for (int i = 0; i < frames; i++) mono[i] = raw[i * micClip.channels];

        if (VoiceStore.Save(id, mono, micClip.frequency))
        {
            recorderMessage = "錄好了！聽聽看，不滿意可以再錄一次。";
            PlayVoice(id);
        }
        else
        {
            recorderMessage = "沒有錄到聲音，請靠近麥克風再錄一次。";
        }
    }
#endif

    // ───────────────────────── 自動測試 ─────────────────────────
    // PhonicShooter.exe -autotest <資料夾>：自動截圖幾個畫面後關閉，用來確認建置正常

    void StartAutoTestIfRequested()
    {
        string dir = AppBoot.Arg("-autotest");
        if (dir != null) StartCoroutine(AutoTest(dir));
    }

    IEnumerator AutoTest(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        yield return new WaitForSeconds(1.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1_menu.png"));
        yield return new WaitForSeconds(0.5f);
        SelectGroup(3);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1a_modes.png"));
        yield return new WaitForSeconds(0.5f);
        recorderGroup = 7;
        State = GameState.Recorder;
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1b_recorder.png"));
        yield return new WaitForSeconds(0.5f);
        groupIndex = 3;
        StartLevel(1);
        yield return new WaitForSeconds(4.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "2_wave.png"));
        yield return new WaitForSeconds(0.5f);
        var wrong = monsters.Find(m => m.Answer.id != target.id);
        if (wrong != null) OnMonsterShot(wrong);
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "3_wrong.png"));
        yield return new WaitForSeconds(0.5f);
        var right = monsters.Find(m => m.Answer.id == target.id);
        if (right != null) OnMonsterShot(right);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "4_correct.png"));
        yield return new WaitForSeconds(0.5f);
        groupIndex = 8;
        StartLevel(0);
        yield return new WaitForSeconds(4.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "5_symbols.png"));
        yield return new WaitForSeconds(0.5f);
        // 把怪獸搬到玩家面前，看牠們攻擊
        for (int i = 0; i < monsters.Count; i++)
            monsters[i].transform.position = PlayerPos + new Vector3(-4f + i * 2f, 0, 3.4f + (i % 2) * 0.6f);
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "6_windup.png"));
        yield return new WaitForSeconds(2.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "7_attacked.png"));
        yield return new WaitForSeconds(0.3f);
        DamagePlayer(999);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "8_gameover.png"));
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    // ───────────────────────── 介面 ─────────────────────────

    GUIStyle sSmall, sMid, sBig, sHuge, sButton, sHint;
    Texture2D white;
    // 介面以 1920x1080 設計；iPad (4:3) 這種比較窄的螢幕改以寬度為準，畫面變高
    float W = 1920f, H = 1080f, guiScale = 1f;
    Rect replayRect;

    // 點在「再聽一次」按鈕上時不要開槍
    public bool PointerOverButton(Vector2 screenPos)
    {
        var gui = new Vector2(screenPos.x / guiScale, (Screen.height - screenPos.y) / guiScale);
#if WEB_BUILD
        if (pauseRect.Contains(gui)) return true;
#endif
        if (!roundActive || target == null || !HasVoice(target.audioId)) return false;
        return replayRect.Contains(gui);
    }

    Rect pauseRect;

    void EnsureStyles()
    {
        if (sSmall != null) return;
        white = Texture2D.whiteTexture;
        sSmall = MakeStyle(34, TextAnchor.MiddleCenter);
        sMid = MakeStyle(46, TextAnchor.MiddleCenter);
        sBig = MakeStyle(80, TextAnchor.MiddleCenter);
        sHuge = MakeStyle(110, TextAnchor.MiddleCenter);
        sHint = MakeStyle(26, TextAnchor.MiddleCenter);
        sButton = new GUIStyle(GUI.skin.button) { font = ZhFont, fontSize = 40, wordWrap = true, richText = true };
        sButton.normal.textColor = Color.white;
        sButton.hover.textColor = Color.yellow;
    }

    GUIStyle MakeStyle(int size, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.label) { font = ZhFont, fontSize = size, alignment = anchor, wordWrap = true, richText = true };
        s.normal.textColor = Color.white;
        return s;
    }

    void Box(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    void Label(Rect r, string text, GUIStyle style, Color color)
    {
        // 加一點陰影比較好讀
        var shadow = new Rect(r.x + 3, r.y + 3, r.width, r.height);
        var old = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, 0.6f);
        GUI.Label(shadow, text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // 把一組注音用各自的顏色顯示
    static string ColoredSymbols(int group)
    {
        string s = "";
        foreach (var sym in PhonicData.SymbolsInGroup(group)) s += "<color=#" + Hex(sym.color) + ">" + sym.ch + "</color>";
        return s;
    }

    void OnGUI()
    {
        EnsureStyles();
        guiScale = Mathf.Min(Screen.height / 1080f, Screen.width / 1920f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));
        W = Screen.width / guiScale;
        H = Screen.height / guiScale;

        switch (State)
        {
            case GameState.Menu: DrawMenu(); break;
            case GameState.ModeSelect: DrawModeSelect(); break;
            case GameState.Playing: DrawHud(); break;
            case GameState.Paused: DrawHud(); DrawPause(); break;
            case GameState.LevelClear: DrawHud(); DrawLevelClear(); break;
            case GameState.AllClear: DrawHud(); DrawAllClear(); break;
            case GameState.GameOver: DrawHud(); DrawGameOver(); break;
            case GameState.Recorder: DrawRecorder(); break;
        }
    }

    void DrawMenu()
    {
        Box(new Rect(0, 0, W, H), new Color(0.1f, 0.15f, 0.3f, 0.65f));
        Label(new Rect(0, 40, W, 140), "注音怪獸大作戰", sHuge, new Color(1f, 0.9f, 0.4f));
        Label(new Rect(0, 180, W, 50), "移動滑鼠瞄準，按一下就發射泡泡！打倒正確的注音怪獸～", sSmall, Color.white);

        // 一個大大的「全部注音」按鈕
        string all = "";
        for (int g = 0; g < PhonicData.GroupCount; g++) all += ColoredSymbols(g);
        var big = new Rect((W - 1000) / 2f, 290, 1000, 400);
        if (GUI.Button(big, "<size=110>全部注音</size>\n<size=40>" + all + "</size>", sButton)) SelectGroup(AllGroups);

        float by = big.yMax + 60;
#if WEB_BUILD
        // 網頁版不能錄音 (電腦上錄好的會一起打包)
        if (GUI.Button(new Rect((W - 370) / 2f, by, 370, 80), "← 回遊樂園", sButton)) AppBoot.Load(AppBoot.ParkScene);
#else
        if (GUI.Button(new Rect(W / 2f - 380, by, 370, 80), "錄製注音念法", sButton)) OpenRecorder();
        if (GUI.Button(new Rect(W / 2f + 10, by, 370, 80), "← 回遊樂園", sButton)) AppBoot.Load(AppBoot.ParkScene);
#endif
#if WEB_BUILD
        Label(new Rect(0, H - 60, W, 50), "點怪獸就能發射！爸爸媽媽：遊戲中右上角「暫停」可以回主選單", sHint, new Color(1, 1, 1, 0.8f));
#else
        Label(new Rect(0, H - 60, W, 50), "爸爸媽媽：Esc 暫停　R 再聽一次", sHint, new Color(1, 1, 1, 0.8f));
#endif
    }

    void SetHard(bool hard)
    {
        Hard = hard;
        PlayerPrefs.SetInt(HardPrefKey, hard ? 1 : 0);
        PlayerPrefs.Save();
    }

    void DrawModeSelect()
    {
        Box(new Rect(0, 0, W, H), new Color(0.1f, 0.15f, 0.3f, 0.65f));
        string title = groupIndex == AllGroups ? "全部注音" : ColoredSymbols(groupIndex);
        Label(new Rect(0, 50, W, 150), title, sHuge, new Color(1f, 0.9f, 0.4f));
        // 難度切換
        float dw = 300, dh = 80, dy = 200;
        var easyRect = new Rect(W / 2f - dw - 10, dy, dw, dh);
        var hardRect = new Rect(W / 2f + 10, dy, dw, dh);
        Box(new Rect((Hard ? hardRect : easyRect).x - 5, dy - 5, dw + 10, dh + 10), new Color(1f, 0.9f, 0.4f));
        if (GUI.Button(easyRect, "簡單", sButton)) SetHard(false);
        if (GUI.Button(hardRect, "困難", sButton)) SetHard(true);
        Label(new Rect(0, dy + dh + 5, W, 40), Hard ? "<size=26>怪獸走得快、打得痛！</size>" : "<size=26>怪獸走得慢、打得輕，適合剛開始玩</size>", sSmall, Color.white);

        float bw = 760, bh = 115, y = 340;
        for (int i = 0; i < Levels.Length; i++)
        {
            var r = new Rect((W - bw) / 2f, y + i * (bh + 18), bw, bh);
            if (GUI.Button(r, "第 " + (i + 1) + " 關　" + Levels[i].title + "\n<size=28>" + Levels[i].desc + "</size>", sButton)) StartLevel(i);
        }
        if (GUI.Button(new Rect((W - 360) / 2f, y + Levels.Length * (bh + 18) + 20, 360, 90), "← 回主選單", sButton)) ShowMenu();
    }

    void DrawHud()
    {
        DrawHurtFlash();

        // 左上：關卡與進度
        Box(new Rect(20, 20, 470, 120), new Color(0, 0, 0, 0.35f));
        Label(new Rect(30, 25, 450, 55), (Hard ? "困難　" : "簡單　") + Level.title, sSmall, Color.white);
        string stars = "";
        for (int i = 0; i < Level.goal; i++) stars += i < score ? "<color=#ffe14d>★</color>" : "<color=#ffffff55>★</color>";
        Label(new Rect(30, 80, 450, 55), stars, sSmall, Color.white);

        DrawHpBar(new Rect(20, 150, 470, 56));
        DrawPrompt();
#if WEB_BUILD
        // iPad 沒有 Esc 鍵：右上角的暫停按鈕
        pauseRect = new Rect(W - 130, 20, 110, 110);
        if (State == GameState.Playing && GUI.Button(pauseRect, "<size=34>暫停</size>", sButton)) SetPaused(true);
#endif

        if (bannerTime > 0f)
        {
            float a = Mathf.Clamp01(bannerTime / 0.4f);
            Box(new Rect(W / 2 - 560, 330, 1120, 260), new Color(0.15f, 0.55f, 0.25f, 0.8f * a));
            Label(new Rect(W / 2 - 560, 340, 1120, 130), bannerTop, sBig, new Color(1f, 1f, 1f, a));
            Label(new Rect(W / 2 - 560, 470, 1120, 110), bannerBottom, sBig, new Color(1f, 0.95f, 0.5f, a));
        }
        else if (State == GameState.Playing && !roundActive && score > 0)
        {
            Label(new Rect(0, 260, W, 80), "往前走～", sMid, Color.white);
        }

        if (messageTime > 0f && !string.IsNullOrEmpty(message))
        {
            Box(new Rect(W / 2 - 560, 760, 1120, 90), new Color(0.6f, 0.15f, 0.15f, 0.8f));
            Label(new Rect(W / 2 - 560, 760, 1120, 90), message, sMid, Color.white);
        }

        DrawCrosshair();
    }

    void DrawHpBar(Rect r)
    {
        Box(r, new Color(0, 0, 0, 0.45f));
        Label(new Rect(r.x + 6, r.y, 50, r.height), "<color=#ff4d6a>♥</color>", sMid, Color.white);
        var bar = new Rect(r.x + 60, r.y + 12, r.width - 74, r.height - 24);
        Box(bar, new Color(0.25f, 0.05f, 0.05f, 0.9f));
        float frac = Mathf.Clamp01(hp / (float)MaxHp);
        float shownFrac = Mathf.Clamp01(hpShown / MaxHp);
        // 剛被打掉的部分先顯示白色，再慢慢消失
        Box(new Rect(bar.x, bar.y, bar.width * shownFrac, bar.height), new Color(1f, 1f, 1f, 0.8f));
        var color = frac > 0.5f ? new Color(0.3f, 0.9f, 0.35f) : frac > 0.25f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.25f, 0.2f);
        Box(new Rect(bar.x, bar.y, bar.width * frac, bar.height), color);
        Label(bar, "<size=26>" + hp + " / " + MaxHp + "</size>", sSmall, Color.white);
    }

    void DrawHurtFlash()
    {
        if (hurtFlash <= 0f) return;
        float a = Mathf.Clamp01(hurtFlash / 0.45f);
        Box(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, 0.35f * a));
        // 四周再深一點
        float edge = 120f;
        var c = new Color(0.7f, 0f, 0f, 0.5f * a);
        Box(new Rect(0, 0, W, edge), c);
        Box(new Rect(0, H - edge, W, edge), c);
        Box(new Rect(0, 0, edge, H), c);
        Box(new Rect(W - edge, 0, edge, H), c);
    }

    void DrawPrompt()
    {
        if (target == null || !roundActive) return;
        var panel = new Rect(W / 2 - 430, 20, 860, 200);
        Box(panel, new Color(1f, 1f, 1f, 0.85f));
        var top = new Rect(panel.x, panel.y + 8, panel.width, 50);
        var mid = new Rect(panel.x, panel.y + 55, panel.width, 130);

        var mode = Level.mode;
        if (mode == LevelMode.ListenOnly && !HasVoice(target.audioId)) mode = LevelMode.CharToZhuyin; // 沒有語音檔就改成看字

        switch (mode)
        {
            case LevelMode.Symbols:
                GUI.Label(top, "<color=#26264d>打倒這隻注音怪獸：</color>", sSmall);
                Label(mid, target.display, sHuge, target.color);
                break;
            case LevelMode.HearAndRead:
                GUI.Label(top, "<color=#26264d>聽一聽，找到這個拼音：</color>", sSmall);
                Label(mid, target.display, sHuge, target.color);
                break;
            case LevelMode.CharToZhuyin:
                GUI.Label(top, "<color=#26264d>這個字的注音是哪一個？</color>", sSmall);
                Label(mid, target.syllable.ch + "<size=50>（" + target.syllable.word + "）</size>", sHuge, new Color(0.2f, 0.4f, 0.9f));
                break;
            case LevelMode.ListenOnly:
                GUI.Label(top, "<color=#26264d>仔細聽！是哪一個注音？</color>", sSmall);
                Label(mid, "♪ ♪ ♪", sHuge, new Color(0.9f, 0.4f, 0.6f));
                break;
        }

        // 大大的「再聽一次」按鈕，小朋友自己點
        if (HasVoice(target.audioId))
        {
            replayRect = new Rect(panel.xMax + 20, panel.y + 30, 230, 140);
            if (GUI.Button(replayRect, "♪\n再聽一次", sButton) && State == GameState.Playing) PlayTargetVoice();
        }
    }

    void DrawCrosshair()
    {
        if (State != GameState.Playing) return;
        Vector2 p = player.AimScreenPos;
        float cx = p.x / guiScale, cy = (Screen.height - p.y) / guiScale;
        if (PointerOverButton(p))
        {
            // 在按鈕上顯示一般的小方塊
            Box(new Rect(cx - 6, cy - 6, 12, 12), Color.white);
            return;
        }

        Color c = player.AimingAtMonster ? new Color(1f, 0.9f, 0.1f) : Color.white;
        float gap = player.AimingAtMonster ? 16f : 12f, len = 24f, th = 6f;
        var outline = new Color(0, 0, 0, 0.7f);
        Rect[] bars =
        {
            new Rect(cx - th / 2, cy - gap - len, th, len),
            new Rect(cx - th / 2, cy + gap, th, len),
            new Rect(cx - gap - len, cy - th / 2, len, th),
            new Rect(cx + gap, cy - th / 2, len, th),
            new Rect(cx - th / 2, cy - th / 2, th, th),
        };
        foreach (var b in bars) Box(new Rect(b.x - 2f, b.y - 2f, b.width + 4f, b.height + 4f), outline);
        foreach (var b in bars) Box(b, c);
    }

    bool Dialog(string title, string sub, out int choice, params string[] buttons)
    {
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
        var panel = new Rect(W / 2 - 450, 180, 900, 260 + buttons.Length * 120);
        Box(panel, new Color(0.15f, 0.2f, 0.4f, 0.95f));
        Label(new Rect(panel.x, panel.y + 30, panel.width, 110), title, sBig, new Color(1f, 0.9f, 0.4f));
        Label(new Rect(panel.x, panel.y + 140, panel.width, 80), sub, sMid, Color.white);
        choice = -1;
        for (int i = 0; i < buttons.Length; i++)
            if (GUI.Button(new Rect(panel.x + 150, panel.y + 240 + i * 120, 600, 100), buttons[i], sButton)) choice = i;
        return choice >= 0;
    }

    void DrawPause()
    {
        if (Dialog("暫停", "休息一下～", out int c, "繼續玩", "回主選單", "回遊樂園"))
        {
            if (c == 0) SetPaused(false);
            else if (c == 1) ShowMenu();
            else AppBoot.Load(AppBoot.ParkScene);
        }
    }

    void DrawLevelClear()
    {
        string stars = mistakes == 0 ? "★★★" : mistakes <= 3 ? "★★☆" : "★☆☆";
        if (Dialog("過關了！好棒！", stars + "　打錯 " + mistakes + " 次", out int c, "下一關", "再玩一次", "回主選單"))
        {
            if (c == 0) StartLevel(levelIndex + 1);
            else if (c == 1) StartLevel(levelIndex);
            else ShowMenu();
        }
    }

    void DrawGameOver()
    {
        if (Dialog("被怪獸打倒了！", "答對了 " + score + " 題，再試一次吧！", out int c, "再玩一次", "回主選單"))
        {
            if (c == 0) StartLevel(levelIndex); else ShowMenu();
        }
    }

    void DrawAllClear()
    {
        bool hasNext = groupIndex != AllGroups && groupIndex < PhonicData.GroupCount - 1;
        string sub = GroupName + " 都學會了！你是注音小英雄！";
        if (hasNext)
        {
            if (Dialog("這一組全部過關！", sub, out int c, "下一組 " + PhonicData.GroupNames[groupIndex + 1], "再玩一次", "回主選單"))
            {
                if (c == 0) { groupIndex++; StartLevel(0); }
                else if (c == 1) StartLevel(levelIndex);
                else ShowMenu();
            }
        }
        else if (Dialog("全部過關！", sub, out int c, "再玩一次", "回主選單"))
        {
            if (c == 0) StartLevel(levelIndex); else ShowMenu();
        }
    }

    void DrawRecorder()
    {
        Box(new Rect(0, 0, W, H), new Color(0.1f, 0.15f, 0.3f, 0.85f));
        Label(new Rect(0, 20, W, 90), "錄製注音念法", sBig, new Color(1f, 0.9f, 0.4f));
        Label(new Rect(W / 2 - 750, 105, 1500, 80),
            "選一組，按「錄音」用學校教的念法念一次（最長 3 秒），念完按「停止」。\n還沒錄的會用教育部辭典的字音；標示「需要錄音」的符號辭典沒有適合的字。", sHint, Color.white);

        // 分組頁籤
        float tw = 178, tgap = 6;
        float tx0 = (W - (PhonicData.GroupCount * tw + (PhonicData.GroupCount - 1) * tgap)) / 2f;
        for (int g = 0; g < PhonicData.GroupCount; g++)
        {
            var r = new Rect(tx0 + g * (tw + tgap), 195, tw, 70);
            if (g == recorderGroup) Box(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(1f, 0.9f, 0.4f));
            GUI.enabled = recordingId == null;
            if (GUI.Button(r, "<size=28>" + ColoredSymbols(g) + "</size>", sButton)) recorderGroup = g;
            GUI.enabled = true;
        }

        var symbols = PhonicData.SymbolsInGroup(recorderGroup);
        float rowW = 1150, rowH = 108, x = (W - rowW) / 2f, y = 285;
        for (int i = 0; i < symbols.Count; i++)
        {
            var sym = symbols[i];
            var row = new Rect(x, y + i * (rowH + 10), rowW, rowH);
            Box(row, new Color(1f, 1f, 1f, 0.12f));
            Label(new Rect(row.x + 10, row.y, 140, rowH), sym.ch, sBig, sym.color);

            bool recordingThis = recordingId == sym.id;
            string status = recordingThis
                ? "<color=#ff6666>● 錄音中… " + Mathf.CeilToInt(RecordSeconds - (Time.unscaledTime - recordStart)) + "</color>"
                : VoiceStore.Has(sym.id) ? "<color=#7dff8a>★ 已錄音</color>"
                : sym.defaultCh != null ? "<color=#cccccc>辭典音（" + sym.defaultCh + "）</color>"
                : "<color=#ffb347>需要錄音</color>";
            GUI.Label(new Rect(row.x + 150, row.y, 330, rowH), status, sSmall);

            float bx = row.x + 500, bw = 190, bh = 84, gap = 16, by = row.y + (rowH - bh) / 2f;
            GUI.enabled = recordingId == null || recordingThis;
            if (GUI.Button(new Rect(bx, by, bw, bh), recordingThis ? "■ 停止" : "● 錄音", sButton))
            {
                if (recordingThis) StopRecording(true); else StartRecording(sym.id);
            }
            GUI.enabled = recordingId == null && HasVoice(sym.id);
            if (GUI.Button(new Rect(bx + bw + gap, by, bw, bh), "▶ 播放", sButton)) PlayVoice(sym.id);
            GUI.enabled = recordingId == null && VoiceStore.Has(sym.id);
            if (GUI.Button(new Rect(bx + 2 * (bw + gap), by, bw, bh), "刪除", sButton))
            {
                VoiceStore.Delete(sym.id);
                recorderMessage = sym.ch + " 的錄音已刪除。";
            }
            GUI.enabled = true;
        }

        if (!string.IsNullOrEmpty(recorderMessage))
            Label(new Rect(0, 880, W, 60), recorderMessage, sSmall, new Color(1f, 0.95f, 0.6f));
        if (GUI.Button(new Rect((W - 360) / 2f, 955, 360, 90), "完成，回主選單", sButton)) LeaveRecorder();
    }
}
