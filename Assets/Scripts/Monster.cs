using System.Collections;
using UnityEngine;

// 注音怪獸：圓滾滾的身體、生氣的大眼睛，手上舉著寫了注音的牌子。
// 會一直走向玩家，靠近後先蓄力 (頭上出現紅色驚嘆號)，再出拳或踢腳。
public class Monster : MonoBehaviour
{
    public Answer Answer { get; private set; }
    public bool Alive => !dying;

    enum AttackState { None, WindUp, Strike, Recover }

    // 停在約 3 公尺外 (太近會擋住牌子)，攻擊時往前撲過來
    const float AttackRange = 3.3f;   // 這麼近就開始攻擊
    const float StopDistance = 3.0f;  // 不會再更靠近
    const float LungeDistance = 1.1f;
    const float WindUpTime = 0.7f;
    const float StrikeTime = 0.22f;
    const float RecoverTime = 0.5f;
    const int PunchDamage = 10;
    const int KickDamage = 15;

    GameManager gm;
    Transform visual;
    Renderer bodyRenderer;
    Color baseColor;
    TextMesh label;
    Renderer labelRenderer;
    bool labelFitted;
    TextMesh warning;
    Renderer warningRenderer;
    bool warningFitted;

    // 出拳用右手、踢腳用右腳
    Transform hand, foot;
    Vector3 handRest, footRest;
    Vector3 handScale, footScale;

    float speed;
    float phase;
    float shakeTime;
    float angryTime;
    bool dying;

    AttackState state;
    float stateTime;
    float cooldown;
    bool kick;
    bool hitDone;

    public void Init(GameManager manager, Answer answer, float moveSpeed)
    {
        gm = manager;
        Answer = answer;
        speed = moveSpeed;
        phase = Random.value * 10f;
        baseColor = answer.color;
        cooldown = Random.Range(0f, 1.2f) * gm.CooldownScale; // 不要全部同時出手
        Build();
    }

    void Build()
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        var dark = baseColor * 0.7f;
        dark.a = 1f;
        var ink = new Color(0.1f, 0.1f, 0.15f);

        var body = Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0, 0.85f, 0), new Vector3(1.5f, 1.4f, 1.4f), baseColor, true);
        bodyRenderer = body.GetComponent<Renderer>();

        for (int side = -1; side <= 1; side += 2)
        {
            Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0.28f * side, 1.25f, 0.52f), Vector3.one * 0.38f, Color.white);
            Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0.28f * side, 1.22f, 0.68f), Vector3.one * 0.18f, ink);
            // 生氣的眉毛 (內低外高)
            var brow = Shapes.Part(PrimitiveType.Cube, visual, new Vector3(0.28f * side, 1.47f, 0.62f), new Vector3(0.34f, 0.07f, 0.07f), ink);
            brow.transform.localRotation = Quaternion.Euler(0, 0, 22f * side);

            var h = Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0.78f * side, 0.62f, 0.76f), Vector3.one * 0.26f, dark).transform;
            var f = Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0.35f * side, 0.1f, 0.1f), new Vector3(0.4f, 0.2f, 0.5f), dark).transform;
            if (side == 1) { hand = h; foot = f; }

            var stalk = Shapes.Part(PrimitiveType.Cylinder, visual, new Vector3(0.3f * side, 1.68f, 0), new Vector3(0.06f, 0.15f, 0.06f), dark);
            stalk.transform.localRotation = Quaternion.Euler(0, 0, -15f * side);
            Shapes.Part(PrimitiveType.Sphere, visual, new Vector3(0.35f * side, 1.85f, 0), Vector3.one * 0.2f, Color.yellow);
        }
        handRest = hand.localPosition; handScale = hand.localScale;
        footRest = foot.localPosition; footScale = foot.localScale;

        // 牌子 (也可以被打中，讓小朋友比較好瞄準)
        Shapes.Part(PrimitiveType.Cube, visual, new Vector3(0, 0.65f, 0.78f), new Vector3(1.6f, 0.85f, 0.06f), Color.white, true);

        label = MakeText("Label", new Vector3(0, 0.65f, 0.82f), Answer.display, new Color(0.12f, 0.12f, 0.2f), out labelRenderer);
        warning = MakeText("Warning", new Vector3(0, 2.35f, 0), "!", new Color(1f, 0.15f, 0.1f), out warningRenderer);
        warning.gameObject.SetActive(false);
    }

    TextMesh MakeText(string name, Vector3 localPos, string text, Color color, out Renderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(visual, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0, 180f, 0); // 讓文字面向玩家
        go.transform.localScale = Vector3.one * 0.08f;
        var tm = go.AddComponent<TextMesh>();
        tm.font = gm.ZhFont;
        tm.fontSize = 100;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = text;
        renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = gm.TextMaterial;
        return tm;
    }

    void LateUpdate()
    {
        // 文字網格產生後，把字縮放到剛好的大小
        if (!labelFitted) labelFitted = Fit(label, labelRenderer, 1.45f, 0.75f);
        if (!warningFitted && warning.gameObject.activeSelf) warningFitted = Fit(warning, warningRenderer, 0.6f, 0.7f);
    }

    static bool Fit(TextMesh tm, Renderer r, float width, float height)
    {
        var size = r.localBounds.size;
        if (size.x < 0.0001f || size.y < 0.0001f) return false;
        tm.transform.localScale = Vector3.one * Mathf.Min(width / size.x, height / size.y);
        return true;
    }

    void Update()
    {
        if (dying || gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        Vector3 pos = transform.position;
        Vector3 toPlayer = gm.PlayerPos - pos;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        Vector3 dir = dist > 0.01f ? toPlayer / dist : transform.forward;

        if (dist > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 8f);
        if (angryTime > 0f) angryTime -= dt;
        if (cooldown > 0f) cooldown -= dt * (angryTime > 0f ? 1.8f : 1f);

        if (state != AttackState.None)
        {
            UpdateAttack(dt, dist);
            return;
        }

        if (dist <= AttackRange && cooldown <= 0f && gm.TryStartAttack(this))
        {
            StartWindUp();
            return;
        }

        // 一直走向玩家，左右稍微晃動
        Vector3 move = Vector3.zero;
        if (dist > StopDistance) move += dir;
        move += Vector3.Cross(Vector3.up, dir) * Mathf.Sin(t * 1.3f + phase) * 0.3f;

        // 和其他怪獸保持距離
        foreach (var other in gm.Monsters)
        {
            if (other == this || other == null) continue;
            Vector3 away = pos - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d < 1.9f && d > 0.001f) move += away / d * (1.9f - d) * 2f;
        }

        float spd = speed * (angryTime > 0f ? 1.6f : 1f);
        pos += move * spd * dt;
        pos.x = Mathf.Clamp(pos.x, -GameManager.LaneHalfWidth, GameManager.LaneHalfWidth);
        pos.z = Mathf.Max(pos.z, gm.PlayerPos.z + 2.4f);
        pos.y = 0f;
        transform.position = pos;

        // 一跳一跳地走
        float hop = Mathf.Abs(Mathf.Sin(t * 6f + phase));
        visual.localPosition = Vector3.up * hop * 0.25f;
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(1f + (1f - hop) * 0.08f, 1f - (1f - hop) * 0.08f, 1f);

        if (shakeTime > 0f)
        {
            shakeTime -= dt;
            visual.localPosition += new Vector3(Mathf.Sin(t * 60f) * 0.12f, 0, 0);
            bodyRenderer.material.color = Color.Lerp(baseColor, new Color(1f, 0.2f, 0.2f), Mathf.Clamp01(shakeTime / 0.6f));
        }
    }

    void StartWindUp()
    {
        state = AttackState.WindUp;
        stateTime = 0f;
        kick = Random.value < 0.4f;
        hitDone = false;
        warning.gameObject.SetActive(true);
        visual.localScale = Vector3.one;
    }

    void UpdateAttack(float dt, float dist)
    {
        stateTime += dt;
        Transform limb = kick ? foot : hand;
        Vector3 rest = kick ? footRest : handRest;
        Vector3 restScale = kick ? footScale : handScale;
        Vector3 back = kick ? new Vector3(0.35f, 0.35f, -0.45f) : new Vector3(0.95f, 0.95f, 0.15f);
        Vector3 hit = kick ? new Vector3(0.15f, 0.6f, 1.45f) : new Vector3(0.25f, 1.05f, 1.6f);

        switch (state)
        {
            case AttackState.WindUp:
            {
                // 往後仰、身體變紅、驚嘆號閃爍
                float windUp = WindUpTime * gm.WindUpScale;
                float k = Mathf.Clamp01(stateTime / windUp);
                visual.localRotation = Quaternion.Euler(-18f * k, 0, 0);
                visual.localPosition = new Vector3(Mathf.Sin(Time.time * 40f) * 0.04f * k, 0, -0.15f * k);
                limb.localPosition = Vector3.Lerp(rest, back, k);
                bodyRenderer.material.color = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f), k * 0.7f);
                warning.gameObject.SetActive(Mathf.Repeat(stateTime * 8f, 1f) < 0.7f);
                if (stateTime >= windUp) { state = AttackState.Strike; stateTime = 0f; }
                break;
            }
            case AttackState.Strike:
            {
                // 往前衝，拳頭/腳變大打出去
                float k = Mathf.Clamp01(stateTime / StrikeTime);
                visual.localRotation = Quaternion.Euler(12f * k, 0, 0);
                visual.localPosition = new Vector3(0, 0.15f * Mathf.Sin(k * Mathf.PI), LungeDistance * k);
                limb.localPosition = Vector3.Lerp(back, hit, k);
                limb.localScale = restScale * (1f + 0.8f * k);
                warning.gameObject.SetActive(false);
                if (!hitDone && k >= 0.6f)
                {
                    hitDone = true;
                    if (dist <= AttackRange + 0.5f) gm.DamagePlayer(kick ? KickDamage : PunchDamage);
                }
                if (stateTime >= StrikeTime) { state = AttackState.Recover; stateTime = 0f; }
                break;
            }
            case AttackState.Recover:
            {
                float k = Mathf.Clamp01(stateTime / RecoverTime);
                visual.localRotation = Quaternion.Euler(12f * (1f - k), 0, 0);
                visual.localPosition = new Vector3(0, 0, LungeDistance * (1f - k));
                limb.localPosition = Vector3.Lerp(hit, rest, k);
                limb.localScale = Vector3.Lerp(restScale * 1.8f, restScale, k);
                bodyRenderer.material.color = Color.Lerp(new Color(1f, 0.25f, 0.2f), baseColor, k);
                if (stateTime >= RecoverTime)
                {
                    state = AttackState.None;
                    cooldown = Random.Range(1.3f, 2.2f) * gm.CooldownScale;
                    gm.EndAttack(this);
                }
                break;
            }
        }
    }

    // 打錯了：搖一搖、變紅，然後更兇 (走更快、更常攻擊)
    public void Wrong()
    {
        shakeTime = 0.6f;
        angryTime = 4f;
    }

    // 打對了：變成彩色紙花
    public void Pop()
    {
        dying = true;
        Vector3 center = transform.position + Vector3.up * 0.9f;
        for (int i = 0; i < 18; i++)
        {
            var c = Color.HSVToRGB(Random.value, 0.7f, 1f);
            var bit = Shapes.Primitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, c);
            bit.layer = Shapes.IgnoreRaycastLayer;
            bit.transform.position = center + Random.insideUnitSphere * 0.5f;
            bit.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);
            var rb = bit.AddComponent<Rigidbody>();
            rb.linearVelocity = Random.insideUnitSphere * 5f + Vector3.up * 5f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(bit, 1.6f);
        }
        Destroy(gameObject);
    }

    // 回合結束：縮小消失
    public void Poof()
    {
        if (dying) return;
        dying = true;
        StartCoroutine(Shrink());
    }

    IEnumerator Shrink()
    {
        Vector3 start = transform.localScale;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            transform.localScale = start * (1f - t / 0.3f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
