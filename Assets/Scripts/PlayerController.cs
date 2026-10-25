using UnityEngine;

// 第一人稱玩家：會自己沿著小路往前走，遇到怪獸就停下來。
// 小朋友只要用滑鼠移動準星、按左鍵發射泡泡。
public class PlayerController : MonoBehaviour
{
    public Camera Cam { get; private set; }
    public bool AimingAtMonster { get; private set; }
    public bool Walking { get; set; }

    const float WalkSpeed = 3f;
    const float EyeHeight = 1.5f;
    const float MaxYaw = 14f;   // 鏡頭稍微轉向準星，比較有臨場感
    const float MaxPitch = 8f;
    const float FireCooldown = 0.22f;
    const float AimRadius = 0.35f; // 讓小朋友比較容易打中

    GameManager gm;
    Transform gun;
    Transform muzzle;
    Vector3 gunRest;
    float fireTimer, recoil, walkPhase, shake;

    // 被打到時鏡頭晃動
    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    public void Init(GameManager manager)
    {
        gm = manager;
        gameObject.layer = Shapes.IgnoreRaycastLayer;

        var camGo = new GameObject("PlayerCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, EyeHeight, 0);
        Cam = camGo.AddComponent<Camera>();
        Cam.nearClipPlane = 0.05f;
        Cam.fieldOfView = 65f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.6f, 0.82f, 1f);
        camGo.AddComponent<AudioListener>();

        BuildGun();
    }

    void BuildGun()
    {
        // 黑灰色狙擊槍 (用基本形狀拼成)
        gun = new GameObject("SniperRifle").transform;
        gun.SetParent(Cam.transform, false);
        gunRest = new Vector3(0.26f, -0.24f, 0.6f);
        gun.localPosition = gunRest;
        gun.localScale = Vector3.one * 0.6f;

        var gunmetal = new Color(0.32f, 0.33f, 0.35f);
        var black = new Color(0.08f, 0.08f, 0.09f);
        var grey = new Color(0.5f, 0.51f, 0.53f);

        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, 0, 0.05f), new Vector3(0.08f, 0.1f, 0.5f), gunmetal);            // 槍身
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, -0.01f, -0.33f), new Vector3(0.07f, 0.13f, 0.28f), black);        // 槍托
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, 0.05f, -0.38f), new Vector3(0.06f, 0.03f, 0.16f), gunmetal);      // 托腮板
        var grip = Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, -0.1f, -0.13f), new Vector3(0.05f, 0.13f, 0.06f), black); // 握把
        grip.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, -0.09f, 0.07f), new Vector3(0.05f, 0.1f, 0.08f), black);          // 彈匣
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, -0.075f, -0.04f), new Vector3(0.012f, 0.05f, 0.07f), grey);       // 扳機護圈

        var barrel = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.01f, 0.58f), new Vector3(0.035f, 0.28f, 0.035f), black);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var brake = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.01f, 0.88f), new Vector3(0.055f, 0.04f, 0.055f), gunmetal);
        brake.transform.localRotation = Quaternion.Euler(90f, 0, 0);

        // 狙擊鏡
        var tube = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.115f, 0.03f), new Vector3(0.045f, 0.16f, 0.045f), black);
        tube.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var front = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.115f, 0.2f), new Vector3(0.07f, 0.04f, 0.07f), black);
        front.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var rear = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.115f, -0.13f), new Vector3(0.06f, 0.03f, 0.06f), black);
        rear.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lens = Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.115f, 0.241f), new Vector3(0.06f, 0.002f, 0.06f), new Color(0.2f, 0.45f, 0.8f));
        lens.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        Shapes.Part(PrimitiveType.Cylinder, gun, new Vector3(0, 0.16f, 0.03f), new Vector3(0.025f, 0.012f, 0.025f), grey);   // 調整鈕
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, 0.07f, 0.12f), new Vector3(0.03f, 0.04f, 0.025f), gunmetal);      // 鏡座
        Shapes.Part(PrimitiveType.Cube, gun, new Vector3(0, 0.07f, -0.06f), new Vector3(0.03f, 0.04f, 0.025f), gunmetal);

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(gun, false);
        muzzle.localPosition = new Vector3(0, 0.01f, 0.92f);
    }

    public void ResetPose()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        Cam.transform.localRotation = Quaternion.identity;
        Walking = false;
    }

    // 準星位置 (螢幕座標)
    public Vector2 AimScreenPos
    {
        get
        {
            Vector2 p = GameInput.PointerPosition;
            p.x = Mathf.Clamp(p.x, 0, Screen.width);
            p.y = Mathf.Clamp(p.y, 0, Screen.height);
            return p;
        }
    }

    void Update()
    {
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        // 自動往前走，走路時鏡頭輕輕晃動
        if (Walking)
        {
            transform.position += Vector3.forward * WalkSpeed * dt;
            walkPhase += dt * 7f;
        }
        float bob = Walking ? Mathf.Sin(walkPhase) * 0.05f : 0f;
        Vector3 shakeOffset = Random.insideUnitSphere * shake * 0.25f;
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
        Cam.transform.localPosition = new Vector3(0, EyeHeight + bob, 0) + shakeOffset;

        // 鏡頭稍微跟著準星轉
        Vector2 aim = AimScreenPos;
        float nx = aim.x / Mathf.Max(1, Screen.width) * 2f - 1f;
        float ny = aim.y / Mathf.Max(1, Screen.height) * 2f - 1f;
        var targetRot = Quaternion.Euler(-ny * MaxPitch, nx * MaxYaw, 0);
        Cam.transform.localRotation = Quaternion.Slerp(Cam.transform.localRotation, targetRot, dt * 3f);

        Ray ray = Cam.ScreenPointToRay(aim);
        Monster aimed = null;
        Vector3 aimPoint = ray.GetPoint(40f);
        if (Physics.SphereCast(ray, AimRadius, out RaycastHit hit, 60f, ~(1 << Shapes.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            aimed = hit.collider.GetComponentInParent<Monster>();
        }
        AimingAtMonster = aimed != null && aimed.Alive;

        // 槍口跟著準星
        gun.rotation = Quaternion.Slerp(gun.rotation, Quaternion.LookRotation(aimPoint - gun.position), dt * 12f);

        fireTimer -= dt;
        if (GameInput.Fire && fireTimer <= 0f && !gm.PointerOverButton(aim))
        {
            fireTimer = FireCooldown;
            recoil = 1f;
            gm.PlayShootSfx();
            Bubble.Launch(muzzle.position, aimPoint, aimed, gm);
        }

        recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
        gun.localPosition = gunRest + gun.localRotation * new Vector3(0, 0.02f, -0.09f) * recoil;
    }
}
