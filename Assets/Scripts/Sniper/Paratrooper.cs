using UnityEngine;

// 狙擊樂園的傘兵：拿著一個注音符號從天上飄下來。
// 被打中 (而且是要找的那個符號) → 降落傘飛走、人掉下去不見；
// 一直沒被打中、落地了 → 站起來拿槍一直對玩家射擊。
public class Paratrooper : MonoBehaviour
{
    public Symbol Sym { get; private set; }
    public bool Landed { get; private set; }
    public bool Dying { get; private set; }
    public bool Alive => !Dying;
    public float LandedTime { get; private set; }

    // 瞄準判定用：符號牌子和身體
    public Vector3 LabelPos => label.position;
    public Vector3 BodyPos => transform.position + Vector3.up * 1.1f;

    SniperManager gm;
    Transform canopy, label, gun, flash;
    float speed, swayPhase, fireTimer, collapse, flashTime;
    Vector3 fallVel, spin;
    bool canopyFree;

    const float CanopyHeight = 4.4f;

    public void Init(SniperManager manager, Symbol sym, float fallSpeed)
    {
        gm = manager;
        Sym = sym;
        speed = fallSpeed;
        swayPhase = Random.value * 10f;
        fireTimer = Random.Range(0.8f, 1.6f); // 落地後先停一下才開槍
        Build();
    }

    void Build()
    {
        var olive = new Color(0.36f, 0.45f, 0.25f);
        var skin = new Color(1f, 0.82f, 0.66f);
        var boot = new Color(0.2f, 0.17f, 0.14f);

        // 人
        Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0, 1.15f, 0), new Vector3(0.62f, 0.75f, 0.4f), olive);          // 身體
        Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(0, 1.8f, 0), Vector3.one * 0.5f, skin);                       // 頭
        Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(0, 1.93f, 0), new Vector3(0.56f, 0.34f, 0.56f), olive * 0.85f); // 頭盔
        Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(-0.1f, 1.82f, 0.23f), Vector3.one * 0.07f, Color.black);     // 眼睛
        Shapes.Part(PrimitiveType.Sphere, transform, new Vector3(0.1f, 1.82f, 0.23f), Vector3.one * 0.07f, Color.black);
        for (int s = -1; s <= 1; s += 2)
        {
            Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0.16f * s, 0.42f, 0), new Vector3(0.22f, 0.7f, 0.24f), olive * 0.9f); // 腳
            Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0.16f * s, 0.06f, 0.04f), new Vector3(0.24f, 0.14f, 0.34f), boot);
            var arm = Shapes.Part(PrimitiveType.Cube, transform, new Vector3(0.42f * s, 1.4f, 0), new Vector3(0.18f, 0.6f, 0.18f), olive);
            arm.transform.localRotation = Quaternion.Euler(0, 0, 25f * s); // 手舉起來拉傘繩
        }

        // 槍 (落地以後才拿出來)
        gun = new GameObject("Gun").transform;
        gun.SetParent(transform, false);
        gun.localPosition = new Vector3(0.2f, 1.2f, 0.35f);
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, 0, 0.25f), new Vector3(0.1f, 0.12f, 0.7f), new Color(0.12f, 0.12f, 0.13f));
        flash = Shapes.Part(PrimitiveType.Sphere, gun, new Vector3(0, 0, 0.7f), Vector3.one * 0.45f, new Color(1f, 0.85f, 0.3f)).transform;
        flash.gameObject.SetActive(false);
        gun.gameObject.SetActive(false);

        // 降落傘：彩色的圓頂和傘繩
        canopy = new GameObject("Canopy").transform;
        canopy.SetParent(transform, false);
        canopy.localPosition = new Vector3(0, CanopyHeight, 0);
        var c1 = Sym.color;
        var c2 = Color.Lerp(Sym.color, Color.white, 0.6f);
        Shapes.Part(PrimitiveType.Sphere, canopy, Vector3.zero, new Vector3(3.6f, 1.5f, 3.6f), c1);
        Shapes.Part(PrimitiveType.Sphere, canopy, new Vector3(0, 0.12f, 0), new Vector3(2.4f, 1.35f, 2.4f), c2);
        Shapes.Part(PrimitiveType.Sphere, canopy, new Vector3(0, 0.25f, 0), new Vector3(1f, 1.2f, 1f), c1);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI * 2f / 6f;
            var rim = new Vector3(Mathf.Cos(a) * 1.65f, -0.15f, Mathf.Sin(a) * 1.65f);
            Jets.Beam(canopy, rim, new Vector3(0, 1.55f - CanopyHeight, 0), 0.03f, new Color(0.9f, 0.9f, 0.9f));
        }

        // 注音符號牌子 (圓形白底、永遠面向玩家)
        label = new GameObject("Label").transform;
        label.SetParent(transform, false);
        label.localPosition = new Vector3(0, CanopyHeight - 0.2f, 0);
        var disc = Shapes.Part(PrimitiveType.Cylinder, label, new Vector3(0, 0, 0.05f), new Vector3(2.3f, 0.03f, 2.3f), Color.white);
        disc.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var ring = Shapes.Part(PrimitiveType.Cylinder, label, new Vector3(0, 0, 0.08f), new Vector3(2.55f, 0.03f, 2.55f), Sym.color * 0.7f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        // 字用深色 (同色系)：白底在背光那面會變灰，淺色字會看不清楚
        var ink = Color.Lerp(Sym.color, Color.black, 0.55f);
        ink.a = 1f;
        ZhText.Make(label, Vector3.zero, Quaternion.identity, Sym.ch, ink, 1.7f, 1.7f);
    }

    // 打中了：傘飛走，人掉下去
    public void Die()
    {
        if (Dying) return;
        Dying = true;
        label.gameObject.SetActive(false);
        gun.gameObject.SetActive(false);
        canopy.SetParent(null, true);
        canopyFree = true;
        fallVel = Landed ? new Vector3(0, 4f, 0) : new Vector3(0, -2f, 0);
        spin = new Vector3(Random.Range(-200f, 200f), Random.Range(-100f, 100f), Random.Range(-300f, 300f));
        Destroy(canopy.gameObject, 4f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (canopyFree && canopy != null)
        {
            // 降落傘慢慢往上飄走
            canopy.position += new Vector3(Mathf.Sin(Time.time * 2f) * 0.5f, 2.5f, 0) * dt;
            canopy.Rotate(0, 60f * dt, 20f * dt);
        }

        if (Dying)
        {
            fallVel.y -= 14f * dt;
            transform.position += fallVel * dt;
            transform.Rotate(spin * dt, Space.Self);
            if (transform.position.y < -0.5f && fallVel.y < 0f)
            {
                gm.Poof(transform.position);
                Destroy(gameObject);
            }
            return;
        }

        // 牌子放在降落傘前面 (玩家這一邊)，永遠面向玩家；落地後移到頭上
        var toEye = (gm.EyePos - transform.position).normalized;
        float h = Mathf.Lerp(CanopyHeight - 0.2f, 3.1f, collapse);
        label.position = transform.position + Vector3.up * h + toEye * Mathf.Lerp(2.2f, 0.6f, collapse);
        label.rotation = Quaternion.LookRotation(label.position - gm.EyePos);

        if (!Landed)
        {
            var p = transform.position;
            p.y -= speed * dt;
            p.x += Mathf.Sin(Time.time * 0.7f + swayPhase) * 0.4f * dt;
            transform.position = p;
            transform.rotation = Quaternion.Euler(0, Yaw(), Mathf.Sin(Time.time * 1.3f + swayPhase) * 6f);
            if (p.y <= 0f) Land();
            return;
        }

        // 落地以後：傘塌下來，站起來對玩家開槍
        collapse = Mathf.MoveTowards(collapse, 1f, dt * 1.5f);
        if (canopy != null)
        {
            canopy.localPosition = Vector3.Lerp(new Vector3(0, CanopyHeight, 0), new Vector3(0, 0.15f, -1.6f), collapse);
            canopy.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.1f, 0.15f, 1.1f), collapse);
        }
        transform.rotation = Quaternion.Euler(0, Yaw(), 0);

        flashTime -= dt;
        flash.gameObject.SetActive(flashTime > 0f);
        if (collapse < 1f) return;
        fireTimer -= dt;
        if (fireTimer <= 0f)
        {
            fireTimer = gm.EnemyFireInterval * Random.Range(0.85f, 1.15f);
            flashTime = 0.08f;
            gm.EnemyShot(this);
        }
    }

    void Land()
    {
        Landed = true;
        LandedTime = Time.time;
        var p = transform.position;
        p.y = 0f;
        transform.position = p;
        gun.gameObject.SetActive(true);
        gm.OnLanded(this);
    }

    // 面向玩家
    float Yaw()
    {
        var d = gm.EyePos - transform.position;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }
}
