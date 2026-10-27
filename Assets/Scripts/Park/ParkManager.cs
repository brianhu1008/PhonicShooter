using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 佩佩喬治樂園：遊戲的入口。點遊樂設施進入：
//   摩天輪 → 注音怪獸大作戰；戰機旋轉設施 → 九九乘法戰機；
//   後面海上的卡皮巴拉發音島 (滑鼠移到畫面上方，鏡頭會帶過去) → Phonics Island
public class ParkManager : MonoBehaviour
{
    readonly KidGui ui = new KidGui();
    Camera cam;
    Transform wheel, jetRide, carousel, islandCapy;
    readonly List<Transform> gondolas = new List<Transform>();
    readonly List<Transform> rideJets = new List<Transform>();
    readonly List<Transform> horses = new List<Transform>();
    readonly List<Transform> balloons = new List<Transform>();
    Collider wheelHit, jetHit, islandHit;
    TextMesh wheelSign, jetSign, islandSign;
    string hovered;
    string leaving;
    float fade;
    float focus, focusTarget; // 0 = 看樂園，1 = 看後面的小島

    void Awake()
    {
        BuildWorld();
        var camGo = new GameObject("ParkCamera");
        camGo.tag = "MainCamera";
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.8f, 1f);
        camGo.AddComponent<AudioListener>();
        UpdateCamera();

        string dir = AppBoot.Arg("-autotest");
        if (dir != null) StartCoroutine(AutoTest(dir));
    }

    // ───────────────────────── 場景 ─────────────────────────

    void BuildWorld()
    {
        var root = new GameObject("World").transform;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45f, -40f, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.6f, 0.82f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 190f;
        QualitySettings.shadowDistance = 100f;

        // 大海、沙灘、草地、廣場、步道 (不要碰撞元件：圓柱的碰撞是巨大的膠囊，會擋住點擊)
        Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.4f, 30f), new Vector3(500f, 0.1f, 500f), new Color(0.25f, 0.58f, 0.85f));
        Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.2f, 4f), new Vector3(92f, 0.1f, 86f), new Color(0.92f, 0.84f, 0.6f));
        Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.1f, 4f), new Vector3(84f, 0.1f, 78f), new Color(0.35f, 0.65f, 0.28f));
        Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.01f, 4f), new Vector3(36f, 0.02f, 30f), new Color(0.93f, 0.85f, 0.68f));
        Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 0.02f, 32f), new Vector3(6f, 0.02f, 24f), new Color(0.88f, 0.78f, 0.6f));
        // 廣場上的彩色地磚
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(Mathf.Cos(a) * 4f, 0.03f, 4f + Mathf.Sin(a) * 4f), new Vector3(1.4f, 0.02f, 1.4f), Color.HSVToRGB(i / 12f, 0.45f, 1f));
        }

        BuildGate(root, new Vector3(0, 0, 38f)); // 樂園後面，通往小島的碼頭入口
        BuildFerrisWheel(root, new Vector3(-11f, 0, 6f));
        BuildJetRide(root, new Vector3(11f, 0, 6f));
        BuildCarousel(root, new Vector3(0, 0, 17f));
        BuildDecor(root);
        BuildIsland(root, new Vector3(0, 0, 92f));
    }

    // 卡皮巴拉發音島：在樂園後面的海上，有木頭碼頭走過去
    void BuildIsland(Transform root, Vector3 p)
    {
        var wood = new Color(0.62f, 0.45f, 0.28f);
        // 碼頭
        for (float z = 44f; z < p.z - 14f; z += 1.2f)
        {
            Shapes.Part(PrimitiveType.Cube, root, new Vector3(0, 0.2f, z), new Vector3(3.6f, 0.15f, 1.0f), wood * (z % 2.4f < 1.2f ? 1f : 0.9f));
            if (Mathf.Repeat(z, 6f) < 1.2f)
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(1.9f * s, 0.2f, z), new Vector3(0.25f, 0.9f, 0.25f), wood * 0.8f);
        }

        var isle = new GameObject("CapybaraIsland").transform;
        isle.SetParent(root, false);
        isle.localPosition = p;
        // 沙灘、草地小山
        Shapes.Part(PrimitiveType.Cylinder, isle, new Vector3(0, -0.15f, 0), new Vector3(38f, 0.3f, 32f), new Color(0.95f, 0.87f, 0.62f));
        Shapes.Part(PrimitiveType.Sphere, isle, new Vector3(0, -1.2f, 2f), new Vector3(30f, 4.4f, 24f), new Color(0.4f, 0.72f, 0.32f));
        // 椰子樹
        var rng = new System.Random(9);
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2f / 9f + 0.3f;
            var tp = new Vector3(Mathf.Cos(a) * 13f, 0, Mathf.Sin(a) * 10.5f + 1f);
            if (tp.z < -6f && Mathf.Abs(tp.x) < 5f) continue; // 碼頭上岸的地方
            float lean = (float)rng.NextDouble() * 20f - 10f;
            var palm = new GameObject("Palm").transform;
            palm.SetParent(isle, false);
            palm.localPosition = tp;
            palm.localRotation = Quaternion.Euler(lean, (float)rng.NextDouble() * 360f, 0);
            Shapes.Part(PrimitiveType.Cylinder, palm, new Vector3(0, 2.5f, 0), new Vector3(0.45f, 2.5f, 0.45f), new Color(0.6f, 0.45f, 0.3f));
            for (int k = 0; k < 5; k++)
            {
                var leaf = Shapes.Part(PrimitiveType.Sphere, palm, Vector3.zero, new Vector3(0.8f, 0.25f, 3.2f), new Color(0.2f, 0.6f, 0.25f));
                leaf.transform.localRotation = Quaternion.Euler(20f, k * 72f, 0);
                leaf.transform.localPosition = new Vector3(0, 5.1f, 0) + leaf.transform.localRotation * new Vector3(0, 0, 1.3f);
            }
        }
        // 溫泉 (冒著熱氣)
        Shapes.Part(PrimitiveType.Cylinder, isle, new Vector3(-7f, 0.9f, 4f), new Vector3(5f, 0.15f, 4f), new Color(0.55f, 0.85f, 0.9f));
        for (int i = 0; i < 4; i++)
            Shapes.Part(PrimitiveType.Sphere, isle, new Vector3(-7f + i * 0.7f - 1f, 1.8f + i * 0.4f, 4f), Vector3.one * (0.8f + i * 0.2f), new Color(0.96f, 0.97f, 1f));

        // 巨大的卡皮巴拉 (PhonicIsland 的 3D 模型)
        islandCapy = BuildCapybara(isle, new Vector3(2f, 1.2f, 2f), 6f);

        islandSign = Sign(isle, new Vector3(0, 2.4f, -11.5f), "卡皮巴拉發音島", "Phonics Island", new Color(0.55f, 0.38f, 0.22f));
        islandHit = AddHitBox(isle.gameObject.transform, new Vector3(0, 4f, 0), new Vector3(34f, 12f, 30f));
    }

    // 從 PhonicIsland 的資源載入卡皮巴拉模型，縮放到指定高度
    Transform BuildCapybara(Transform parent, Vector3 pos, float height)
    {
        var holder = new GameObject("Capybara").transform;
        holder.SetParent(parent, false);
        holder.localPosition = pos;
        holder.localRotation = Quaternion.Euler(0, 180f, 0); // 面向樂園
        var prefab = Resources.Load<GameObject>("Models/Capybara");
        if (prefab == null)
        {
            // 找不到模型時用簡單的形狀代替
            Shapes.Part(PrimitiveType.Sphere, holder, new Vector3(0, height * 0.45f, 0), new Vector3(height * 0.8f, height * 0.7f, height * 1.2f), new Color(0.6f, 0.42f, 0.28f));
            return holder;
        }
        var model = Instantiate(prefab, holder);
        var tex = Resources.Load<Texture2D>("Models/Capybara_BaseColor");
        var baseMat = Resources.Load<Material>("BaseMat");
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(baseMat);
            if (tex != null) m.mainTexture = tex;
            else m.color = new Color(0.6f, 0.42f, 0.28f);
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
        // 依模型大小縮放、放在地面上
        var b = new Bounds(holder.position, Vector3.zero);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        if (b.size.y > 0.001f)
        {
            float s = height / b.size.y;
            model.transform.localScale *= s;
            b = new Bounds(holder.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            model.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.min.y, holder.position.z - b.center.z);
        }
        return holder;
    }

    void BuildGate(Transform root, Vector3 p)
    {
        var gate = new GameObject("Gate").transform;
        gate.SetParent(root, false);
        gate.localPosition = p;
        var red = new Color(0.95f, 0.35f, 0.35f);
        for (int s = -1; s <= 1; s += 2)
        {
            Shapes.Part(PrimitiveType.Cylinder, gate, new Vector3(4f * s, 2.5f, 0), new Vector3(0.8f, 2.5f, 0.8f), red);
            Shapes.Part(PrimitiveType.Sphere, gate, new Vector3(4f * s, 5.4f, 0), Vector3.one * 1.2f, new Color(1f, 0.85f, 0.25f));
        }
        Shapes.Part(PrimitiveType.Cube, gate, new Vector3(0, 5.4f, 0), new Vector3(9.5f, 1.8f, 0.5f), new Color(0.3f, 0.45f, 0.95f));
        ZhText.Make(gate, new Vector3(0, 5.4f, -0.3f), Quaternion.identity, "往卡皮巴拉發音島 →", Color.white, 8.4f, 1.2f);
        ZhText.Make(gate, new Vector3(0, 5.4f, 0.3f), Quaternion.Euler(0, 180f, 0), "佩佩喬治樂園", Color.white, 8.4f, 1.4f);
    }

    void BuildFerrisWheel(Transform root, Vector3 p)
    {
        var baseT = new GameObject("FerrisWheel").transform;
        baseT.SetParent(root, false);
        baseT.localPosition = p;
        baseT.localRotation = Quaternion.Euler(0, 20f, 0);
        var white = new Color(0.95f, 0.95f, 1f);
        Vector3 hub = new Vector3(0, 9f, 0);
        const float R = 7f;

        // 支架
        foreach (var leg in new[] { new Vector3(-3.5f, 0, -1.6f), new Vector3(3.5f, 0, -1.6f), new Vector3(-3.5f, 0, 1.6f), new Vector3(3.5f, 0, 1.6f) })
            Jets.Beam(baseT, leg, hub + new Vector3(0, 0, Mathf.Sign(leg.z) * 0.6f), 0.35f, white);
        Shapes.Part(PrimitiveType.Cube, baseT, new Vector3(0, 0.3f, 0), new Vector3(9f, 0.6f, 4.5f), new Color(0.6f, 0.6f, 0.7f));

        wheel = new GameObject("Wheel").transform;
        wheel.SetParent(baseT, false);
        wheel.localPosition = hub;
        var hubGo = Shapes.Part(PrimitiveType.Cylinder, wheel, Vector3.zero, new Vector3(1.4f, 0.8f, 1.4f), new Color(1f, 0.8f, 0.25f));
        hubGo.transform.localRotation = Quaternion.Euler(90, 0, 0);

        const int N = 12;
        for (int ring = -1; ring <= 1; ring += 2)
        {
            for (int i = 0; i < N * 2; i++)
            {
                float a0 = i * Mathf.PI * 2f / (N * 2), a1 = (i + 1) * Mathf.PI * 2f / (N * 2);
                var p0 = new Vector3(Mathf.Cos(a0) * R, Mathf.Sin(a0) * R, ring * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * R, Mathf.Sin(a1) * R, ring * 0.5f);
                Jets.Beam(wheel, p0, p1, 0.22f, new Color(0.95f, 0.4f, 0.6f));
                Shapes.Part(PrimitiveType.Sphere, wheel, p0, Vector3.one * 0.35f, Color.HSVToRGB(i / (float)(N * 2), 0.6f, 1f));
            }
        }
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            var rim = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0);
            Jets.Beam(wheel, Vector3.zero, rim + new Vector3(0, 0, 0.5f), 0.15f, white);
            Jets.Beam(wheel, Vector3.zero, rim + new Vector3(0, 0, -0.5f), 0.15f, white);

            // 車廂：掛在輪子上，自己保持直立
            var pivot = new GameObject("Gondola").transform;
            pivot.SetParent(wheel, false);
            pivot.localPosition = rim;
            var car = new GameObject("Car").transform;
            car.SetParent(pivot, false);
            var c = Color.HSVToRGB(i / (float)N, 0.65f, 1f);
            Jets.Beam(car, Vector3.zero, new Vector3(0, -0.8f, 0), 0.1f, white);
            Shapes.Part(PrimitiveType.Cube, car, new Vector3(0, -1.4f, 0), new Vector3(1.3f, 1.0f, 1.1f), c);
            Shapes.Part(PrimitiveType.Cube, car, new Vector3(0, -0.85f, 0), new Vector3(1.5f, 0.15f, 1.3f), c * 0.8f);
            Shapes.Part(PrimitiveType.Cube, car, new Vector3(0, -1.3f, 0.56f), new Vector3(1.0f, 0.45f, 0.02f), new Color(0.7f, 0.9f, 1f));
            gondolas.Add(car);
        }

        wheelSign = Sign(baseT, new Vector3(0, 2.2f, -3f), "注音摩天輪", "ㄅㄆㄇㄈ", new Color(0.95f, 0.45f, 0.65f));
        wheelHit = AddHitBox(baseT, new Vector3(0, 8f, 0), new Vector3(16f, 17f, 5f));
    }

    void BuildJetRide(Transform root, Vector3 p)
    {
        var baseT = new GameObject("JetRide").transform;
        baseT.SetParent(root, false);
        baseT.localPosition = p;
        baseT.localRotation = Quaternion.Euler(0, -20f, 0);

        Shapes.Part(PrimitiveType.Cylinder, baseT, new Vector3(0, 0.3f, 0), new Vector3(12f, 0.3f, 12f), new Color(0.55f, 0.6f, 0.75f));
        Shapes.Part(PrimitiveType.Cylinder, baseT, new Vector3(0, 3.5f, 0), new Vector3(1.2f, 3.5f, 1.2f), new Color(0.95f, 0.75f, 0.3f));
        Shapes.Part(PrimitiveType.Sphere, baseT, new Vector3(0, 7.4f, 0), Vector3.one * 2f, new Color(0.35f, 0.6f, 1f));
        Shapes.Part(PrimitiveType.Cylinder, baseT, new Vector3(0, 8.6f, 0), new Vector3(0.12f, 0.8f, 0.12f), Color.white);
        Shapes.Part(PrimitiveType.Sphere, baseT, new Vector3(0, 9.5f, 0), Vector3.one * 0.6f, Color.red);

        jetRide = new GameObject("Arms").transform;
        jetRide.SetParent(baseT, false);
        jetRide.localPosition = new Vector3(0, 6.6f, 0);
        Color[] colors = { new Color(0.35f, 0.55f, 0.95f), new Color(0.95f, 0.4f, 0.35f), new Color(0.4f, 0.8f, 0.45f), new Color(1f, 0.7f, 0.2f), new Color(0.75f, 0.45f, 0.95f), new Color(0.3f, 0.8f, 0.85f) };
        for (int i = 0; i < 6; i++)
        {
            float a = i * 360f / 6f;
            var arm = new GameObject("Arm").transform;
            arm.SetParent(jetRide, false);
            arm.localRotation = Quaternion.Euler(0, a, 0);
            Jets.Beam(arm, Vector3.zero, new Vector3(0, -1.2f, 5.2f), 0.18f, Color.white);
            var holder = new GameObject("JetHolder").transform;
            holder.SetParent(arm, false);
            holder.localPosition = new Vector3(0, -1.6f, 5.2f);
            var jet = Jets.Fighter(holder, colors[i], Color.white, new Color(1f, 0.85f, 0.3f));
            jet.localRotation = Quaternion.Euler(0, 90f, 0); // 朝著繞圈的方向
            jet.localScale = Vector3.one * 0.7f;
            rideJets.Add(holder);
        }

        jetSign = Sign(baseT, new Vector3(0, 2.2f, -6.8f), "九九乘法戰機", "9 × 9", new Color(0.35f, 0.55f, 0.95f));
        jetHit = AddHitBox(baseT, new Vector3(0, 5f, 0), new Vector3(15f, 11f, 15f));
    }

    void BuildCarousel(Transform root, Vector3 p)
    {
        var baseT = new GameObject("Carousel").transform;
        baseT.SetParent(root, false);
        baseT.localPosition = p;
        Shapes.Part(PrimitiveType.Cylinder, baseT, new Vector3(0, 0.3f, 0), new Vector3(8f, 0.3f, 8f), new Color(1f, 0.9f, 0.6f));
        carousel = new GameObject("Spin").transform;
        carousel.SetParent(baseT, false);
        Shapes.Part(PrimitiveType.Cylinder, carousel, new Vector3(0, 2.5f, 0), new Vector3(0.8f, 2.2f, 0.8f), new Color(1f, 0.5f, 0.6f));
        Shapes.Part(PrimitiveType.Cylinder, carousel, new Vector3(0, 4.8f, 0), new Vector3(8.4f, 0.2f, 8.4f), new Color(0.95f, 0.35f, 0.45f));
        Shapes.Part(PrimitiveType.Sphere, carousel, new Vector3(0, 5.2f, 0), new Vector3(8f, 2.4f, 8f), new Color(1f, 0.85f, 0.9f));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            var pos = new Vector3(Mathf.Cos(a) * 3f, 0, Mathf.Sin(a) * 3f);
            Shapes.Part(PrimitiveType.Cylinder, carousel, pos + Vector3.up * 2.6f, new Vector3(0.1f, 2.2f, 0.1f), new Color(1f, 0.85f, 0.3f));
            var horse = new GameObject("Horse").transform;
            horse.SetParent(carousel, false);
            horse.localPosition = pos + Vector3.up * 1.6f;
            horse.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            var hc = Color.HSVToRGB(i / 8f, 0.3f, 1f);
            Shapes.Part(PrimitiveType.Cube, horse, Vector3.zero, new Vector3(0.5f, 0.6f, 1.3f), hc);
            Shapes.Part(PrimitiveType.Cube, horse, new Vector3(0, 0.5f, 0.6f), new Vector3(0.35f, 0.7f, 0.4f), hc);
            Shapes.Part(PrimitiveType.Cube, horse, new Vector3(0, 0.75f, 0.85f), new Vector3(0.3f, 0.3f, 0.5f), hc);
            horses.Add(horse);
        }
    }

    void BuildDecor(Transform root)
    {
        var rng = new System.Random(3);
        // 外圍的樹
        for (int i = 0; i < 40; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = 26f + (float)rng.NextDouble() * 12f; // 在草地上，不會長到海裡
            var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 0.9f + 4f);
            if (p.z > 20f && Mathf.Abs(p.x) < 7f) continue; // 不要擋住往小島的碼頭
            float s = 0.9f + (float)rng.NextDouble() * 0.8f;
            Shapes.Part(PrimitiveType.Cylinder, root, p + Vector3.up * 1.3f * s, new Vector3(0.45f, 1.3f, 0.45f) * s, new Color(0.55f, 0.35f, 0.2f));
            Shapes.Part(PrimitiveType.Sphere, root, p + Vector3.up * 3.2f * s, Vector3.one * 2.8f * s, new Color(0.22f, 0.55f + (float)rng.NextDouble() * 0.15f, 0.25f));
        }
        // 路燈和旗子
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            var p = new Vector3(Mathf.Cos(a) * 20f, 0, 4f + Mathf.Sin(a) * 17f);
            Shapes.Part(PrimitiveType.Cylinder, root, p + Vector3.up * 2f, new Vector3(0.15f, 2f, 0.15f), new Color(0.3f, 0.3f, 0.35f));
            Shapes.Part(PrimitiveType.Cube, root, p + new Vector3(0.45f, 3.5f, 0), new Vector3(0.8f, 0.5f, 0.05f), Color.HSVToRGB(i / 10f, 0.7f, 1f));
        }
        // 入口旁的氣球
        for (int i = 0; i < 9; i++)
        {
            var b = new GameObject("Balloon").transform;
            b.SetParent(root, false);
            b.localPosition = new Vector3(-6.5f + (i % 3) * 0.7f + (i > 4 ? 12.4f : 0), 0, -11.5f - (i % 2) * 0.6f);
            float h = 4.5f + (i % 3) * 0.6f;
            Shapes.Part(PrimitiveType.Cylinder, b, Vector3.up * h / 2f, new Vector3(0.03f, h / 2f, 0.03f), Color.white);
            Shapes.Part(PrimitiveType.Sphere, b, Vector3.up * (h + 0.4f), new Vector3(0.8f, 1f, 0.8f), Color.HSVToRGB(i / 9f, 0.7f, 1f));
            balloons.Add(b);
        }
    }

    TextMesh Sign(Transform parent, Vector3 pos, string title, string sub, Color color)
    {
        var sign = new GameObject("Sign").transform;
        sign.SetParent(parent, false);
        sign.localPosition = pos;
        for (int s = -1; s <= 1; s += 2)
            Shapes.Part(PrimitiveType.Cylinder, sign, new Vector3(2.4f * s, -1.1f, 0), new Vector3(0.15f, 1.1f, 0.15f), Color.white);
        Shapes.Part(PrimitiveType.Cube, sign, Vector3.zero, new Vector3(5.6f, 2f, 0.2f), color);
        var t = ZhText.Make(sign, new Vector3(0, 0.3f, -0.12f), Quaternion.identity, title, Color.white, 5f, 1f);
        ZhText.Make(sign, new Vector3(0, -0.55f, -0.12f), Quaternion.identity, sub, new Color(1f, 1f, 0.7f), 4f, 0.55f);
        return t;
    }

    static Collider AddHitBox(Transform t, Vector3 center, Vector3 size)
    {
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        return box;
    }

    // ───────────────────────── 動畫、點擊 ─────────────────────────

    void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        wheel.localRotation = Quaternion.Euler(0, 0, t * 8f);
        foreach (var g in gondolas) g.rotation = wheel.parent.rotation; // 車廂保持直立
        jetRide.localRotation = Quaternion.Euler(0, t * 35f, 0);
        for (int i = 0; i < rideJets.Count; i++)
            rideJets[i].localPosition = new Vector3(0, -1.6f + Mathf.Sin(t * 1.5f + i) * 0.6f, 5.2f);
        carousel.localRotation = Quaternion.Euler(0, -t * 20f, 0);
        for (int i = 0; i < horses.Count; i++)
        {
            var hp = horses[i].localPosition;
            hp.y = 1.6f + Mathf.Sin(t * 2.5f + i) * 0.3f;
            horses[i].localPosition = hp;
        }
        for (int i = 0; i < balloons.Count; i++)
            balloons[i].localRotation = Quaternion.Euler(Mathf.Sin(t + i) * 6f, 0, Mathf.Cos(t * 0.8f + i) * 6f);

        islandCapy.localRotation = Quaternion.Euler(0, 180f + Mathf.Sin(t * 0.6f) * 12f, 0);

        // 滑鼠移到畫面上方 → 鏡頭帶到後面的小島；移到下方 → 回到樂園 (iPad 點上方 / 下方也可以)
        float py = GameInput.PointerPosition.y / Mathf.Max(1, Screen.height);
        if (py > 0.78f) focusTarget = 1f;
        else if (py < 0.4f) focusTarget = 0f;
        focus = Mathf.MoveTowards(focus, focusTarget, dt * 0.8f);
        UpdateCamera();

        // 滑過 / 點設施
        hovered = null;
        if (leaving == null)
        {
            Ray ray = cam.ScreenPointToRay(GameInput.PointerPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 300f))
            {
                if (hit.collider == wheelHit) hovered = AppBoot.ZhuyinScene;
                else if (hit.collider == jetHit) hovered = AppBoot.MultiplyScene;
                else if (hit.collider == islandHit) hovered = AppBoot.IslandScene;
            }
            if (hovered == AppBoot.IslandScene) focusTarget = 1f; // 指著小島就停在小島
            if (hovered != null && GameInput.Fire && !PointerOverUi()) Go(hovered);
        }
        float pulse = 1f + Mathf.Sin(t * 8f) * 0.08f;
        wheelSign.transform.parent.localScale = Vector3.one * (hovered == AppBoot.ZhuyinScene ? pulse : 1f);
        jetSign.transform.parent.localScale = Vector3.one * (hovered == AppBoot.MultiplyScene ? pulse : 1f);
        islandSign.transform.parent.localScale = Vector3.one * (hovered == AppBoot.IslandScene ? pulse * 1.3f : 1.3f);

        if (leaving != null)
        {
            fade += dt * 2.5f;
            if (fade >= 1f) { var s = leaving; leaving = null; AppBoot.Load(s); }
        }
    }

    void UpdateCamera()
    {
        // 樂園：慢慢左右環顧；小島：從碼頭上空看過去
        float yaw = Mathf.Sin(Time.time * 0.12f) * 22f;
        var center = new Vector3(0, 4f, 4f);
        Vector3 parkPos = center + Quaternion.Euler(0, yaw * (1f - focus), 0) * new Vector3(0, 11f, -34f);
        Vector3 parkLook = center + Vector3.up * 2.5f;
        Vector3 islePos = new Vector3(Mathf.Sin(Time.time * 0.15f) * 6f, 17f, 54f);
        Vector3 isleLook = new Vector3(0, 3f, 92f);
        float k = Mathf.SmoothStep(0f, 1f, focus);
        cam.transform.position = Vector3.Lerp(parkPos, islePos, k);
        cam.transform.LookAt(Vector3.Lerp(parkLook, isleLook, k));
    }

    void Go(string scene)
    {
        if (leaving != null) return;
        leaving = scene;
        fade = 0f;
    }

    Rect quitButton;

    bool PointerOverUi() => quitButton.Contains(ui.ToGui(GameInput.PointerPosition));

    void OnGUI()
    {
        ui.Begin();
        float W = ui.W, H = ui.H;
        ui.Label(new Rect(0, 20, W, 140), "佩佩喬治樂園", ui.Huge, new Color(1f, 0.9f, 0.35f));
        ui.Label(new Rect(20, 150, W - 40, 50), "要玩哪一個？點遊樂設施就能進去！", ui.Small, Color.white);

        // 看後面小島的提示
        float a = 0.6f + Mathf.Sin(Time.time * 3f) * 0.3f;
        if (focus < 0.5f)
            ui.Label(new Rect(20, 200, W - 40, 50), "<size=30>↑ 滑鼠移到上面，看看後面的卡皮巴拉發音島！</size>", ui.Small, new Color(1f, 1f, 0.75f, a));
        else
            ui.Label(new Rect(20, H - 90, W - 40, 60), "<size=30>↓ 滑鼠移到下面，回到樂園</size>", ui.Small, new Color(1f, 1f, 0.75f, a));

        // 滑到設施上時顯示提示
        if (hovered != null)
        {
            var p = ui.ToGui(GameInput.PointerPosition);
            string tip = hovered == AppBoot.ZhuyinScene ? "坐摩天輪！" : hovered == AppBoot.MultiplyScene ? "開戰機！" : "去卡皮巴拉發音島！";
            float tw = hovered == AppBoot.IslandScene ? 380 : 260;
            ui.Box(new Rect(p.x + 20, p.y - 70, tw, 60), new Color(0, 0, 0, 0.6f));
            ui.Label(new Rect(p.x + 20, p.y - 70, tw, 60), tip, ui.Small, Color.yellow);
        }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        quitButton = new Rect(W - 230, 30, 200, 80);
        if (GUI.Button(quitButton, "離開", ui.Button)) Application.Quit();
#endif

        if (fade > 0f) ui.Box(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(fade)));
    }

    IEnumerator AutoTest(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        yield return new WaitForSeconds(2f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "park.png"));
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }
}
