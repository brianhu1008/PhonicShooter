using System.Collections.Generic;
using UnityEngine;

// 九九乘法戰機的物件。全部放在會一直往前飛的 Rig 底下，用 Rig 的區域座標移動：
// x 左右、z 前後 (畫面上下)，戰鬥都在 y = SkyManager.CombatY 這個平面上。

// 玩家戰機 (跟著滑鼠 / 手指移動，自動連射) + 兩台僚機
public class SkyPlayer : MonoBehaviour
{
    public const float HitRadius = 0.75f;
    const float Speed = 13f;
    const float FireInterval = 0.13f;

    SkyManager gm;
    Transform jet;
    readonly List<Transform> wingmen = new List<Transform>();
    float fireTimer, wingTimer, bank;
    public float Invulnerable;

    public void Init(SkyManager manager)
    {
        gm = manager;
        jet = Jets.Player(transform);
        for (int s = -1; s <= 1; s += 2)
        {
            var w = Jets.Fighter(transform, new Color(0.3f, 0.5f, 0.85f), new Color(0.9f, 0.95f, 1f), new Color(0.5f, 0.9f, 1f));
            w.localScale = Vector3.one * 0.55f;
            w.localPosition = new Vector3(1.9f * s, 0, -1.2f);
            wingmen.Add(w);
        }
    }

    public void ResetPose()
    {
        transform.localPosition = new Vector3(0, SkyManager.CombatY, -5f);
        Invulnerable = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool playing = gm.State == SkyState.Playing;

        // 鍵盤方向鍵 / 畫面上的方向鍵 (可以斜著走)
        Vector3 before = transform.localPosition;
        if (playing)
        {
            Vector2 mv = Vector2.ClampMagnitude(gm.MoveInput, 1f);
            var p = before + new Vector3(mv.x, 0, mv.y) * Speed * dt;
            transform.localPosition = new Vector3(Mathf.Clamp(p.x, -11f, 11f), SkyManager.CombatY, Mathf.Clamp(p.z, -8f, 2f));
        }
        float vx = (transform.localPosition.x - before.x) / Mathf.Max(dt, 0.0001f);
        bank = Mathf.Lerp(bank, Mathf.Clamp(-vx * 3f, -35f, 35f), dt * 8f);
        transform.localRotation = Quaternion.Euler(0, 0, bank);

        // 被打到後閃爍
        if (Invulnerable > 0f) Invulnerable -= dt;
        bool visible = Invulnerable <= 0f || Mathf.Repeat(Invulnerable * 12f, 1f) < 0.5f;
        jet.gameObject.SetActive(visible);

        if (!playing) return;

        // 自動連射
        fireTimer -= dt;
        wingTimer -= dt;
        if (fireTimer <= 0f)
        {
            fireTimer = FireInterval;
            Vector3 o = transform.localPosition;
            gm.FirePlayerBullet(o + new Vector3(-0.22f, 0, 1.6f));
            gm.FirePlayerBullet(o + new Vector3(0.22f, 0, 1.6f));
        }
        // 僚機只跟著飛，不開火 (只有中間兩發，要對準才打得到)
    }
}

// 子彈 (玩家的往前飛，敵人的往玩家飛)
public class SkyBullet : MonoBehaviour
{
    public Vector3 Velocity;
    public bool Friendly;
    public float Life = 2f;

    void Update()
    {
        if (SkyManager.I.State != SkyState.Playing && SkyManager.I.State != SkyState.BossIntro) return;
        transform.localPosition += Velocity * Time.deltaTime;
        Life -= Time.deltaTime;
        var p = transform.localPosition;
        if (Life <= 0f || Mathf.Abs(p.x) > 16f || p.z > 18f || p.z < -14f) SkyManager.I.RemoveBullet(this);
    }
}

// 載著數字的敵機
public class SkyEnemy : MonoBehaviour
{
    public const float HitRadius = 1.15f;
    public int Value { get; private set; }
    public bool Alive { get; private set; } = true;

    SkyManager gm;
    Vector3 slot;
    float phase, fireTimer, shieldTime;
    GameObject shield;
    Transform body;

    public void Init(SkyManager manager, int value, Vector3 slotPos, Color color)
    {
        gm = manager;
        Value = value;
        slot = slotPos;
        phase = Random.value * 10f;
        fireTimer = Random.Range(1.5f, 3f) * gm.FireIntervalScale;
        transform.localPosition = new Vector3(slotPos.x * 0.6f, SkyManager.CombatY, 17f + Random.Range(0f, 3f));

        body = Jets.Enemy(transform, color);
        // 數字牌：平放在機身上方，從上面看得到
        var plate = Shapes.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0.95f, 0.1f), new Vector3(2.5f, 0.04f, 2.5f), Color.white);
        Shapes.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0.92f, 0.1f), new Vector3(2.7f, 0.03f, 2.7f), new Color(0.15f, 0.15f, 0.2f));
        ZhText.Make(transform, new Vector3(0, 1.0f, 0.1f), Quaternion.Euler(90, 0, 0), value.ToString(), new Color(0.1f, 0.1f, 0.2f), 2.0f, 1.5f);
        // 護盾：數字牌外圈發光的圓環 (不會擋住數字)
        shield = Shapes.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0.89f, 0.1f), new Vector3(3.6f, 0.02f, 3.6f), new Color(0.45f, 0.95f, 1f));
        shield.SetActive(false);
        plate.name = "Plate";
    }

    void Update()
    {
        if (!Alive) return;
        float dt = Time.deltaTime;
        if (shieldTime > 0f) { shieldTime -= dt; shield.SetActive(Mathf.Repeat(shieldTime * 10f, 1f) < 0.6f); }
        else shield.SetActive(false);
        if (gm.State != SkyState.Playing) return;

        // 飛到自己的位置，左右擺動，慢慢往玩家逼近
        slot.z = Mathf.Max(3.5f, slot.z - dt * 0.12f * gm.SpeedScale);
        var target = slot + new Vector3(Mathf.Sin(Time.time * 0.9f + phase) * 0.7f, 0, Mathf.Sin(Time.time * 0.6f + phase) * 0.5f);
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, dt * 1.6f);
        body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.9f + phase) * 15f);

        // 朝玩家開火
        fireTimer -= dt;
        if (fireTimer <= 0f && transform.localPosition.z < 14f)
        {
            fireTimer = Random.Range(2.2f, 3.6f) * gm.FireIntervalScale;
            gm.FireEnemyBullet(transform.localPosition + new Vector3(0, 0, -1.4f), gm.Player.transform.localPosition, 0f);
        }
    }

    public void Shield() => shieldTime = 0.5f;

    // 被打錯：護盾擋住，並朝玩家連發三顆子彈反擊 (不能亂掃)
    float retaliateCooldown;
    public void Retaliate()
    {
        Shield();
        if (retaliateCooldown > Time.time) return;
        retaliateCooldown = Time.time + 1.6f;
        var from = transform.localPosition + new Vector3(0, 0, -1.4f);
        var to = gm.Player.transform.localPosition;
        for (int i = -1; i <= 1; i++)
            gm.FireEnemyBullet(from, to + new Vector3(i * 1.6f, 0, 0), 0f);
    }

    public void Explode(float delay)
    {
        Alive = false;
        gm.SpawnExplosion(transform.localPosition, 1.3f, delay);
        Destroy(gameObject, delay + 0.05f);
    }
}

// 大魔王：巨大的空中要塞。身上的砲台載著答案，打中正確的砲台它才會受傷
public class SkyBoss : MonoBehaviour
{
    public const float TurretRadius = 1.3f;
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public bool Defeated { get; private set; }
    public readonly List<Transform> Turrets = new List<Transform>();
    public readonly List<int> Values = new List<int>();

    SkyManager gm;
    readonly List<TextMesh> labels = new List<TextMesh>();
    readonly List<GameObject> shields = new List<GameObject>();
    readonly List<float> shieldTimes = new List<float>();
    Renderer core;
    float flash, fanTimer, aimTimer, sway;
    Vector3 home = new Vector3(0, SkyManager.CombatY, 9.5f);

    public bool Arrived => transform.localPosition.z < home.z + 0.3f;

    public void Init(SkyManager manager, int hp)
    {
        gm = manager;
        Hp = MaxHp = hp;
        transform.localPosition = new Vector3(0, SkyManager.CombatY, 26f);
        fanTimer = 2f;
        aimTimer = 3f;

        var hull = new Color(0.42f, 0.44f, 0.5f);
        var darkHull = new Color(0.26f, 0.28f, 0.33f);
        var red = new Color(0.9f, 0.2f, 0.2f);
        // 主體
        Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0, 0, 0.8f), new Vector3(9f, 1.6f, 4.2f), hull);
        Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0, 0.9f, 1.4f), new Vector3(4f, 1.4f, 2.6f), darkHull);
        Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0, 1.7f, 1.6f), new Vector3(1.8f, 0.8f, 1.4f), hull);
        for (int s = -1; s <= 1; s += 2)
        {
            // 兩側的大砲艙
            Shapes.Part(PrimitiveType.Cube, transform, new Vector3(6.2f * s, 0, 1.2f), new Vector3(3.2f, 1.9f, 5.2f), darkHull);
            Shapes.Part(PrimitiveType.Cube, transform, new Vector3(6.2f * s, 1.05f, 1.2f), new Vector3(2.4f, 0.3f, 4.2f), red);
            for (int k = 0; k < 3; k++)
            {
                var barrel = Shapes.Part(PrimitiveType.Cylinder, transform, new Vector3(6.2f * s + (k - 1) * 0.8f, 0.1f, -1.9f), new Vector3(0.28f, 0.6f, 0.28f), new Color(0.15f, 0.15f, 0.18f));
                barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            // 引擎噴焰
            Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(6.2f * s, 0, 4f), new Vector3(1.4f, 1f, 1.2f), new Color(1f, 0.55f, 0.2f));
        }
        // 發光的核心
        core = Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(0, 0.6f, -0.9f), Vector3.one * 1.6f, new Color(1f, 0.3f, 0.3f)).GetComponent<Renderer>();
        // 裝飾的燈
        for (int i = 0; i < 8; i++)
            Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(-3.8f + i * 1.08f, 0.85f, -1.2f), Vector3.one * 0.25f, new Color(1f, 0.9f, 0.3f));

        // 答案砲台
        float[] xs = { -5.4f, -1.8f, 1.8f, 5.4f };
        foreach (var x in xs)
        {
            var t = new GameObject("Turret").transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3(x, 0.4f, -2.6f);
            Shapes.Part(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(1.6f, 0.35f, 1.6f), darkHull);
            Shapes.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.4f, 0), new Vector3(2.1f, 0.04f, 2.1f), Color.white);
            labels.Add(ZhText.Make(t, new Vector3(0, 0.45f, 0), Quaternion.Euler(90, 0, 0), "?", new Color(0.7f, 0.1f, 0.1f), 1.6f, 1.1f));
            var sh = Shapes.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.37f, 0), new Vector3(3f, 0.02f, 3f), new Color(0.45f, 0.95f, 1f));
            sh.SetActive(false);
            shields.Add(sh);
            shieldTimes.Add(0f);
            Turrets.Add(t);
            Values.Add(0);
        }
    }

    public void SetChoices(List<int> values)
    {
        for (int i = 0; i < Turrets.Count; i++)
        {
            Values[i] = values[i];
            ZhText.SetText(labels[i], values[i].ToString());
        }
    }

    public Vector3 TurretPos(int i) => transform.localPosition + Turrets[i].localPosition;

    public void Shield(int i) => shieldTimes[i] = 0.5f;

    public void Damage()
    {
        Hp--;
        flash = 0.6f;
        if (Hp <= 0) Defeated = true;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < shields.Count; i++)
        {
            if (shieldTimes[i] > 0f) shieldTimes[i] -= dt;
            shields[i].SetActive(shieldTimes[i] > 0f && Mathf.Repeat(shieldTimes[i] * 10f, 1f) < 0.6f);
        }
        if (flash > 0f) flash -= dt;
        core.material.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), Color.white, Mathf.Repeat(Time.time * 2f, 1f) * 0.3f + (flash > 0f ? 0.7f : 0f));

        if (Defeated || (gm.State != SkyState.Playing && gm.State != SkyState.BossIntro)) return;

        // 飛進來，然後左右移動
        sway += dt;
        var target = Arrived ? home + new Vector3(Mathf.Sin(sway * 0.5f) * 2.5f, 0, 0) : home;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, (Arrived ? 3f : 6f) * dt);
        if (gm.State != SkyState.Playing) return;

        // 攻擊：扇形彈幕 + 瞄準射擊，血越少打得越兇
        float anger = 1f + (1f - Hp / (float)MaxHp) * 0.8f;
        fanTimer -= dt * anger;
        aimTimer -= dt * anger;
        if (fanTimer <= 0f)
        {
            fanTimer = 2.4f * gm.FireIntervalScale;
            FireFan(gm.Hard ? 7 : 5);
        }
        if (aimTimer <= 0f)
        {
            aimTimer = 1.8f * gm.FireIntervalScale;
            for (int s = -1; s <= 1; s += 2)
                gm.FireEnemyBullet(transform.localPosition + new Vector3(6.2f * s, 0, -2.4f), gm.Player.transform.localPosition, 0f);
        }
    }

    public void FireFan(int count)
    {
        var from = transform.localPosition + new Vector3(0, 0, -1.8f);
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-40f, 40f, count == 1 ? 0.5f : i / (float)(count - 1));
            gm.FireEnemyBullet(from, from + Quaternion.Euler(0, angle, 0) * Vector3.back * 10f, 0f);
        }
    }
}

// 爆炸：火球變大再縮小 + 碎片飛散 + 黑煙
public class SkyExplosion : MonoBehaviour
{
    float delay, t;
    float size;
    Transform fire, core, smoke;
    readonly List<Transform> bits = new List<Transform>();
    readonly List<Vector3> bitVel = new List<Vector3>();
    bool started;

    public void Init(float scale, float startDelay)
    {
        size = scale;
        delay = startDelay;
    }

    void Begin()
    {
        started = true;
        fire = Shapes.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, new Color(1f, 0.55f, 0.15f)).transform;
        core = Shapes.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, new Color(1f, 0.95f, 0.6f)).transform;
        smoke = Shapes.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, new Color(0.25f, 0.22f, 0.22f)).transform;
        for (int i = 0; i < 10; i++)
        {
            var c = i % 2 == 0 ? new Color(0.35f, 0.35f, 0.4f) : new Color(1f, 0.6f, 0.2f);
            var b = Shapes.Part(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one * Random.Range(0.15f, 0.35f) * size, c).transform;
            bits.Add(b);
            var v = Random.insideUnitSphere * 9f * size;
            v.y = Mathf.Abs(v.y) * 0.4f;
            bitVel.Add(v);
        }
        SkyManager.I.PlayBoom(size);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f) { delay -= dt; return; }
        if (!started) Begin();
        t += dt;
        float k = t / 0.9f;
        fire.localScale = Vector3.one * size * 2.6f * Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI);
        core.localScale = Vector3.one * size * 1.6f * Mathf.Sin(Mathf.Clamp01(k * 2f) * Mathf.PI);
        smoke.localPosition = new Vector3(0, -0.5f, -k * 1.5f);
        smoke.localScale = Vector3.one * size * 2.2f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (k > 0.2f ? 1f : 0f);
        for (int i = 0; i < bits.Count; i++)
        {
            bitVel[i] += Vector3.down * 12f * dt;
            bits[i].localPosition += bitVel[i] * dt;
            bits[i].Rotate(400f * dt, 300f * dt, 0);
        }
        if (k >= 1f) Destroy(gameObject);
    }
}
