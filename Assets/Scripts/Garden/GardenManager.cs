using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GardenState { Menu, Plant, Grow, Answer, Result, Finished }

// 九九乘法花園：題目「種 3 排，每排 4 朵花」(3 × 4)。
// 從花圃左上角開始，手指按住拖到 (或滑鼠移到) 第 3 排、第 4 格放開，花就一排一排長出來，
// 一邊長一邊數「4、8、12」。再從答案裡選「一共有幾朵花」，答對了花圃轉一圈，4 × 3 也是 12。
public class GardenManager : MonoBehaviour
{
    const int Size = 9;            // 花圃 9 × 9 格
    const float Cell = 1f;
    const int Rounds = 10;
    const float Pitch = 58f;       // 鏡頭往下看的角度
    const string HardKey = "garden_hard", RangeKey = "garden_range";
    static readonly Color Soil = new Color(0.55f, 0.37f, 0.24f);
    static readonly Color SoilPick = new Color(0.55f, 0.85f, 0.4f);
    static readonly Color SoilRight = new Color(1f, 0.85f, 0.3f);
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
    int round, stars, wrongPlants, wrongAnswers;
    int pickRows, pickCols;        // 目前拖出來的大小 (0 = 沒有)
    bool pressOnBed, prevHeld, touchMode;
    List<int> choices;
    readonly HashSet<int> crossedOut = new HashSet<int>();
    string banner, banner2, message;
    float messageTime, spin, spinTarget, plantReady;
    int token;
    Coroutine talk;

    AudioSource voice, sfx;
    AudioClip ding, oops, pop, win;
    Rect backRect, listenRect;

    int Choices => hard ? 4 : 3;

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
        // 花圃旁邊的小路和外圍的樹、花叢
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 0.01f, -6.8f), new Vector3(14f, 0.02f, 1.6f), new Color(0.93f, 0.85f, 0.68f));
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

        // 排數 (左邊) 和每排第幾朵 (上面)
        labels = new GameObject("Labels").transform;
        labels.SetParent(bed, false);
        var face = Quaternion.Euler(Pitch, 0, 0);
        for (int i = 0; i < Size; i++)
        {
            ZhText.Make(labels, CellPos(i, 0) + new Vector3(-0.95f, 0.25f, 0), face, (i + 1).ToString(), new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.6f);
            ZhText.Make(labels, CellPos(0, i) + new Vector3(0, 0.25f, 0.95f), face, (i + 1).ToString(), new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.6f);
        }
        ZhText.Make(labels, CellPos(0, 0) + new Vector3(-0.95f, 0.25f, 0.95f), face, "★", new Color(1f, 0.6f, 0.1f), 0.7f, 0.7f);

        capy = ParkManager.BuildCapybara(root, new Vector3(-7.2f, 0, -3.5f), 2.4f);
        capy.localRotation = Quaternion.Euler(0, 150f, 0);
    }

    // 第 r 排 (0 = 最上面)、第 c 朵 (0 = 最左邊)
    static Vector3 CellPos(int r, int c) => new Vector3((c - (Size - 1) / 2f) * Cell, 0, ((Size - 1) / 2f - r) * Cell);

    // 直放、橫放都要看得到整個花圃 (上面留給題目、下面留給答案按鈕)
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
        wrongPlants = 0;
        wrongAnswers = 0;
        crossedOut.Clear();
        banner = banner2 = null;
        state = GardenState.Plant;
        Say("GardenVoice/p_" + q.a + "_" + q.b);
    }

    void ClearBed()
    {
        token++;
        foreach (var f in flowers) if (f != null) Destroy(f.gameObject);
        System.Array.Clear(flowers, 0, flowers.Length);
        foreach (var t in rowCounts) Destroy(t.gameObject);
        rowCounts.Clear();
        foreach (var b in butterflies) Destroy(b.gameObject);
        butterflies.Clear();
        growing.Clear();
        bouncing.Clear();
        spin = spinTarget = 0f;
        bed.localRotation = Quaternion.identity;
        labels.gameObject.SetActive(true);
        pickRows = pickCols = 0;
        // 按「開始」或「下一題」的那一下不要算成種花 (按鈕有時候比放開手指早一格就觸發)
        pressOnBed = false;
        plantReady = Time.unscaledTime + 0.4f;
    }

    void Plant(int rows, int cols)
    {
        if (rows == q.a && cols == q.b)
        {
            state = GardenState.Grow;
            pickRows = pickCols = 0;
            StartCoroutine(Grow(token));
            return;
        }
        wrongPlants++;
        sfx.PlayOneShot(oops, 0.6f);
        if (rows == q.b && cols == q.a && q.a != q.b)
            ShowMessage("排和朵弄反了！橫的一排要有 " + q.b + " 朵");
        else
            ShowMessage("要種 " + q.a + " 排，每排 " + q.b + " 朵喔！");
        Say("GardenVoice/p_" + q.a + "_" + q.b);
    }

    IEnumerator Grow(int t)
    {
        for (int r = 0; r < q.a; r++)
        {
            for (int c = 0; c < q.b; c++)
            {
                // 往鏡頭這邊挪一點：鏡頭斜斜往下看，花頭才會落在自己那一格上
                var f = GardenFlower.Make(bed, CellPos(r, c) + new Vector3(0, 0.17f, -0.32f), GardenFlower.RowColor(r), 1.25f);
                f.localScale = Vector3.zero;
                flowers[r, c] = f;
                growing.Add(new KeyValuePair<Transform, float>(f, Time.time));
                yield return new WaitForSeconds(hard ? 0.03f : 0.05f);
                if (t != token) yield break;
            }
            sfx.pitch = 0.9f + r * 0.08f;
            sfx.PlayOneShot(pop, 0.7f);
            if (!hard)
            {
                // 簡單：每排長完就數一次 (4、8、12)
                AddRowCount(r, q.b * (r + 1));
                Say("GardenVoice/n_" + q.b * (r + 1));
                yield return new WaitForSeconds(0.85f);
            }
            else yield return new WaitForSeconds(0.3f);
            if (t != token) yield break;
        }
        sfx.pitch = 1f;
        yield return new WaitForSeconds(0.3f);
        if (t != token) yield break;
        choices = MulData.Choices(q, Choices);
        state = GardenState.Answer;
        Say("GardenVoice/ask");
    }

    void AddRowCount(int r, int count)
    {
        var tm = ZhText.Make(bed, CellPos(r, q.b - 1) + new Vector3(0.95f, 0.4f, 0), Quaternion.Euler(Pitch, 0, 0), count.ToString(), new Color(0.15f, 0.35f, 0.9f), 0.9f, 0.7f);
        rowCounts.Add(tm);
    }

    void Answer(int value)
    {
        if (value == q.Answer)
        {
            if (wrongPlants == 0 && wrongAnswers == 0) stars++;
            state = GardenState.Result;
            StartCoroutine(Celebrate(token));
            return;
        }
        wrongAnswers++;
        crossedOut.Add(value);
        sfx.PlayOneShot(oops, 0.6f);
        ShowMessage("再數數看！");
        StartCoroutine(Recount(token));
    }

    // 答錯：一排一排跳一下，再數一次
    IEnumerator Recount(int t)
    {
        foreach (var tm in rowCounts) Destroy(tm.gameObject);
        rowCounts.Clear();
        Say("GardenVoice/again");
        yield return new WaitForSeconds(1.1f);
        for (int r = 0; r < q.a && t == token; r++)
        {
            bouncing.Clear();
            for (int c = 0; c < q.b; c++) bouncing.Add(flowers[r, c]);
            bounceStart = Time.time;
            AddRowCount(r, q.b * (r + 1));
            Say("GardenVoice/n_" + q.b * (r + 1));
            yield return new WaitForSeconds(0.85f);
        }
        bouncing.Clear();
    }

    IEnumerator Celebrate(int t)
    {
        sfx.PlayOneShot(ding, 0.6f);
        banner = q.Full;
        SayChain("GardenVoice/good" + Random.Range(1, 4), "GardenVoice/c_" + q.a + "_" + q.b);
        for (int i = 0; i < Mathf.Min(q.Answer, 6); i++) AddButterfly();
        yield return new WaitForSeconds(2.6f);
        if (t != token || q.a == q.b) yield break;
        // 轉一轉：橫的變直的，b × a 也是一樣多
        labels.gameObject.SetActive(false);
        foreach (var tm in rowCounts) tm.gameObject.SetActive(false);
        spinTarget = 90f;
        yield return new WaitForSeconds(1.2f);
        if (t == token) banner2 = q.b + " × " + q.a + " = " + q.Answer + "　也是一樣多！";
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
            yield return new WaitForSeconds(clip.length + 0.1f);
        }
        talk = null;
    }

    void ShowMessage(string text)
    {
        message = text;
        messageTime = 2.5f;
    }

    // ───────────────────────── 每一格畫面 ─────────────────────────

    void Update()
    {
        float dt = Time.deltaTime, t = Time.time;
        FitCamera();
        if (messageTime > 0f) messageTime -= dt;
        if (GameInput.Fire) touchMode = GameInput.IsTouch;

        if (state == GardenState.Plant) UpdatePlant();
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

    void UpdatePlant()
    {
        bool held = GameInput.PointerHeld;
        if (GameInput.Fire) pressOnBed = Time.unscaledTime >= plantReady && !PointerOverUi() && PointedCell(false, out _, out _);

        bool preview = held ? pressOnBed : !touchMode;
        if (preview && PointedCell(held, out int r, out int c)) { pickRows = r + 1; pickCols = c + 1; }
        else if (!held) pickRows = pickCols = 0;

        if (prevHeld && !held && pressOnBed)
        {
            pressOnBed = false;
            if (PointedCell(true, out r, out c)) Plant(r + 1, c + 1);
        }
    }

    // 手指 / 滑鼠指著哪一格；clamp = 拖到花圃外面時算最邊邊那格
    bool PointedCell(bool clamp, out int row, out int col)
    {
        row = col = -1;
        var ray = cam.ScreenPointToRay(GameInput.PointerPosition);
        var plane = new Plane(Vector3.up, new Vector3(0, 0.17f, 0));
        if (!plane.Raycast(ray, out float d)) return false;
        var p = bed.InverseTransformPoint(ray.GetPoint(d));
        float fc = p.x / Cell + (Size - 1) / 2f, fr = (Size - 1) / 2f - p.z / Cell;
        int c = Mathf.RoundToInt(fc), r = Mathf.RoundToInt(fr);
        bool inside = r >= 0 && r < Size && c >= 0 && c < Size;
        if (!inside && !clamp) return false;
        row = Mathf.Clamp(r, 0, Size - 1);
        col = Mathf.Clamp(c, 0, Size - 1);
        return true;
    }

    void PaintSoil(float t)
    {
        bool showHint = state == GardenState.Plant && wrongPlants >= 2 && Mathf.Repeat(t * 2f, 1f) < 0.5f;
        bool right = q != null && pickRows == q.a && pickCols == q.b && !hard;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                Color col = Soil;
                if (r < pickRows && c < pickCols) col = right ? SoilRight : SoilPick;
                else if (showHint && q != null && r < q.a && c < q.b) col = SoilHint;
                if (r == 0 && c == 0 && state == GardenState.Plant && pickRows == 0) col = Color.Lerp(Soil, SoilRight, 0.5f + Mathf.Sin(t * 5f) * 0.5f);
                if (soil[r, c].color != col) soil[r, c].color = col;
            }
    }

    bool PointerOverUi()
    {
        var p = ui.ToGui(GameInput.PointerPosition);
        return backRect.Contains(p) || listenRect.Contains(p);
    }

    void BackToMenu()
    {
        ClearBed();
        state = GardenState.Menu;
        banner = banner2 = null;
        voice.Stop();
    }

    // ───────────────────────── 介面 ─────────────────────────

    void OnGUI()
    {
        ui.Begin();
        float W = ui.W, H = ui.H;
        backRect = listenRect = Rect.zero;

        if (state == GardenState.Menu) { DrawMenu(); return; }

        // 上面：第幾題、星星、題目
        ui.Box(new Rect(0, 0, W, 210), new Color(0, 0, 0, 0.3f));
        ui.Label(new Rect(20, 12, 420, 50), "第 " + round + " / " + Rounds + " 題　★ " + stars, ui.Small, Color.white);
        backRect = new Rect(W - 250, 15, 230, 75);
        if (GUI.Button(backRect, "<size=32>← 選乘法表</size>", ui.Button)) { BackToMenu(); return; }

        if (state == GardenState.Plant || state == GardenState.Grow || state == GardenState.Answer)
        {
            ui.Label(new Rect(0, 55, W, 100), "種 <color=#ffe066>" + q.a + "</color> 排，每排 <color=#ffe066>" + q.b + "</color> 朵花", ui.Big, Color.white);
            listenRect = new Rect(W / 2f + 430, 75, 170, 70);
            if (W < 1300) listenRect = new Rect(W - 250, 100, 230, 70);
            if (GUI.Button(listenRect, "<size=30>再聽一次</size>", ui.Button)) Say("GardenVoice/p_" + q.a + "_" + q.b);
        }

        if (state == GardenState.Plant)
        {
            string how = touchMode ? "從 ★ 開始，手指按住花圃拖一拖，放開就種下去！" : "從 ★ 開始，滑鼠移到花圃上，點一下就種下去！";
            ui.Label(new Rect(20, 150, W - 40, 55), "<size=30>" + how + "</size>", ui.Small, new Color(1f, 1f, 0.75f));
            if (pickRows > 0)
            {
                var p = ui.ToGui(GameInput.PointerPosition);
                var r = new Rect(Mathf.Clamp(p.x + 40, 10, W - 330), Mathf.Clamp(p.y - 120, 220, H - 100), 320, 80);
                ui.Box(r, new Color(0, 0, 0, 0.55f));
                ui.Label(r, pickRows + " 排 × " + pickCols + " 朵", ui.Mid, Color.yellow);
            }
        }
        else if (state == GardenState.Answer)
        {
            ui.Label(new Rect(20, 150, W - 40, 55), "<size=34>一共有幾朵花？" + (q.a + " × " + q.b + " = ?") + "</size>", ui.Small, new Color(1f, 1f, 0.75f));
            DrawChoices(W, H);
        }
        else if (state == GardenState.Result)
        {
            ui.Label(new Rect(0, 60, W, 120), banner, ui.Huge, new Color(1f, 0.9f, 0.35f));
            if (!string.IsNullOrEmpty(banner2)) ui.Label(new Rect(0, 175, W, 60), banner2, ui.Mid, Color.white);
            var next = new Rect((W - 420) / 2f, H - 150, 420, 110);
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
            var r = new Rect((W - 900) / 2f, H * 0.42f, 900, 90);
            ui.Box(r, new Color(0.6f, 0.1f, 0.1f, 0.8f));
            ui.Label(r, message, ui.Mid, Color.white);
        }
    }

    void DrawChoices(float W, float H)
    {
        int n = choices.Count;
        float bw = KidGui.Portrait ? 230f : 260f, gap = 30f, bh = 130f;
        float x0 = (W - (n * bw + (n - 1) * gap)) / 2f, y = H - bh - 30f;
        for (int i = 0; i < n; i++)
        {
            int v = choices[i];
            var r = new Rect(x0 + i * (bw + gap), y, bw, bh);
            if (crossedOut.Contains(v))
            {
                ui.Box(r, new Color(0.3f, 0.3f, 0.3f, 0.6f));
                ui.Label(r, "<size=70><color=#888888>" + v + "</color></size>", ui.Big, Color.white);
                continue;
            }
            if (GUI.Button(r, "<size=80>" + v + "</size>", ui.Button)) Answer(v);
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
        ui.Label(new Rect(20, 165, W - 40, 70), "照題目種花，一排一排數一數，一共有幾朵花？", ui.Small, Color.white);

        float dw = 300, dh = 80, dy = 240;
        var easyRect = new Rect(W / 2f - dw - 10, dy, dw, dh);
        var hardRect = new Rect(W / 2f + 10, dy, dw, dh);
        ui.Box(new Rect((hard ? hardRect : easyRect).x - 5, dy - 5, dw + 10, dh + 10), new Color(1f, 0.9f, 0.4f));
        if (GUI.Button(easyRect, "簡單", ui.Button)) SetHard(false);
        if (GUI.Button(hardRect, "困難", ui.Button)) SetHard(true);
        ui.Label(new Rect(0, dy + dh + 5, W, 40), hard ? "<size=26>不幫你數，答案有 4 個</size>" : "<size=26>每排長好會幫你數，答案有 3 個</size>", ui.Small, Color.white);

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
