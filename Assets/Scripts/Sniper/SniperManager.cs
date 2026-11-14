using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SniperState { Menu, Playing, Paused, Victory, GameOver }

// 狙擊樂園：站在瞭望塔上，從狙擊鏡裡找天上飄下來的注音傘兵。
// 上面會出題 (念出一個注音，例如「ㄅ」)，打中拿著 ㄅ 的傘兵他才會掉下去；打錯了只會射空。
// 傘兵落地以後會一直對你開槍，血用完就輸了。打下一定數量的傘兵就過關。
// 操作：電腦移動滑鼠轉動狙擊鏡、按一下射擊 (射十字線中間)；iPad 點哪裡，狙擊鏡就轉過去射哪裡。
public class SniperManager : MonoBehaviour
{
    const int MaxHp = 100;
    const float PitchMin = -14f, PitchMax = 42f, YawRange = 46f;
    const float ShotRange = 120f;
    const int AllGroups = -1;
    const string HardKey = "sniper_hard", GroupKey = "sniper_group";

    public SniperState State { get; private set; } = SniperState.Menu;
    public bool Hard { get; private set; }
    public Vector3 EyePos => cam.transform.position;
    public float EnemyFireInterval => Hard ? 2.6f : 4.2f;
    int EnemyDamage => Hard ? 9 : 6;
    float FallSpeed => Hard ? 1.7f : 1.05f;
    int MaxActive => Hard ? 5 : 3;
    float SpawnInterval => Hard ? 2.2f : 3.4f;
    int Goal => Hard ? 15 : 10;

    readonly KidGui ui = new KidGui();
    Camera cam;
    Transform rig;
    float yaw, pitch, targetYaw, targetPitch, shake;
    bool pendingShot, touchMode;
    readonly List<Paratrooper> troopers = new List<Paratrooper>();
    readonly List<Rect> uiRects = new List<Rect>();
    List<Symbol> pool;
    int group;
    Symbol target;
    int score, hp, token;
    float hpShown, hurtFlash, spawnTimer, repeatTimer, messageTime, bannerPulse;
    string message;
    Texture2D scopeTex;

    AudioSource voice, sfx;
    AudioClip bang, ding, boom, hurt, miss;

    void Awake()
    {
        Hard = PlayerPrefs.GetInt(HardKey, 0) == 1;
        group = PlayerPrefs.GetInt(GroupKey, 0);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.65f, 0.83f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 220f;
        QualitySettings.shadowDistance = 90f;

        rig = new GameObject("SniperRig").transform;
        rig.position = new Vector3(0, 8f, 0); // 瞭望塔上
        var camGo = new GameObject("ScopeCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(rig, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.8f, 1f);
        cam.nearClipPlane = 0.2f;
        camGo.AddComponent<AudioListener>();

        voice = gameObject.AddComponent<AudioSource>();
        sfx = gameObject.AddComponent<AudioSource>();
        bang = SfxSynth.Bang();
        ding = SfxSynth.Ding();
        boom = SfxSynth.Boom();
        hurt = SfxSynth.Hit();
        miss = GameAudio.Load("Sfx/oops");

        scopeTex = MakeScopeTexture(512);
        BuildWorld();
        pitch = targetPitch = 18f;
        ApplyCamera(0f);
    }

    // ───────────────────────── 場景 ─────────────────────────

    void BuildWorld()
    {
        var root = new GameObject("World").transform;
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, -0.1f, 60f), new Vector3(400f, 0.2f, 400f), new Color(0.42f, 0.72f, 0.33f));
        var rng = new System.Random(11);
        // 遠方的山
        for (int i = 0; i < 9; i++)
        {
            float a = (-70f + i * 17.5f) * Mathf.Deg2Rad;
            float d = 150f + (float)rng.NextDouble() * 30f;
            float h = 30f + (float)rng.NextDouble() * 25f;
            Shapes.Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Sin(a) * d, -h * 0.3f, Mathf.Cos(a) * d), new Vector3(70f, h * 1.6f, 50f),
                Color.Lerp(new Color(0.35f, 0.6f, 0.4f), new Color(0.55f, 0.7f, 0.8f), (float)rng.NextDouble() * 0.5f));
        }
        // 樹、小房子 (60 公尺以內留空地，才看得到落地的傘兵)
        for (int i = 0; i < 70; i++)
        {
            float a = ((float)rng.NextDouble() * 140f - 70f) * Mathf.Deg2Rad;
            float d = 62f + (float)rng.NextDouble() * 60f;
            var p = new Vector3(Mathf.Sin(a) * d, 0, Mathf.Cos(a) * d);
            float s = 0.9f + (float)rng.NextDouble() * 0.9f;
            Shapes.Part(PrimitiveType.Cylinder, root, p + Vector3.up * 1.4f * s, new Vector3(0.5f, 1.4f, 0.5f) * s, new Color(0.5f, 0.33f, 0.2f));
            Shapes.Part(PrimitiveType.Sphere, root, p + Vector3.up * 3.6f * s, Vector3.one * 3.2f * s, new Color(0.2f, 0.5f + (float)rng.NextDouble() * 0.2f, 0.22f));
        }
        for (int i = 0; i < 6; i++)
        {
            float a = (-50f + i * 20f) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a) * 85f, 0, Mathf.Cos(a) * 85f);
            var c = Color.HSVToRGB((float)rng.NextDouble(), 0.35f, 1f);
            Shapes.Part(PrimitiveType.Cube, root, p + Vector3.up * 2f, new Vector3(5f, 4f, 5f), c);
            var roof = Shapes.Part(PrimitiveType.Cube, root, p + Vector3.up * 4.6f, new Vector3(4f, 4f, 5.2f), new Color(0.75f, 0.3f, 0.25f));
            roof.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        }
        // 雲
        for (int i = 0; i < 14; i++)
        {
            float a = ((float)rng.NextDouble() * 130f - 65f) * Mathf.Deg2Rad;
            float d = 80f + (float)rng.NextDouble() * 60f;
            var p = new Vector3(Mathf.Sin(a) * d, 55f + (float)rng.NextDouble() * 25f, Mathf.Cos(a) * d);
            for (int k = 0; k < 3; k++)
                Shapes.Part(PrimitiveType.Sphere, root, p + new Vector3(k * 6f - 6f, (k == 1 ? 2f : 0f), 0), new Vector3(10f, 6f, 8f), Color.white);
        }
        // 瞭望塔 (往下看的時候看得到欄杆)
        var wood = new Color(0.55f, 0.4f, 0.26f);
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 6.4f, 0), new Vector3(4f, 0.3f, 4f), wood);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Shapes.Part(PrimitiveType.Cube, root, new Vector3(1.8f * sx, 3.2f, 1.8f * sz), new Vector3(0.3f, 6.4f, 0.3f), wood * 0.85f);
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 7.4f, 1.9f), new Vector3(4f, 0.15f, 0.15f), wood);
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 6.9f, 1.9f), new Vector3(4f, 0.12f, 0.12f), wood);
    }

    // 狙擊鏡：中間透明的圓，外面黑色
    static Texture2D MakeScopeTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.955f, 0.975f, d));
                byte shade = (byte)(d > 0.94f && d < 0.975f ? 40 : 0); // 鏡筒的邊
                px[y * size + x] = new Color32(shade, shade, shade, (byte)(Mathf.Max(a, d > 0.94f ? 0.85f : 0f) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ───────────────────────── 遊戲流程 ─────────────────────────

    void StartGame(int groupIndex)
    {
        group = groupIndex;
        PlayerPrefs.SetInt(GroupKey, group);
        PlayerPrefs.Save();
        ClearTroopers();
        pool = PhonicData.SymbolsInGroup(group);
        token++;
        score = 0;
        hp = MaxHp;
        hpShown = MaxHp;
        target = null;
        message = null;
        spawnTimer = 0f;
        State = SniperState.Playing;
        // 一開始先放幾個，高度錯開，不會同時落地
        for (int i = 0; i < Mathf.Min(MaxActive, pool.Count); i++) Spawn(32f + i * 7f);
        NextTarget();
    }

    void ClearTroopers()
    {
        foreach (var t in troopers) if (t != null) Destroy(t.gameObject);
        troopers.Clear();
    }

    int ActiveCount()
    {
        int n = 0;
        foreach (var t in troopers) if (t != null && t.Alive) n++;
        return n;
    }

    void Spawn(float height)
    {
        // 符號不要跟天上已經有的重複
        var used = new HashSet<Symbol>();
        foreach (var t in troopers) if (t != null && t.Alive) used.Add(t.Sym);
        var choices = pool.FindAll(s => !used.Contains(s));
        if (choices.Count == 0) return;
        var sym = choices[Random.Range(0, choices.Count)];

        float a = Random.Range(-YawRange + 8f, YawRange - 8f) * Mathf.Deg2Rad;
        float d = Random.Range(32f, 52f);
        var go = new GameObject("Paratrooper_" + sym.ch);
        go.transform.position = new Vector3(Mathf.Sin(a) * d, height, Mathf.Cos(a) * d);
        var p = go.AddComponent<Paratrooper>();
        p.Init(this, sym, FallSpeed * Random.Range(0.85f, 1.15f));
        troopers.Add(p);
    }

    // 下一題：先找已經落地的 (最早落地的)，沒有就找最低的
    void NextTarget()
    {
        Paratrooper best = null;
        foreach (var t in troopers)
        {
            if (t == null || !t.Alive) continue;
            if (best == null) { best = t; continue; }
            if (t.Landed != best.Landed) { if (t.Landed) best = t; continue; }
            if (t.Landed ? t.LandedTime < best.LandedTime : t.transform.position.y < best.transform.position.y) best = t;
        }
        target = best != null ? best.Sym : null;
        if (target != null) SayTarget();
    }

    void SayTarget()
    {
        repeatTimer = 7f;
        bannerPulse = 1f;
        var clip = VoiceStore.Get(target.id);
        if (clip == null) clip = GameAudio.Load("Audio/" + target.id);
        if (clip == null) return;
        voice.Stop();
        voice.clip = clip;
        voice.Play();
    }

    public void OnLanded(Paratrooper p)
    {
        if (State != SniperState.Playing) return;
        ShowMessage("傘兵落地了！快找 " + p.Sym.ch + " 打下來！", 2.5f);
        // 現在的題目還在天上，落地的比較危險，換成落地的那個
        bool targetLanded = false;
        foreach (var t in troopers) if (t != null && t.Alive && t.Landed && t.Sym == target) targetLanded = true;
        if (!targetLanded) NextTarget();
    }

    public void EnemyShot(Paratrooper p)
    {
        if (State != SniperState.Playing) return;
        sfx.PlayOneShot(bang, 0.25f);
        hp = Mathf.Max(0, hp - EnemyDamage);
        hurtFlash = 0.4f;
        shake = 0.6f;
        sfx.PlayOneShot(hurt, 0.6f);
        if (hp > 0) return;
        State = SniperState.GameOver;
        token++;
        Cursor.visible = true;
    }

    public void Poof(Vector3 pos)
    {
        sfx.PlayOneShot(boom, 0.35f);
        StartCoroutine(PoofRoutine(pos));
    }

    IEnumerator PoofRoutine(Vector3 pos)
    {
        var puffs = new List<Transform>();
        for (int i = 0; i < 6; i++)
            puffs.Add(Shapes.Part(PrimitiveType.Sphere, null, pos + Random.insideUnitSphere * 0.6f + Vector3.up * 0.5f, Vector3.one * 0.8f, new Color(0.95f, 0.95f, 0.9f)).transform);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            foreach (var p in puffs) { p.localScale = Vector3.one * (0.8f + t * 3f); p.position += Vector3.up * Time.deltaTime; }
            yield return null;
        }
        foreach (var p in puffs) Destroy(p.gameObject);
    }

    void Shoot()
    {
        sfx.PlayOneShot(bang, 0.7f);
        shake = Mathf.Max(shake, 0.35f);
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // 十字線附近最前面的傘兵
        Paratrooper hit = null;
        float best = float.MaxValue;
        foreach (var t in troopers)
        {
            if (t == null || !t.Alive) continue;
            float d = Mathf.Min(RayDistance(ray, t.LabelPos, 1.35f), RayDistance(ray, t.BodyPos, 0.95f));
            if (d < best) { best = d; hit = t; }
        }
        Vector3 end = hit != null ? hit.LabelPos : ray.GetPoint(ShotRange);
        StartCoroutine(Tracer(end));
        if (hit == null) return;

        if (hit.Sym == target)
        {
            hit.Die();
            score++;
            sfx.PlayOneShot(ding, 0.6f);
            ShowMessage("打中了！" + target.ch, 1.5f);
            if (score >= Goal)
            {
                State = SniperState.Victory;
                token++;
                Cursor.visible = true;
                StartCoroutine(WinRoutine());
                return;
            }
            StartCoroutine(NextTargetSoon(token));
        }
        else
        {
            // 打錯了：子彈射空，傘兵沒事
            sfx.PlayOneShot(miss, 0.5f);
            ShowMessage("射空了！那是 " + hit.Sym.ch + "，要找 " + target.ch, 2f);
        }
    }

    // 射線到某一點的距離 (在 radius 以內才算打到；回傳沿著射線的遠近)
    static float RayDistance(Ray ray, Vector3 p, float radius)
    {
        var v = p - ray.origin;
        float along = Vector3.Dot(v, ray.direction);
        if (along <= 0f || along > ShotRange) return float.MaxValue;
        return Vector3.Cross(ray.direction, v).magnitude <= radius ? along : float.MaxValue;
    }

    IEnumerator NextTargetSoon(int t)
    {
        target = null;
        yield return new WaitForSeconds(1.2f);
        if (t != token || State != SniperState.Playing) yield break;
        if (ActiveCount() == 0) Spawn(28f);
        NextTarget();
    }

    IEnumerator WinRoutine()
    {
        yield return new WaitForSeconds(0.8f);
        foreach (var t in troopers) if (t != null && t.Alive) t.Die();
    }

    IEnumerator Tracer(Vector3 end)
    {
        var start = cam.transform.position + cam.transform.rotation * new Vector3(0.25f, -0.3f, 0.5f);
        var beam = Jets.Beam(null, start, end, 0.05f, new Color(1f, 0.95f, 0.6f));
        yield return new WaitForSeconds(0.06f);
        Destroy(beam);
    }

    void ShowMessage(string text, float seconds)
    {
        message = text;
        messageTime = seconds;
    }

    // ───────────────────────── 每一格 ─────────────────────────

    void Update()
    {
        float dt = Time.deltaTime;
        troopers.RemoveAll(t => t == null);
        if (messageTime > 0f) messageTime -= dt;
        hurtFlash = Mathf.Max(0f, hurtFlash - dt);
        bannerPulse = Mathf.Max(0f, bannerPulse - dt * 1.5f);
        hpShown = Mathf.MoveTowards(hpShown, hp, dt * 60f);

        if (State == SniperState.Playing)
        {
            UpdateAim(dt);
            spawnTimer -= dt;
            if (spawnTimer <= 0f && ActiveCount() < Mathf.Min(MaxActive, pool.Count))
            {
                spawnTimer = SpawnInterval;
                Spawn(Random.Range(36f, 46f));
            }
            repeatTimer -= dt;
            if (target != null && repeatTimer <= 0f) SayTarget();
            if (GameInput.Pause) SetPaused(true);
        }
        else if (State == SniperState.Paused && GameInput.Pause) SetPaused(false);

        // 滑鼠在按鈕上才顯示游標，其他時候看狙擊鏡的十字線
        Cursor.visible = State != SniperState.Playing || touchMode || PointerOverUi();
        ApplyCamera(dt);
    }

    void UpdateAim(float dt)
    {
        if (GameInput.Fire) touchMode = GameInput.IsTouch;
        var p = GameInput.PointerPosition;
        bool overUi = PointerOverUi();

        if (touchMode)
        {
            // iPad：點哪裡，狙擊鏡就轉過去，轉好了自動射擊
            if (GameInput.Fire && !overUi)
            {
                var d = cam.ScreenPointToRay(p).direction;
                targetYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -YawRange, YawRange);
                targetPitch = Mathf.Clamp(Mathf.Asin(d.y) * Mathf.Rad2Deg, PitchMin, PitchMax);
                pendingShot = true;
            }
            yaw = Mathf.MoveTowards(yaw, targetYaw, dt * 90f);
            pitch = Mathf.MoveTowards(pitch, targetPitch, dt * 90f);
            if (pendingShot && Mathf.Abs(yaw - targetYaw) < 0.3f && Mathf.Abs(pitch - targetPitch) < 0.3f)
            {
                pendingShot = false;
                Shoot();
            }
            return;
        }

        // 電腦：滑鼠在畫面上的位置決定狙擊鏡看哪裡，按一下射十字線中間
        if (!overUi)
        {
            float nx = Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width)) * 2f - 1f;
            float ny = Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height));
            targetYaw = nx * YawRange;
            targetPitch = Mathf.Lerp(PitchMin, PitchMax, ny);
        }
        yaw = Mathf.Lerp(yaw, targetYaw, dt * 8f);
        pitch = Mathf.Lerp(pitch, targetPitch, dt * 8f);
        if (GameInput.Fire && !overUi) Shoot();
    }

    void ApplyCamera(float dt)
    {
        // 狙擊鏡放大：直放 (iPad 轉正) 時左右也要看得夠寬
        float hfov = 30f;
        float vfov = 2f * Mathf.Atan(Mathf.Tan(hfov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(cam.aspect, 0.1f)) * Mathf.Rad2Deg;
        cam.fieldOfView = Mathf.Clamp(vfov, 20f, 60f);
        if (State == SniperState.Menu) { yaw = Mathf.Sin(Time.time * 0.15f) * 25f; pitch = 16f; }
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
        var jitter = Random.insideUnitSphere * shake * 0.6f;
        cam.transform.localRotation = Quaternion.Euler(-pitch + jitter.y, yaw + jitter.x, 0);
    }

    void SetPaused(bool paused)
    {
        State = paused ? SniperState.Paused : SniperState.Playing;
        Time.timeScale = paused ? 0f : 1f;
    }

    void BackToMenu()
    {
        Time.timeScale = 1f;
        token++;
        ClearTroopers();
        State = SniperState.Menu;
        voice.Stop();
    }

    bool PointerOverUi()
    {
        var p = ui.ToGui(GameInput.PointerPosition);
        foreach (var r in uiRects) if (r.Contains(p)) return true;
        return false;
    }

    Rect Ui(Rect r)
    {
        uiRects.Add(r);
        return r;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
    }

    // ───────────────────────── 介面 ─────────────────────────

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static string ColoredSymbols(int g)
    {
        string s = "";
        foreach (var sym in PhonicData.SymbolsInGroup(g)) s += "<color=#" + Hex(sym.color) + ">" + sym.ch + "</color>";
        return s;
    }

    void OnGUI()
    {
        ui.Begin();
        float W = ui.W, H = ui.H;
        if (Event.current.type == EventType.Layout) uiRects.Clear();

        if (State == SniperState.Menu) { DrawMenu(); return; }

        DrawScope(W, H);
        if (hurtFlash > 0f) ui.Box(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, 0.4f * hurtFlash / 0.4f));
        DrawHud(W, H);

        if (State == SniperState.Paused)
        {
            if (ui.Dialog("暫停", "休息一下～", out int c, "繼續", "重新開始", "回遊樂園"))
            {
                if (c == 0) SetPaused(false);
                else if (c == 1) { SetPaused(false); StartGame(group); }
                else { Time.timeScale = 1f; AppBoot.Load(AppBoot.ParkScene); }
            }
        }
        else if (State == SniperState.GameOver)
        {
            if (ui.Dialog("你被打倒了！", "打下了 " + score + " 個傘兵，再試一次吧！", out int c, "再玩一次", "選注音", "回遊樂園"))
            {
                if (c == 0) StartGame(group);
                else if (c == 1) BackToMenu();
                else AppBoot.Load(AppBoot.ParkScene);
            }
        }
        else if (State == SniperState.Victory)
        {
            if (ui.Dialog("傘兵全部打下來了！", "★★★　你是注音神射手！", out int c, "再玩一次", "選注音", "回遊樂園"))
            {
                if (c == 0) StartGame(group);
                else if (c == 1) BackToMenu();
                else AppBoot.Load(AppBoot.ParkScene);
            }
        }
    }

    // 狙擊鏡的直徑：橫放時兩邊至少留 320 給血條、題目 (iPad 4:3 橫放會比較小一點)
    static float ScopeSize(float W, float H)
    {
        float d = Mathf.Min(W, H) * 0.96f;
        return W > H ? Mathf.Min(d, W - 640f) : d;
    }

    void DrawScope(float W, float H)
    {
        float D = ScopeSize(W, H);
        var r = new Rect((W - D) / 2f, (H - D) / 2f, D, D);
        var black = new Color(0f, 0f, 0f, 1f);
        ui.Box(new Rect(0, 0, r.x + 1, H), black);
        ui.Box(new Rect(r.xMax - 1, 0, W - r.xMax + 1, H), black);
        ui.Box(new Rect(r.x, 0, D, r.y + 1), black);
        ui.Box(new Rect(r.x, r.yMax - 1, D, H - r.yMax + 1), black);
        GUI.DrawTexture(r, scopeTex);

        // 十字線 (中間留空，旁邊有刻度)
        float cx = W / 2f, cy = H / 2f, gap = 22f, len = D * 0.46f;
        var line = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        ui.Box(new Rect(cx - len, cy - 1.5f, len - gap, 3f), line);
        ui.Box(new Rect(cx + gap, cy - 1.5f, len - gap, 3f), line);
        ui.Box(new Rect(cx - 1.5f, cy - len, 3f, len - gap), line);
        ui.Box(new Rect(cx - 1.5f, cy + gap, 3f, len - gap), line);
        for (int i = 1; i <= 4; i++)
        {
            float o = gap + i * D * 0.07f;
            ui.Box(new Rect(cx + o - 2f, cy - 7f, 4f, 14f), line);
            ui.Box(new Rect(cx - o - 2f, cy - 7f, 4f, 14f), line);
            ui.Box(new Rect(cx - 7f, cy + o - 2f, 14f, 4f), line);
        }
        ui.Box(new Rect(cx - 3f, cy - 3f, 6f, 6f), new Color(1f, 0.2f, 0.2f, 0.9f));

        // 狙擊鏡外面的傘兵：在鏡子邊緣標出方向 (簡單模式標出符號)
        if (State != SniperState.Playing) return;
        float R = D / 2f * 0.9f;
        foreach (var t in troopers)
        {
            if (t == null || !t.Alive) continue;
            var sp = cam.WorldToScreenPoint(t.LabelPos);
            var g = ui.ToGui(sp);
            var dir = new Vector2(g.x - cx, g.y - cy);
            if (sp.z < 0f) dir = -dir;
            if (sp.z > 0f && dir.magnitude < R) continue;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            var m = new Vector2(cx, cy) + dir.normalized * R;
            var box = new Rect(m.x - 34f, m.y - 34f, 68f, 68f);
            var col = t.Landed ? new Color(0.9f, 0.15f, 0.15f, 0.9f) : new Color(t.Sym.color.r, t.Sym.color.g, t.Sym.color.b, 0.85f);
            ui.Box(box, col);
            ui.Label(box, Hard ? (t.Landed ? "!" : "?") : t.Sym.ch, ui.Mid, Color.white);
        }
    }

    void DrawHud(float W, float H)
    {
        // 左上：血條、打下幾個
        float hw = W > H ? Mathf.Min(440f, (W - ScopeSize(W, H)) / 2f - 40f) : 440f;
        ui.Box(new Rect(20, 20, hw, 130), new Color(0, 0, 0, 0.45f));
        ui.Bar(new Rect(35, 35, hw - 30, 46), hpShown / MaxHp, hp > 30 ? new Color(0.3f, 0.85f, 0.35f) : new Color(1f, 0.3f, 0.25f), "血量 " + hp);
        ui.Label(new Rect(35, 88, hw - 30, 50), "<size=" + (hw < 400 ? 28 : 34) + ">打下 " + score + " / " + Goal + " 個傘兵</size>", ui.Small, Color.white);

        // 要打哪一個：橫放放在狙擊鏡左邊的黑色地方 (不要擋住鏡子邊的標記)，直放放在上面
        if (State == SniperState.Playing || State == SniperState.Paused)
        {
            float s = 1f + bannerPulse * 0.25f;
            float side = (W - ScopeSize(W, H)) / 2f;
            var tr = W > H
                ? new Rect(20f, 170f, (side - 40f) * s, 150f * s)
                : new Rect(W / 2f - 260f * s, Mathf.Max(20f, (H - ScopeSize(W, H)) / 2f - 140f), 520f * s, 130f * s);
            ui.Box(tr, new Color(0.15f, 0.15f, 0.2f, 0.9f));
            float k = s * Mathf.Min(1f, tr.width / s / 420f); // 框比較窄 (iPad 橫放) 字就小一點，不要換行
            string t = target != null ? "打 <color=#" + Hex(target.color) + "><size=" + (int)(90 * k) + ">" + target.ch + "</size></color> 傘兵！" : "找下一個…";
            ui.Label(tr, "<size=" + (int)(52 * k) + ">" + t + "</size>", ui.Mid, Color.white);
        }

        // 右上：再聽一次、暫停
        var pauseRect = Ui(new Rect(W - 200, 20, 180, 80));
        var listenRect = Ui(new Rect(W - 200, 110, 180, 80));
        if (State == SniperState.Playing)
        {
            if (GUI.Button(pauseRect, "<size=34>暫停</size>", ui.Button)) SetPaused(true);
            if (target != null && GUI.Button(listenRect, "<size=30>再聽一次</size>", ui.Button)) SayTarget();
        }

        if (messageTime > 0f && State == SniperState.Playing)
        {
            var r = new Rect((W - 900) / 2f, H - 170, 900, 80);
            ui.Box(r, new Color(0, 0, 0, 0.6f));
            ui.Label(r, message, ui.Mid, Color.yellow);
        }

        string how = touchMode ? "點傘兵，狙擊鏡會轉過去射擊" : "移動滑鼠瞄準，按一下射擊";
        if (State == SniperState.Playing) ui.Label(new Rect(0, H - 70, W, 50), "<size=26>" + how + "</size>", ui.Small, new Color(1f, 1f, 1f, 0.8f));
    }

    void DrawMenu()
    {
        float W = ui.W, H = ui.H;
        ui.Box(new Rect(0, 0, W, H), new Color(0.05f, 0.1f, 0.2f, 0.55f));
        ui.Label(new Rect(0, 25, W, 130), "狙擊樂園", ui.Huge, new Color(1f, 0.9f, 0.35f));
        ui.Label(new Rect(20, 150, W - 40, 60), "聽注音，用狙擊鏡打下拿著那個注音的傘兵！別讓他們落地！", ui.Small, Color.white);

        float dw = 300, dh = 80, dy = 220;
        var easyRect = new Rect(W / 2f - dw - 10, dy, dw, dh);
        var hardRect = new Rect(W / 2f + 10, dy, dw, dh);
        ui.Box(new Rect((Hard ? hardRect : easyRect).x - 5, dy - 5, dw + 10, dh + 10), new Color(1f, 0.9f, 0.4f));
        if (GUI.Button(easyRect, "簡單", ui.Button)) SetHard(false);
        if (GUI.Button(hardRect, "困難", ui.Button)) SetHard(true);
        ui.Label(new Rect(0, dy + dh + 5, W, 40), Hard ? "<size=26>傘兵多、掉得快、落地後打得兇</size>" : "<size=26>傘兵少、掉得慢，鏡子邊會告訴你傘兵在哪</size>", ui.Small, Color.white);

        // 選要練的注音 (一組，或全部)
        int cols = KidGui.Portrait ? 2 : 4;
        float bw = KidGui.Portrait ? 460 : 400, bh = 95, gap = 16, y0 = 360;
        float x0 = (W - (cols * bw + (cols - 1) * gap)) / 2f;
        for (int i = 0; i <= PhonicData.GroupCount; i++)
        {
            int g = i < PhonicData.GroupCount ? i : AllGroups;
            var r = new Rect(x0 + (i % cols) * (bw + gap), y0 + (i / cols) * (bh + gap), bw, bh);
            if (g == group) ui.Box(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), new Color(1f, 1f, 1f, 0.35f));
            string text = g == AllGroups ? "<size=44>全部注音</size>" : "<size=50>" + ColoredSymbols(g) + "</size>";
            if (GUI.Button(r, text, ui.Button)) StartGame(g);
        }
        int rows = (PhonicData.GroupCount + cols) / cols;
        if (GUI.Button(new Rect((W - 360) / 2f, y0 + rows * (bh + gap) + 10, 360, 85), "← 回遊樂園", ui.Button)) AppBoot.Load(AppBoot.ParkScene);
    }

    void SetHard(bool value)
    {
        Hard = value;
        PlayerPrefs.SetInt(HardKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
