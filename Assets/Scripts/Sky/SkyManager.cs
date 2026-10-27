using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SkyState { Menu, Playing, BossIntro, Paused, Victory, GameOver }

// 九九乘法戰機 (像「雷電」的直向捲軸射擊)：
// 戰機一直往前飛，敵機載著答案。打中正確答案的那台，整批敵機一起爆炸；
// 打完一定題數後大魔王出現，一直出題，打中正確的砲台它才會受傷。
public class SkyManager : MonoBehaviour
{
    public static SkyManager I { get; private set; }
    public const float CombatY = 12f;
    const float FlySpeed = 7f;
    const int MaxHp = 100;
    const string HardKey = "sky_hard", RangeKey = "sky_range";

    public SkyState State { get; private set; } = SkyState.Menu;
    public bool Hard { get; private set; }
    public SkyPlayer Player { get; private set; }

    // 難度：簡單 = 敵機少、開火慢、子彈慢、打得輕
    public float FireIntervalScale => Hard ? 1f : 1.7f;
    public float SpeedScale => Hard ? 1.3f : 1f;
    float EnemyBulletSpeed => Hard ? 8f : 5.5f;
    int Damage => Hard ? 10 : 6;
    int WaveSize => Hard ? 5 : 4;
    int Goal => Hard ? 10 : 6;
    int BossHp => Hard ? 6 : 4;
    const int HealOnCorrect = 10;

    Transform rig;
    Camera cam;
    Vector3 camBase;
    SkyTerrain terrain;
    readonly List<SkyEnemy> enemies = new List<SkyEnemy>();
    readonly List<SkyBullet> bullets = new List<SkyBullet>();
    readonly Stack<SkyBullet> friendlyPool = new Stack<SkyBullet>();
    readonly Stack<SkyBullet> enemyPool = new Stack<SkyBullet>();
    Material friendlyMat, enemyMat;
    SkyBoss boss;

    int range;
    MulQuestion q;
    bool asking;          // 目前的題目可以作答
    int score, hp, token;
    float hpShown, hurtFlash, shake, warnTime, victoryDelay;
    string bannerTop, message;
    float bannerTime, messageTime, dingCooldown, pewCooldown, punishCooldown;

    AudioSource voice, sfx;
    AudioClip pew, boom, ding, hitClip;
    readonly KidGui ui = new KidGui();
    Rect pauseRect;

    void Awake()
    {
        I = this;
        Hard = PlayerPrefs.GetInt(HardKey, 0) == 1;
        range = PlayerPrefs.GetInt(RangeKey, 0);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.0f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        sun.transform.rotation = Quaternion.Euler(55f, -25f, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.62f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.62f, 0.78f, 0.92f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 95f;
        QualitySettings.shadowDistance = 70f;

        rig = new GameObject("Rig").transform;
        var camGo = new GameObject("SkyCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(rig, false);
        camBase = new Vector3(0, 34f, -6f);
        camGo.transform.localPosition = camBase;
        camGo.transform.LookAt(rig.TransformPoint(new Vector3(0, CombatY, 3f)));
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        cam.farClipPlane = 200f;
        camGo.AddComponent<AudioListener>();

        terrain = new GameObject("Terrain").AddComponent<SkyTerrain>();
        terrain.Init(rig);

        var playerGo = new GameObject("Player");
        playerGo.transform.SetParent(rig, false);
        Player = playerGo.AddComponent<SkyPlayer>();
        Player.Init(this);
        Player.ResetPose();

        voice = gameObject.AddComponent<AudioSource>();
        sfx = gameObject.AddComponent<AudioSource>();
        pew = SfxSynth.Pew();
        boom = SfxSynth.Boom();
        ding = SfxSynth.Ding();
        hitClip = SfxSynth.Hit();

        var baseMat = Resources.Load<Material>("BaseMat");
        friendlyMat = new Material(baseMat) { color = new Color(1f, 0.95f, 0.4f) };
        enemyMat = new Material(baseMat) { color = new Color(1f, 0.35f, 0.25f) };

        string dir = AppBoot.Arg("-autotest");
        if (dir != null) StartCoroutine(AutoTest(dir));
    }

    // ───────────────────────── 流程 ─────────────────────────

    void Update()
    {
        float dt = Time.deltaTime;
        if (State != SkyState.Paused) rig.position += Vector3.forward * FlySpeed * dt;

        if (bannerTime > 0f) bannerTime -= dt;
        if (messageTime > 0f) messageTime -= dt;
        if (hurtFlash > 0f) hurtFlash -= dt;
        if (dingCooldown > 0f) dingCooldown -= dt;
        if (pewCooldown > 0f) pewCooldown -= dt;
        if (punishCooldown > 0f) punishCooldown -= dt;
        hpShown = Mathf.MoveTowards(hpShown, hp, dt * 40f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
        cam.transform.localPosition = camBase + Random.insideUnitSphere * shake;
        FitCamera();
        UpdateDpad();

        if (State == SkyState.Playing || State == SkyState.BossIntro)
        {
            if (GameInput.Pause) SetPaused(true);
        }
        else if (State == SkyState.Paused && GameInput.Pause) SetPaused(false);

        if (State == SkyState.BossIntro)
        {
            warnTime -= dt;
            if (warnTime <= 0f && boss != null && boss.Arrived)
            {
                State = SkyState.Playing;
                BossQuestion();
            }
        }
        if (State == SkyState.Playing) HandleHits();
        if (victoryDelay > 0f) victoryDelay -= dt;
    }

    void SetPaused(bool paused)
    {
        State = paused ? SkyState.Paused : (boss != null && !boss.Arrived ? SkyState.BossIntro : SkyState.Playing);
        Time.timeScale = paused ? 0f : 1f;
    }

    void StartGame(int rangeIndex)
    {
        range = rangeIndex;
        PlayerPrefs.SetInt(RangeKey, range);
        PlayerPrefs.Save();
        token++;
        ClearAll();
        score = 0;
        hp = MaxHp;
        hpShown = hp;
        q = null;
        asking = false;
        bannerTime = messageTime = 0f;
        Player.gameObject.SetActive(true);
        Player.ResetPose();
        Time.timeScale = 1f;
        State = SkyState.Playing;
        StartCoroutine(After(token, 1.2f, NextWave));
    }

    // 延遲動作都用 token 判斷是否還有效，所以這裡不用停掉協程
    void ClearAll()
    {
        foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
        enemies.Clear();
        if (boss != null) Destroy(boss.gameObject);
        boss = null;
        for (int i = bullets.Count - 1; i >= 0; i--) RemoveBullet(bullets[i]);
    }

    IEnumerator After(int t, float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        if (t == token && (State == SkyState.Playing || State == SkyState.BossIntro)) action();
    }

    void NextWave()
    {
        q = MulData.Next(range, q);
        var choices = MulData.Choices(q, WaveSize);
        Color[] colors = { new Color(0.85f, 0.3f, 0.3f), new Color(0.95f, 0.6f, 0.2f), new Color(0.6f, 0.35f, 0.8f), new Color(0.3f, 0.65f, 0.4f), new Color(0.55f, 0.55f, 0.6f) };
        // 散得很開、前後錯開，要對準才打得到
        float spread = WaveSize == 5 ? 4.8f : 6.2f;
        for (int i = 0; i < choices.Count; i++)
        {
            float x = (i - (choices.Count - 1) / 2f) * spread;
            var go = new GameObject("Enemy " + choices[i]);
            go.transform.SetParent(rig, false);
            var e = go.AddComponent<SkyEnemy>();
            e.Init(this, choices[i], new Vector3(x, CombatY, i % 2 == 0 ? 10.5f : 7f), colors[i % colors.Length]);
            enemies.Add(e);
        }
        asking = true;
        StartCoroutine(After(token, 0.4f, () => PlayVoice(q.QuestionVoice)));
    }

    // ───────────────────────── 命中判定 ─────────────────────────

    static float Dist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    void HandleHits()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            if (i >= bullets.Count) continue;
            var b = bullets[i];
            var p = b.transform.localPosition;
            if (b.Friendly)
            {
                if (HitEnemies(p) || HitBoss(p)) RemoveBullet(b);
            }
            else if (Player.Invulnerable <= 0f && Dist(p, Player.transform.localPosition) < SkyPlayer.HitRadius)
            {
                RemoveBullet(b);
                DamagePlayer();
            }
        }
    }

    bool HitEnemies(Vector3 p)
    {
        foreach (var e in enemies)
        {
            if (e == null || !e.Alive || e.transform.localPosition.z > 13f) continue;
            if (Dist(p, e.transform.localPosition) > SkyEnemy.HitRadius) continue;
            if (asking && e.Value == q.Answer) WaveCorrect(e);
            else WrongHit("這台是 ", e.Value, e.Retaliate);
            return true;
        }
        return false;
    }

    bool HitBoss(Vector3 p)
    {
        if (boss == null || boss.Defeated || !boss.Arrived) return false;
        for (int i = 0; i < boss.Turrets.Count; i++)
        {
            if (Dist(p, boss.TurretPos(i)) > SkyBoss.TurretRadius) continue;
            int idx = i;
            if (asking && boss.Values[i] == q.Answer) BossCorrect(i);
            else
            {
                WrongHit("這個砲台是 ", boss.Values[i], () => boss.Shield(idx));
                // 打錯會被大魔王反擊
                if (punishCooldown <= 0f) { punishCooldown = 1.2f; boss.FireFan(3); }
            }
            return true;
        }
        // 打到船身：擋掉
        var bp = boss.transform.localPosition;
        return Mathf.Abs(p.x - bp.x) < 7.6f && p.z > bp.z - 2f && p.z < bp.z + 3.5f;
    }

    void WrongHit(string what, int value, System.Action react)
    {
        react(); // 打到護盾不出聲 (一直叮叮叮太吵)
        if (q != null)
        {
            message = what + value + "，要找 " + q.a + " × " + q.b + " 的答案！";
            messageTime = 1.4f;
        }
    }

    void WaveCorrect(SkyEnemy hit)
    {
        asking = false;
        score++;
        hp = Mathf.Min(MaxHp, hp + HealOnCorrect);
        // 打中的先爆，其他的依距離一台接一台爆炸
        enemies.Sort((a, b) => Dist(a.transform.localPosition, hit.transform.localPosition).CompareTo(Dist(b.transform.localPosition, hit.transform.localPosition)));
        for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null) enemies[i].Explode(i * 0.12f);
        enemies.Clear();
        ClearEnemyBullets();
        shake = 0.5f;
        ShowAnswer();
        StartCoroutine(After(token, 2.4f, () => { if (score >= Goal) StartBoss(); else NextWave(); }));
    }

    void ShowAnswer()
    {
        bannerTop = q.Full; // 只顯示算式；口訣用念的，不寫國字
        bannerTime = 2.4f;
        messageTime = 0f;
        PlayVoice(q.ChantVoice);
    }

    void ClearEnemyBullets()
    {
        for (int i = bullets.Count - 1; i >= 0; i--) if (!bullets[i].Friendly) RemoveBullet(bullets[i]);
    }

    // ───────────────────────── 大魔王 ─────────────────────────

    void StartBoss()
    {
        State = SkyState.BossIntro;
        warnTime = 3f;
        asking = false;
        ClearEnemyBullets();
        var go = new GameObject("Boss");
        go.transform.SetParent(rig, false);
        boss = go.AddComponent<SkyBoss>();
        boss.Init(this, BossHp);
    }

    void BossQuestion()
    {
        q = MulData.Next(range, q);
        boss.SetChoices(MulData.Choices(q, boss.Turrets.Count));
        asking = true;
        PlayVoice(q.QuestionVoice);
    }

    void BossCorrect(int i)
    {
        asking = false;
        score++;
        hp = Mathf.Min(MaxHp, hp + HealOnCorrect);
        SpawnExplosion(boss.TurretPos(i) + Vector3.up * 0.5f, 1.4f, 0f);
        boss.Damage();
        shake = 0.6f;
        ShowAnswer();
        if (boss.Defeated) StartCoroutine(BossDefeated(token));
        else StartCoroutine(After(token, 2.2f, BossQuestion));
    }

    IEnumerator BossDefeated(int t)
    {
        ClearEnemyBullets();
        for (int k = 0; k < 14; k++)
        {
            if (t != token || boss == null) yield break;
            var bp = boss.transform.localPosition;
            SpawnExplosion(bp + new Vector3(Random.Range(-7f, 7f), 1f, Random.Range(-2f, 3f)), Random.Range(1.2f, 2.2f), 0f);
            shake = 0.8f;
            yield return new WaitForSeconds(0.18f);
        }
        if (t != token) yield break;
        SpawnExplosion(boss.transform.localPosition + Vector3.up, 4f, 0f);
        Destroy(boss.gameObject);
        boss = null;
        yield return new WaitForSeconds(1.2f);
        if (t != token) yield break;
        State = SkyState.Victory;
        victoryDelay = 0.5f;
    }

    // ───────────────────────── 受傷 ─────────────────────────

    void DamagePlayer()
    {
        if (State != SkyState.Playing) return;
        hp = Mathf.Max(0, hp - Damage);
        Player.Invulnerable = 1.0f;
        hurtFlash = 0.45f;
        shake = 0.7f;
        sfx.PlayOneShot(hitClip, 0.8f);
        if (hp > 0) return;

        token++;
        asking = false;
        SpawnExplosion(Player.transform.localPosition, 2f, 0f);
        Player.gameObject.SetActive(false);
        State = SkyState.GameOver;
    }

    // ───────────────────────── 子彈、爆炸、聲音 ─────────────────────────

    public void FirePlayerBullet(Vector3 localPos)
    {
        var b = GetBullet(true);
        b.transform.localPosition = localPos;
        b.Velocity = new Vector3(0, 0, 34f);
        b.Life = 0.9f;
        if (pewCooldown <= 0f) { pewCooldown = 0.12f; sfx.PlayOneShot(pew, 0.18f); }
    }

    public void FireEnemyBullet(Vector3 from, Vector3 toward, float unused)
    {
        var dir = toward - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
        var b = GetBullet(false);
        b.transform.localPosition = new Vector3(from.x, CombatY, from.z);
        b.Velocity = dir.normalized * EnemyBulletSpeed;
        b.Life = 5f;
    }

    SkyBullet GetBullet(bool friendly)
    {
        var pool = friendly ? friendlyPool : enemyPool;
        SkyBullet b;
        if (pool.Count > 0)
        {
            b = pool.Pop();
            b.gameObject.SetActive(true);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = friendly ? friendlyMat : enemyMat;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(rig, false);
            go.transform.localScale = friendly ? new Vector3(0.18f, 0.18f, 0.75f) : Vector3.one * 0.5f;
            b = go.AddComponent<SkyBullet>();
            b.Friendly = friendly;
        }
        bullets.Add(b);
        return b;
    }

    public void RemoveBullet(SkyBullet b)
    {
        if (!bullets.Remove(b)) return;
        b.gameObject.SetActive(false);
        (b.Friendly ? friendlyPool : enemyPool).Push(b);
    }

    public void SpawnExplosion(Vector3 localPos, float scale, float delay)
    {
        var go = new GameObject("Explosion");
        go.transform.SetParent(rig, false);
        go.transform.localPosition = localPos;
        go.AddComponent<SkyExplosion>().Init(scale, delay);
    }

    public void PlayBoom(float size) => sfx.PlayOneShot(boom, Mathf.Clamp(0.25f + size * 0.15f, 0.3f, 0.9f));

    void PlayVoice(string path)
    {
        var clip = Resources.Load<AudioClip>(path);
        if (clip == null) return;
        voice.Stop();
        voice.clip = clip;
        voice.Play();
    }

    // ───────────────────────── 操控 ─────────────────────────
    // 電腦：鍵盤方向鍵 / WASD。iPad：右下角半透明的方向鍵 (手指或觸控筆按住，中間四個斜角可以斜著走)

    Rect dpadRect;
    Vector2 dpadDir;

    public Vector2 MoveInput => GameInput.Move + dpadDir;

    void UpdateDpad()
    {
        dpadDir = Vector2.zero;
        if (!GameInput.PointerHeld || dpadRect.width <= 0f) return;
        var p = ui.ToGui(GameInput.PointerPosition);
        if (!dpadRect.Contains(p)) return;
        var d = new Vector2(p.x - dpadRect.center.x, dpadRect.center.y - p.y) / (dpadRect.width / 2f);
        if (d.magnitude < 0.18f) return; // 正中間不動
        // 對齊到 8 個方向
        float angle = Mathf.Round(Mathf.Atan2(d.y, d.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
        dpadDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    void DrawDpad()
    {
        float size = KidGui.Portrait ? 450f : 330f;
        dpadRect = new Rect(ui.W - size - 30f, ui.H - size - 30f, size, size);
        float cell = size / 3f;
        ui.Box(dpadRect, new Color(0f, 0f, 0f, 0.12f));
        string[] arrows = { "↖", "▲", "↗", "◀", "", "▶", "↙", "▼", "↘" };
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                int i = row * 3 + col;
                if (i == 4) continue;
                var dir = new Vector2(col - 1, 1 - row).normalized;
                bool pressed = dpadDir.sqrMagnitude > 0f && Vector2.Dot(dpadDir, dir) > 0.95f;
                bool diagonal = row != 1 && col != 1;
                var r = new Rect(dpadRect.x + col * cell + 6, dpadRect.y + row * cell + 6, cell - 12, cell - 12);
                ui.Box(r, new Color(1f, 1f, 1f, pressed ? 0.55f : diagonal ? 0.14f : 0.25f));
                ui.Label(r, "<size=" + (int)(cell * (diagonal ? 0.4f : 0.5f)) + ">" + arrows[i] + "</size>", ui.Small, new Color(1f, 1f, 1f, pressed ? 1f : 0.75f));
            }
    }

    // 直放 (iPad 轉正) 時視野變窄，把鏡頭拉廣，左右的敵機才看得到
    void FitCamera()
    {
        float halfWidth = 13f, dist = 23f;
        float vfov = 2f * Mathf.Atan(halfWidth / dist / Mathf.Max(cam.aspect, 0.1f)) * Mathf.Rad2Deg;
        cam.fieldOfView = Mathf.Clamp(vfov, 55f, 85f);
    }

    // ───────────────────────── 介面 ─────────────────────────

    void OnGUI()
    {
        ui.Begin();
        float W = ui.W, H = ui.H;

        if (hurtFlash > 0f) ui.Box(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, 0.35f * Mathf.Clamp01(hurtFlash / 0.45f)));

        switch (State)
        {
            case SkyState.Menu: DrawMenu(); return;
            case SkyState.Paused:
                DrawHud();
                if (ui.Dialog("暫停", "休息一下～", out int pc, "繼續飛", "重新開始", "回遊樂園"))
                {
                    if (pc == 0) SetPaused(false);
                    else if (pc == 1) StartGame(range);
                    else AppBoot.Load(AppBoot.ParkScene);
                }
                return;
            case SkyState.GameOver:
                DrawHud();
                if (ui.Dialog("戰機被打下來了！", "答對了 " + score + " 題，再試一次吧！", out int gc, "再玩一次", "選乘法表", "回遊樂園"))
                {
                    if (gc == 0) StartGame(range);
                    else if (gc == 1) BackToMenu();
                    else AppBoot.Load(AppBoot.ParkScene);
                }
                return;
            case SkyState.Victory:
                if (victoryDelay > 0f) { DrawHud(); return; }
                if (ui.Dialog("打敗大魔王了！", "★★★　你是九九乘法小英雄！", out int vc, "再玩一次", "選乘法表", "回遊樂園"))
                {
                    if (vc == 0) StartGame(range);
                    else if (vc == 1) BackToMenu();
                    else AppBoot.Load(AppBoot.ParkScene);
                }
                return;
        }
        DrawHud();
    }

    void BackToMenu()
    {
        token++;
        ClearAll();
        Time.timeScale = 1f;
        Player.gameObject.SetActive(true);
        Player.ResetPose();
        State = SkyState.Menu;
    }

    void DrawMenu()
    {
        float W = ui.W, H = ui.H;
        ui.Box(new Rect(0, 0, W, H), new Color(0.05f, 0.1f, 0.25f, 0.45f));
        ui.Label(new Rect(0, 30, W, 140), "九九乘法戰機", ui.Huge, new Color(1f, 0.9f, 0.35f));
        ui.Label(new Rect(20, 165, W - 40, 70), "戰機會自動發射！電腦用鍵盤 ↑↓←→，iPad 直放、用右下角的方向鍵移動，對準答案！", ui.Small, Color.white);

        // 難度
        float dw = 300, dh = 80, dy = 240;
        var easyRect = new Rect(W / 2f - dw - 10, dy, dw, dh);
        var hardRect = new Rect(W / 2f + 10, dy, dw, dh);
        ui.Box(new Rect((Hard ? hardRect : easyRect).x - 5, dy - 5, dw + 10, dh + 10), new Color(1f, 0.9f, 0.4f));
        if (GUI.Button(easyRect, "簡單", ui.Button)) SetHard(false);
        if (GUI.Button(hardRect, "困難", ui.Button)) SetHard(true);
        ui.Label(new Rect(0, dy + dh + 5, W, 40), Hard ? "<size=26>敵機多、子彈快、大魔王很硬！</size>" : "<size=26>敵機少、子彈慢，還有點點圖提示</size>", ui.Small, Color.white);

        // 乘法表範圍 (按了就開始)
        float bw = 700, bh = 120, y = 400;
        for (int i = 0; i < MulData.RangeNames.Length; i++)
        {
            var r = new Rect((W - bw) / 2f, y + i * (bh + 20), bw, bh);
            if (i == range) ui.Box(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), new Color(1f, 1f, 1f, 0.3f));
            if (GUI.Button(r, "<size=54>" + MulData.RangeNames[i] + "</size>", ui.Button)) StartGame(i);
        }
        if (GUI.Button(new Rect((W - 360) / 2f, y + 3 * (bh + 20) + 20, 360, 90), "← 回遊樂園", ui.Button)) AppBoot.Load(AppBoot.ParkScene);
    }

    void SetHard(bool hard)
    {
        Hard = hard;
        PlayerPrefs.SetInt(HardKey, hard ? 1 : 0);
        PlayerPrefs.Save();
    }

    void DrawHud()
    {
        float W = ui.W, H = ui.H;

        // 左上：血條和進度
        ui.Box(new Rect(20, 20, 480, 150), new Color(0, 0, 0, 0.35f));
        ui.Label(new Rect(30, 25, 460, 50), (Hard ? "困難　" : "簡單　") + MulData.RangeNames[range], ui.Small, Color.white);
        float frac = hp / (float)MaxHp;
        var hpColor = frac > 0.5f ? new Color(0.3f, 0.9f, 0.35f) : frac > 0.25f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.25f, 0.2f);
        ui.Bar(new Rect(30, 78, 460, 40), hpShown / MaxHp, hpColor, "♥ " + hp + " / " + MaxHp);
        if (boss == null)
        {
            string stars = "";
            for (int i = 0; i < Goal; i++) stars += i < score ? "<color=#ffe14d>★</color>" : "<color=#ffffff55>★</color>";
            ui.Label(new Rect(30, 122, 460, 44), "<size=30>" + stars + "</size>", ui.Small, Color.white);
        }

        // 題目：橫放在上方中間；直放時在血條下面
        bool portrait = KidGui.Portrait;
        var panel = portrait ? new Rect(W / 2 - 430, 190, 860, 200) : new Rect(W / 2 - 430, 20, 860, 200);

        // 大魔王血條 (題目下面)
        if (boss != null)
            ui.Bar(new Rect(W / 2 - 430, panel.yMax + 12, 860, 50), boss.Hp / (float)boss.MaxHp, new Color(0.9f, 0.2f, 0.25f), "大魔王");

        if (q != null && asking)
        {
            ui.Box(panel, new Color(1f, 1f, 1f, 0.88f));
            bool hint = !Hard;
            var textRect = hint ? new Rect(panel.x, panel.y, panel.width - 230, panel.height) : panel;
            ui.Label(textRect, "<size=120>" + q.a + " × " + q.b + " = ?</size>", ui.Huge, new Color(0.2f, 0.25f, 0.6f));
            if (hint) DrawDots(new Rect(panel.xMax - 225, panel.y + 8, 210, 184), q.a, q.b);
        }

        // 暫停按鈕
        pauseRect = new Rect(W - 150, 20, 130, 110);
        if ((State == SkyState.Playing || State == SkyState.BossIntro) && GUI.Button(pauseRect, "<size=34>暫停</size>", ui.Button)) SetPaused(true);

        // 大魔王來了
        if (State == SkyState.BossIntro && Mathf.Repeat(Time.time * 3f, 1f) < 0.7f)
        {
            ui.Box(new Rect(0, H / 2 - 90, W, 180), new Color(0.6f, 0f, 0f, 0.6f));
            ui.Label(new Rect(0, H / 2 - 90, W, 180), "！大魔王來了！", ui.Huge, new Color(1f, 0.9f, 0.3f));
        }

        // 答對的算式 (只有算式，口訣用念的)
        float bw = Mathf.Min(1000f, W - 40f);
        if (bannerTime > 0f)
        {
            float a = Mathf.Clamp01(bannerTime / 0.4f);
            var r = new Rect(W / 2 - bw / 2, H / 2 - 90, bw, 170);
            ui.Box(r, new Color(0.15f, 0.55f, 0.25f, 0.8f * a));
            ui.Label(r, bannerTop, ui.Huge, new Color(1f, 1f, 1f, a));
        }

        if (State == SkyState.Playing || State == SkyState.BossIntro) DrawDpad();
        else dpadRect = Rect.zero;

        if (bannerTime <= 0f && messageTime > 0f && !string.IsNullOrEmpty(message))
        {
            // 在方向鍵上面，不會被擋住
            var r = new Rect(W / 2 - bw / 2, dpadRect.height > 0f ? dpadRect.y - 110 : H - 230, bw, 90);
            ui.Box(r, new Color(0.2f, 0.4f, 0.7f, 0.8f));
            ui.Label(r, message, ui.Mid, Color.white);
        }
    }

    // 簡單模式的提示：a 排、每排 b 個點
    void DrawDots(Rect area, int rows, int cols)
    {
        float cell = Mathf.Min(area.width / cols, area.height / rows, 34f);
        float dot = cell * 0.72f;
        float ox = area.x + (area.width - cols * cell) / 2f, oy = area.y + (area.height - rows * cell) / 2f;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                ui.Box(new Rect(ox + c * cell, oy + r * cell, dot, dot), Color.HSVToRGB(r / 9f, 0.6f, 0.9f));
    }

    // ───────────────────────── 自動測試 ─────────────────────────

    IEnumerator AutoTest(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        string Shot(string n) => System.IO.Path.Combine(dir, n);
        yield return new WaitForSeconds(2f);
        ScreenCapture.CaptureScreenshot(Shot("sky_1_menu.png"));
        yield return new WaitForSeconds(0.5f);
        StartGame(0);
        yield return new WaitForSeconds(5f);
        ScreenCapture.CaptureScreenshot(Shot("sky_2_wave.png"));
        yield return new WaitForSeconds(0.3f);
        var wrong = enemies.Find(e => e != null && e.Value != q.Answer);
        if (wrong != null) WrongHit("這台是 ", wrong.Value, wrong.Retaliate);
        yield return new WaitForSeconds(0.2f);
        ScreenCapture.CaptureScreenshot(Shot("sky_3_wrong.png"));
        yield return new WaitForSeconds(0.3f);
        var right = enemies.Find(e => e != null && e.Value == q.Answer);
        if (right != null) WaveCorrect(right);
        yield return new WaitForSeconds(0.35f);
        ScreenCapture.CaptureScreenshot(Shot("sky_4_correct.png"));
        yield return new WaitForSeconds(0.3f);
        token++; // 取消原本排好的下一波，直接叫出大魔王
        StartCoroutine(AutoTestBoss(dir));
    }

    IEnumerator AutoTestBoss(string dir)
    {
        string Shot(string n) => System.IO.Path.Combine(dir, n);
        StartBoss();
        yield return new WaitForSeconds(1.5f);
        ScreenCapture.CaptureScreenshot(Shot("sky_5_warning.png"));
        yield return new WaitForSeconds(4f);
        ScreenCapture.CaptureScreenshot(Shot("sky_6_boss.png"));
        yield return new WaitForSeconds(0.3f);
        if (boss != null && asking)
        {
            int idx = boss.Values.IndexOf(q.Answer);
            if (idx >= 0) BossCorrect(idx);
        }
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(Shot("sky_7_boss_hit.png"));
        yield return new WaitForSeconds(1f);
        hp = 1;
        Player.Invulnerable = 0f;
        DamagePlayer();
        yield return new WaitForSeconds(1.2f);
        ScreenCapture.CaptureScreenshot(Shot("sky_8_gameover.png"));
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }
}
