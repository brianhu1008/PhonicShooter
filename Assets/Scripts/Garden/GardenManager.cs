using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GardenState { Menu, Play, Grow, Result, Finished }

// 九九乘法花園：先出題「5 × 8 = ?」，下面的答案字卡一開始就看得到。
// 小朋友可以自己在花圃上拖出一塊地種花 (幾排、每排幾朵都自己決定)，花一排一排長出來，
// 種好、數一數，再選正確答案。答錯了先請他再數數看，錯好幾次才慢慢給提示。
// 答對了，如果種的剛好是 5 排 × 8 朵，花圃轉一圈，8 × 5 也是一樣多。
public class GardenManager : MonoBehaviour
{
    const int Size = 9;            // 花圃 9 × 9 格
    const float Cell = 1f;
    const int Rounds = 10;
    const float Pitch = 58f;       // 鏡頭往下看的角度
    const string HardKey = "garden_hard", RangeKey = "garden_range";
    static readonly Color Soil = new Color(0.55f, 0.37f, 0.24f);
    static readonly Color SoilPick = new Color(0.55f, 0.85f, 0.4f);
    static readonly Color SoilHover = new Color(0.68f, 0.52f, 0.38f);
    static readonly Color SoilHint = new Color(1f, 1f, 0.6f);

    readonly KidGui ui = new KidGui();
    Camera cam;
    Transform bed, labels, capy;
    readonly Material[,] soil = new Material[Size, Size];
    readonly Transform[,] flowers = new Transform[Size, Size];
    readonly List<TextMesh> rowCounts = new List<TextMesh>();
    readonly List<Transform> butterflies = new List<Transform>();
    readonly List<KeyValuePair<Transform, float>> growing = new List<KeyValuePair<Transform, float>>();
    readonly List<Transform> bouncing = new List<Transform>();
    float bounceStart;

    GardenState state = GardenState.Menu;
    bool hard;
    int range;
    MulQuestion q;
    int round, stars, wrongAnswers;
    // 種好的那塊地 (左上角那一格、幾排、每排幾朵)
    int plantR, plantC, plantRows, plantCols;
    // 正在拖的那塊地
    bool dragging;
    int dragR, dragC, pickR, pickC, pickRows, pickCols, hoverR = -1, hoverC = -1;
    bool prevHeld, touchMode;
    List<int> choices;
    readonly HashSet<int> crossedOut = new HashSet<int>();
    readonly List<Rect> uiRects = new List<Rect>();
    string banner, banner2, message;
    float messageTime, spin, spinTarget, plantReady;
    int token, growToken;
    Coroutine talk;

    AudioSource voice, sfx;
    AudioClip ding, oops, pop, win;

    int ChoiceCount => hard ? 4 : 3;
    int Planted => plantRows * plantCols;

    void Awake()
    {
        hard = PlayerPrefs.GetInt(HardKey, 0) == 1;
        range = PlayerPrefs.GetInt(RangeKey, 0);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.5f;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        RenderSettings.fog = false;

        var camGo = new GameObject("GardenCamera");
        camGo.tag = "MainCamera";
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.6f, 0.85f, 1f);
        camGo.AddComponent<AudioListener>();

        voice = gameObject.AddComponent<AudioSource>();
        sfx = gameObject.AddComponent<AudioSource>();
        ding = SfxSynth.Ding();
        oops = GameAudio.Load("Sfx/oops");
        pop = GameAudio.Load("Sfx/tile");
        win = GameAudio.Load("Sfx/win");

        BuildWorld();
        FitCamera();
    }

    // ───────────────────────── 場景 ─────────────────────────

    void BuildWorld()
    {
        var root = new GameObject("World").transform;
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, -0.1f, 0), new Vector3(80f, 0.2f, 80f), new Color(0.45f, 0.75f, 0.35f));
        var rng = new System.Random(5);
        for (int i = 0; i < 26; i++)
        {
            float a = i * Mathf.PI * 2f / 26f;
            var p = new Vector3(Mathf.Cos(a) * 12f, 0, Mathf.Sin(a) * 11f + 2f);
            if (p.z < -3f) continue; // 前面 (鏡頭這邊) 不要擋住
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            Shapes.Part(PrimitiveType.Cylinder, root, p + Vector3.up * 1.2f * s, new Vector3(0.4f, 1.2f, 0.4f) * s, new Color(0.55f, 0.35f, 0.2f));
            Shapes.Part(PrimitiveType.Sphere, root, p + Vector3.up * 3f * s, Vector3.one * 2.6f * s, new Color(0.22f, 0.55f + (float)rng.NextDouble() * 0.15f, 0.25f));
        }
        for (int i = 0; i < 30; i++)
        {
            float x = (float)rng.NextDouble() * 22f - 11f, z = (float)rng.NextDouble() * 16f - 6f;
            if (Mathf.Abs(x) < 6.5f && z < 6.5f) continue; // 花圃
            GardenFlower.Make(root, new Vector3(x, 0, z), Color.HSVToRGB((float)rng.NextDouble(), 0.5f, 1f), 1.2f);
        }

        // 會轉的花圃 (土、9 × 9 格)
        bed = new GameObject("Bed").transform;
        bed.SetParent(root, false);
        Shapes.Part(PrimitiveType.Cube, bed, new Vector3(0, 0.05f, 0), new Vector3(Size * Cell + 0.6f, 0.1f, Size * Cell + 0.6f), new Color(0.98f, 0.95f, 0.9f));
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                var tile = Shapes.Part(PrimitiveType.Cube, bed, CellPos(r, c) + Vector3.up * 0.1f, new Vector3(Cell * 0.9f, 0.14f, Cell * 0.9f), Soil);
                soil[r, c] = tile.GetComponent<Renderer>().sharedMaterial;
            }

        // 排數 (左邊) 和每排第幾朵 (上面)，數的時候可以對照
        labels = new GameObject("Labels").transform;
        labels.SetParent(bed, false);
        var face = Quaternion.Euler(Pitch, 0, 0);
        for (int i = 0; i < Size; i++)
        {
            ZhText.Make(labels, CellPos(i, 0) + new Vector3(-0.95f, 0.25f, 0), face, (i + 1).ToString(), new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.6f);
            ZhText.Make(labels, CellPos(0, i) + new Vector3(0, 0.25f, 0.95f), face, (i + 1).ToString(), new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.6f);
        }

        capy = ParkManager.BuildCapybara(root, new Vector3(-7.2f, 0, -3.5f), 2.4f);
        capy.localRotation = Quaternion.Euler(0, 150f, 0);
    }

    // 第 r 排 (0 = 最上面)、第 c 朵 (0 = 最左邊)
    static Vector3 CellPos(int r, int c) => new Vector3((c - (Size - 1) / 2f) * Cell, 0, ((Size - 1) / 2f - r) * Cell);

    // 直放、橫放都要看得到整個花圃 (上面留給題目、下面留給答案字卡)
    void FitCamera()
    {
        float halfW = 7.2f, halfH = 7.6f;
        float vfov = 50f;
        float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * Mathf.Deg2Rad / 2f) * Mathf.Max(cam.aspect, 0.1f));
        float dist = Mathf.Max(halfW / Mathf.Tan(hfov / 2f), halfH / Mathf.Tan(vfov * Mathf.Deg2Rad / 2f));
        cam.fieldOfView = vfov;
        var look = new Vector3(0, 0, -0.4f);
        cam.transform.rotation = Quaternion.Euler(Pitch, 0, 0);
        cam.transform.position = look - cam.transform.forward * dist;
    }

    // ───────────────────────── 遊戲流程 ─────────────────────────

    void StartGame(int rangeIndex)
    {
        range = rangeIndex;
        PlayerPrefs.SetInt(RangeKey, range);
        PlayerPrefs.Save();
        round = 0;
        stars = 0;
        q = null;
        NextQuestion();
    }

    void NextQuestion()
    {
        ClearBed();
        round++;
        q = MulData.Next(range, q);
        wrongAnswers = 0;
        crossedOut.Clear();
        choices = MulData.Choices(q, ChoiceCount);
        banner = banner2 = null;
        state = GardenState.Play;
        SayChain("GardenVoice/q_" + q.a + "_" + q.b, "GardenVoice/plant");
    }

    void ClearBed()
    {
        token++;
        ClearFlowers();
        foreach (var b in butterflies) Destroy(b.gameObject);
        butterflies.Clear();
        spin = spinTarget = 0f;
        bed.localRotation = Quaternion.identity;
        labels.gameObject.SetActive(true);
        // 按「開始」或「下一題」的那一下不要算成種花 (按鈕有時候比放開手指早一格就觸發)
        dragging = false;
        plantReady = Time.unscaledTime + 0.4f;
    }

    void ClearFlowers()
    {
        growToken++;
        foreach (var f in flowers) if (f != null) Destroy(f.gameObject);
        System.Array.Clear(flowers, 0, flowers.Length);
        foreach (var t in rowCounts) Destroy(t.gameObject);
        rowCounts.Clear();
        growing.Clear();
        bouncing.Clear();
        plantRows = plantCols = 0;
    }

    // 種下拖出來的那塊地 (再種一次會換掉原來的花)
    void Plant(int r0, int c0, int rows, int cols)
    {
        ClearFlowers();
        plantR = r0; plantC = c0; plantRows = rows; plantCols = cols;
        state = GardenState.Grow;
        StartCoroutine(Grow(growToken));
    }

    IEnumerator Grow(int t)
    {
        for (int i = 0; i < plantRows; i++)
        {
            for (int j = 0; j < plantCols; j++)
            {
                int r = plantR + i, c = plantC + j;
                // 往鏡頭這邊挪一點：鏡頭斜斜往下看，花頭才會落在自己那一格上
                var f = GardenFlower.Make(bed, CellPos(r, c) + new Vector3(0, 0.17f, -0.32f), GardenFlower.RowColor(i), 1.25f);
                f.localScale = Vector3.zero;
                flowers[r, c] = f;
                growing.Add(new KeyValuePair<Transform, float>(f, Time.time));
                yield return new WaitForSeconds(hard ? 0.03f : 0.05f);
                if (t != growToken) yield break;
            }
            sfx.pitch = 0.9f + i * 0.08f;
            sfx.PlayOneShot(pop, 0.7f);
            if (!hard)
            {
                // 簡單：每排長完就幫忙數一次 (8、16、24…)
                AddRowCount(i, plantCols * (i + 1));
                Say("GardenVoice/n_" + plantCols * (i + 1));
                yield return new WaitForSeconds(0.85f);
            }
            else yield return new WaitForSeconds(0.3f);
            if (t != growToken) yield break;
        }
        sfx.pitch = 1f;
        state = GardenState.Play;
    }

    void AddRowCount(int i, int count)
    {
        var pos = CellPos(plantR + i, plantC + plantCols - 1) + new Vector3(0.95f, 0.4f, 0);
        rowCounts.Add(ZhText.Make(bed, pos, Quaternion.Euler(Pitch, 0, 0), count.ToString(), new Color(0.15f, 0.35f, 0.9f), 0.9f, 0.7f));
    }

    void Answer(int value)
    {
        if (value == q.Answer)
        {
            if (wrongAnswers == 0) stars++;
            state = GardenState.Result;
            StartCoroutine(Celebrate(token));
            return;
        }
        wrongAnswers++;
        crossedOut.Add(value);
        sfx.PlayOneShot(oops, 0.6f);
        // 提示一次一點點：先請他數數看，錯第二次才說要種幾排，錯第三次才把那塊地亮給他看
        if (wrongAnswers >= 2)
        {
            ShowMessage("種 " + q.a + " 排，每排 " + q.b + " 朵花，數數看！", 4f);
            Say("GardenVoice/p_" + q.a + "_" + q.b);
        }
        else if (Planted > 0)
        {
            ShowMessage("再數數看！", 2.5f);
            StartCoroutine(Recount(growToken));
        }
        else
        {
            ShowMessage("先種花，數數看！", 2.5f);
            Say("GardenVoice/plant");
        }
    }

    // 一排一排跳一下，再數一次
    IEnumerator Recount(int t)
    {
        foreach (var tm in rowCounts) Destroy(tm.gameObject);
        rowCounts.Clear();
        Say("GardenVoice/again");
        yield return new WaitForSeconds(1.1f);
        for (int i = 0; i < plantRows && t == growToken; i++)
        {
            bouncing.Clear();
            for (int j = 0; j < plantCols; j++) bouncing.Add(flowers[plantR + i, plantC + j]);
            bounceStart = Time.time;
            AddRowCount(i, plantCols * (i + 1));
            Say("GardenVoice/n_" + plantCols * (i + 1));
            yield return new WaitForSeconds(0.85f);
        }
        bouncing.Clear();
    }

    IEnumerator Celebrate(int t)
    {
        growToken++; // 還在長的花停下來
        sfx.PlayOneShot(ding, 0.6f);
        banner = q.Full;
        SayChain("GardenVoice/good" + Random.Range(1, 4), "GardenVoice/c_" + q.a + "_" + q.b);
        for (int i = 0; i < 6; i++) AddButterfly();
        yield return new WaitForSeconds(2.6f);
        // 種的剛好是 a 排 × b 朵 (或 b 排 × a 朵)：轉一轉，橫的變直的，一樣多
        bool matches = Planted == q.Answer && plantRows != plantCols;
        if (t != token || !matches) yield break;
        labels.gameObject.SetActive(false);
        foreach (var tm in rowCounts) tm.gameObject.SetActive(false);
        spinTarget = 90f;
        yield return new WaitForSeconds(1.2f);
        if (t == token) banner2 = plantRows + " 排 × " + plantCols + " 朵，轉過來變成 " + plantCols + " 排 × " + plantRows + " 朵，也是 " + q.Answer + " 朵！";
    }

    void AddButterfly()
    {
        var b = new GameObject("Butterfly").transform;
        b.SetParent(bed.parent, false);
        var col = Color.HSVToRGB(Random.value, 0.6f, 1f);
        for (int s = -1; s <= 1; s += 2)
        {
            var wing = new GameObject("Wing").transform;
            wing.SetParent(b, false);
            Shapes.Part(PrimitiveType.Cube, wing, new Vector3(0.18f * s, 0, 0), new Vector3(0.34f, 0.02f, 0.3f), col);
        }
        Shapes.Part(PrimitiveType.Cube, b, Vector3.zero, new Vector3(0.06f, 0.06f, 0.3f), new Color(0.25f, 0.2f, 0.2f));
        b.localPosition = new Vector3(Random.Range(-4f, 4f), 1.5f, Random.Range(-4f, 4f));
        butterflies.Add(b);
    }

    void Say(string path)
    {
        if (talk != null) StopCoroutine(talk);
        talk = null;
        var clip = GameAudio.Load(path);
        if (clip == null) return;
        voice.Stop();
        voice.clip = clip;
        voice.Play();
    }

    void SayChain(params string[] paths)
    {
        if (talk != null) StopCoroutine(talk);
        talk = StartCoroutine(SayAll(paths));
    }

    IEnumerator SayAll(string[] paths)
    {
        foreach (var p in paths)
        {
            var clip = GameAudio.Load(p);
            if (clip == null) continue;
            voice.Stop();
            voice.clip = clip;
            voice.Play();
            yield return new WaitForSeconds(clip.length + 0.15f);
        }
        talk = null;
    }

    void ShowMessage(string text, float seconds)
    {
        message = text;
        messageTime = seconds;
    }

    // ───────────────────────── 每一格畫面 ─────────────────────────

    void Update()
    {
        float dt = Time.deltaTime, t = Time.time;
        FitCamera();
        if (messageTime > 0f) messageTime -= dt;
        if (GameInput.Fire) touchMode = GameInput.IsTouch;

        UpdateDrag();
        prevHeld = GameInput.PointerHeld;
        PaintSoil(t);

        // 花長出來 (彈一下)
        for (int i = growing.Count - 1; i >= 0; i--)
        {
            float k = (t - growing[i].Value) / 0.3f;
            var f = growing[i].Key;
            if (f == null || k >= 1f) { if (f != null) f.localScale = Vector3.one * 1.25f; growing.RemoveAt(i); continue; }
            f.localScale = Vector3.one * 1.25f * (Mathf.Sin(k * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f));
        }
        float bounce = Mathf.Max(0f, Mathf.Sin((t - bounceStart) * 9f)) * Mathf.Clamp01(1f - (t - bounceStart) / 0.7f);
        foreach (var f in bouncing) if (f != null) f.localScale = Vector3.one * 1.25f * (1f + bounce * 0.35f);

        // 花圃轉一轉
        spin = Mathf.MoveTowards(spin, spinTarget, dt * 110f);
        bed.localRotation = Quaternion.Euler(0, spin, 0);

        // 蝴蝶
        for (int i = 0; i < butterflies.Count; i++)
        {
            var b = butterflies[i];
            float a = t * 0.8f + i * 1.7f;
            var target = new Vector3(Mathf.Cos(a) * (2f + i % 3), 1.4f + Mathf.Sin(t * 2f + i) * 0.4f, Mathf.Sin(a * 1.3f) * 2.5f);
            var dir = target - b.localPosition;
            b.localPosition += dir * Mathf.Min(1f, dt * 1.5f);
            if (dir.sqrMagnitude > 0.001f) b.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z) + Vector3.forward * 0.001f);
            float flap = Mathf.Sin(t * 18f + i) * 50f;
            b.GetChild(0).localRotation = Quaternion.Euler(0, 0, flap);
            b.GetChild(1).localRotation = Quaternion.Euler(0, 0, -flap);
        }

        if (capy != null) capy.localRotation = Quaternion.Euler(0, 150f + Mathf.Sin(t * 0.7f) * 15f, 0);
        if (GameInput.Pause && state != GardenState.Menu) BackToMenu();
    }

    // 在花圃上按住拖一塊地 (從哪裡開始、拖到哪裡都可以)，放開就種
    void UpdateDrag()
    {
        bool held = GameInput.PointerHeld;
        hoverR = hoverC = -1;
        if (state != GardenState.Play) { dragging = false; pickRows = pickCols = 0; return; }

        if (GameInput.Fire && Time.unscaledTime >= plantReady && !PointerOverUi() && PointedCell(false, out int r0, out int c0))
        {
            dragging = true;
            dragR = r0; dragC = c0;
        }
        if (dragging && PointedCell(true, out int r, out int c))
        {
            pickR = Mathf.Min(dragR, r); pickC = Mathf.Min(dragC, c);
            pickRows = Mathf.Abs(r - dragR) + 1; pickCols = Mathf.Abs(c - dragC) + 1;
        }
        else pickRows = pickCols = 0;

        if (dragging && prevHeld && !held)
        {
            dragging = false;
            if (pickRows > 0) Plant(pickR, pickC, pickRows, pickCols);
            pickRows = pickCols = 0;
        }
        else if (!dragging && !held && !touchMode && PointedCell(false, out r, out c)) { hoverR = r; hoverC = c; }
    }

    // 手指 / 滑鼠指著哪一格；clamp = 拖到花圃外面時算最邊邊那格
    bool PointedCell(bool clamp, out int row, out int col)
    {
        row = col = -1;
        var ray = cam.ScreenPointToRay(GameInput.PointerPosition);
        var plane = new Plane(Vector3.up, new Vector3(0, 0.17f, 0));
        if (!plane.Raycast(ray, out float d)) return false;
        var p = bed.InverseTransformPoint(ray.GetPoint(d));
        int c = Mathf.RoundToInt(p.x / Cell + (Size - 1) / 2f), r = Mathf.RoundToInt((Size - 1) / 2f - p.z / Cell);
        bool inside = r >= 0 && r < Size && c >= 0 && c < Size;
        if (!inside && !clamp) return false;
        row = Mathf.Clamp(r, 0, Size - 1);
        col = Mathf.Clamp(c, 0, Size - 1);
        return true;
    }

    void PaintSoil(float t)
    {
        // 錯第三次以後，左上角 a 排 × b 朵那塊地會閃，告訴他可以怎麼種
        bool showHint = state == GardenState.Play && wrongAnswers >= 3 && Mathf.Repeat(t * 2f, 1f) < 0.5f;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                Color col = Soil;
                if (r >= pickR && r < pickR + pickRows && c >= pickC && c < pickC + pickCols) col = SoilPick;
                else if (r == hoverR && c == hoverC) col = SoilHover;
                else if (showHint && r < q.a && c < q.b) col = SoilHint;
                if (soil[r, c].color != col) soil[r, c].color = col;
            }
    }

    bool PointerOverUi()
    {
        var p = ui.ToGui(GameInput.PointerPosition);
        foreach (var r in uiRects) if (r.Contains(p)) return true;
        return false;
    }

    void BackToMenu()
    {
        ClearBed();
        state = GardenState.Menu;
        banner = banner2 = null;
        messageTime = 0f;
        voice.Stop();
    }

    // ───────────────────────── 介面 ─────────────────────────

    void OnGUI()
    {
        ui.Begin();
        float W = ui.W, H = ui.H;
        if (Event.current.type == EventType.Layout) uiRects.Clear();

        if (state == GardenState.Menu) { DrawMenu(); return; }

        // 上面：第幾題、星星、題目
        ui.Box(new Rect(0, 0, W, 210), new Color(0, 0, 0, 0.3f));
        ui.Label(new Rect(20, 12, 420, 50), "第 " + round + " / " + Rounds + " 題　★ " + stars, ui.Small, Color.white);
        var backRect = Ui(new Rect(W - 250, 15, 230, 75));
        if (GUI.Button(backRect, "<size=32>← 選乘法表</size>", ui.Button)) { BackToMenu(); return; }

        if (state == GardenState.Play || state == GardenState.Grow)
        {
            ui.Label(new Rect(0, 40, W, 130), q.a + " × " + q.b + " = ?", ui.Huge, new Color(1f, 0.9f, 0.35f));
            var listenRect = Ui(W < 1300 ? new Rect(W - 250, 100, 230, 70) : new Rect(W / 2f + 330, 75, 200, 70));
            if (GUI.Button(listenRect, "<size=30>再聽一次</size>", ui.Button)) Say("GardenVoice/q_" + q.a + "_" + q.b);

            string how = Planted > 0
                ? "種了 " + plantRows + " 排，每排 " + plantCols + " 朵。數一數，選答案！想重種就再拖一次"
                : touchMode ? "手指在花圃上拖一拖，種花來數數看！" : "滑鼠在花圃上按住拖一拖，種花來數數看！";
            ui.Label(new Rect(20, 155, W - 40, 50), "<size=30>" + how + "</size>", ui.Small, new Color(1f, 1f, 0.75f));
            if (pickRows > 0)
            {
                var p = ui.ToGui(GameInput.PointerPosition);
                var r = new Rect(Mathf.Clamp(p.x + 40, 10, W - 330), Mathf.Clamp(p.y - 120, 220, H - 100), 320, 80);
                ui.Box(r, new Color(0, 0, 0, 0.55f));
                ui.Label(r, pickRows + " 排 × " + pickCols + " 朵", ui.Mid, Color.yellow);
            }
            DrawChoices(W, H, state == GardenState.Play);
        }
        else if (state == GardenState.Result)
        {
            ui.Label(new Rect(0, 50, W, 120), banner, ui.Huge, new Color(1f, 0.9f, 0.35f));
            if (!string.IsNullOrEmpty(banner2)) ui.Label(new Rect(20, 160, W - 40, 50), "<size=32>" + banner2 + "</size>", ui.Small, Color.white);
            var next = Ui(new Rect((W - 420) / 2f, H - 150, 420, 110));
            string label = round >= Rounds ? "<size=48>看成績 ★</size>" : "<size=48>下一題 ▶</size>";
            if (GUI.Button(next, label, ui.Button))
            {
                if (round >= Rounds) Finish();
                else NextQuestion();
            }
        }
        else if (state == GardenState.Finished)
        {
            if (ui.Dialog("花園種滿了！", "★ " + stars + " / " + Rounds + "　你是九九乘法小園丁！", out int c, "再玩一次", "選乘法表", "回遊樂園"))
            {
                if (c == 0) StartGame(range);
                else if (c == 1) BackToMenu();
                else AppBoot.Load(AppBoot.ParkScene);
            }
        }

        if (messageTime > 0f && state != GardenState.Finished)
        {
            var r = new Rect((W - 1000) / 2f, H * 0.42f, 1000, 90);
            ui.Box(r, new Color(0.6f, 0.1f, 0.1f, 0.8f));
            ui.Label(r, message, ui.Mid, Color.white);
        }
    }

    // 記下按鈕的位置，按按鈕時不要在花圃上種花
    Rect Ui(Rect r)
    {
        uiRects.Add(r);
        return r;
    }

    // 答案字卡：一開始就看得到；花在長的時候先等一下
    void DrawChoices(float W, float H, bool enabled)
    {
        int n = choices.Count;
        float bw = KidGui.Portrait ? 230f : 260f, gap = 30f, bh = 130f;
        float x0 = (W - (n * bw + (n - 1) * gap)) / 2f, y = H - bh - 30f;
        bool oldEnabled = GUI.enabled;
        for (int i = 0; i < n; i++)
        {
            int v = choices[i];
            var r = Ui(new Rect(x0 + i * (bw + gap), y, bw, bh));
            if (crossedOut.Contains(v))
            {
                ui.Box(r, new Color(0.3f, 0.3f, 0.3f, 0.6f));
                ui.Label(r, "<size=70><color=#888888>" + v + "</color></size>", ui.Big, Color.white);
                continue;
            }
            GUI.enabled = enabled;
            if (GUI.Button(r, "<size=80>" + v + "</size>", ui.Button)) Answer(v);
            GUI.enabled = oldEnabled;
        }
    }

    void Finish()
    {
        state = GardenState.Finished;
        sfx.PlayOneShot(win, 0.7f);
        Say("GardenVoice/done");
    }

    void DrawMenu()
    {
        float W = ui.W, H = ui.H;
        ui.Box(new Rect(0, 0, W, H), new Color(0.05f, 0.2f, 0.05f, 0.45f));
        ui.Label(new Rect(0, 30, W, 140), "九九乘法花園", ui.Huge, new Color(1f, 0.9f, 0.35f));
        ui.Label(new Rect(20, 165, W - 40, 70), "看題目，自己在花圃上種花、數一數，再選答案！", ui.Small, Color.white);

        float dw = 300, dh = 80, dy = 240;
        var easyRect = new Rect(W / 2f - dw - 10, dy, dw, dh);
        var hardRect = new Rect(W / 2f + 10, dy, dw, dh);
        ui.Box(new Rect((hard ? hardRect : easyRect).x - 5, dy - 5, dw + 10, dh + 10), new Color(1f, 0.9f, 0.4f));
        if (GUI.Button(easyRect, "簡單", ui.Button)) SetHard(false);
        if (GUI.Button(hardRect, "困難", ui.Button)) SetHard(true);
        ui.Label(new Rect(0, dy + dh + 5, W, 40), hard ? "<size=26>花長好不幫你數，答案有 4 個</size>" : "<size=26>每排花長好會幫你數，答案有 3 個</size>", ui.Small, Color.white);

        float bw = 700, bh = 120, y = 400;
        for (int i = 0; i < MulData.RangeNames.Length; i++)
        {
            var r = new Rect((W - bw) / 2f, y + i * (bh + 20), bw, bh);
            if (i == range) ui.Box(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), new Color(1f, 1f, 1f, 0.3f));
            if (GUI.Button(r, "<size=54>" + MulData.RangeNames[i] + "</size>", ui.Button)) StartGame(i);
        }
        if (GUI.Button(new Rect((W - 360) / 2f, y + 3 * (bh + 20) + 20, 360, 90), "← 回遊樂園", ui.Button)) AppBoot.Load(AppBoot.ParkScene);
    }

    void SetHard(bool value)
    {
        hard = value;
        PlayerPrefs.SetInt(HardKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
