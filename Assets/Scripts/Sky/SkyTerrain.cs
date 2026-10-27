using System.Collections.Generic;
using UnityEngine;

// 九九乘法戰機底下會一直捲動的地形：山、河、湖、沙灘、草原、森林、小房子、雲。
// 每一段地形 (和上面的樹、房子) 合成一個網格，用頂點顏色上色，iPad 也跑得動。
public class SkyTerrain : MonoBehaviour
{
    const float ChunkLength = 40f;
    const float HalfWidth = 45f;
    const float Step = 1.5f;
    const float WaterLevel = 0f;

    Material material;
    Transform rig;
    readonly Queue<GameObject> chunks = new Queue<GameObject>();
    float nextZ;
    float seedX, seedZ;

    public void Init(Transform followRig)
    {
        rig = followRig;
        var template = Resources.Load<Material>("TerrainMat");
        material = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
        seedX = Random.Range(0f, 1000f);
        seedZ = Random.Range(0f, 1000f);
        Reset();
    }

    public void Reset()
    {
        while (chunks.Count > 0) Destroy(chunks.Dequeue());
        nextZ = rig.position.z - 30f;
        Update();
    }

    void Update()
    {
        float z = rig.position.z;
        while (nextZ < z + 110f)
        {
            chunks.Enqueue(MakeChunk(nextZ));
            nextZ += ChunkLength;
        }
        while (chunks.Count > 0 && chunks.Peek().transform.position.z + ChunkLength < z - 40f)
            Destroy(chunks.Dequeue());
    }

    // ───────── 地形高度與顏色 ─────────

    float Fbm(float x, float z)
    {
        float sum = 0f, amp = 0.5f, freq = 1f;
        for (int i = 0; i < 4; i++)
        {
            sum += amp * Mathf.PerlinNoise(seedX + x * freq, seedZ + z * freq);
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return sum / 0.9375f;
    }

    float Height(float x, float z)
    {
        float e = Fbm(x * 0.018f, z * 0.018f);
        float h = (e - 0.42f) * 14f;
        // 高的地方變成山 (不能高過戰機)
        if (e > 0.6f) h += (e - 0.6f) * (e - 0.6f) * 110f;
        h = Mathf.Min(h, 8.5f);
        // 蜿蜒的河
        float river = Mathf.Abs(Mathf.PerlinNoise(seedX + 77f + x * 0.012f, seedZ + z * 0.006f) - 0.5f);
        if (river < 0.05f) h = Mathf.Min(h, -1.5f + river * 30f);
        // 細小的起伏
        h += (Mathf.PerlinNoise(seedX + x * 0.2f, seedZ + z * 0.2f) - 0.5f) * 0.5f;
        return h;
    }

    // 每個頂點的顏色，相鄰頂點之間會漸層 (草地 → 沙灘 → 岩石 → 雪)
    Color Ground(float x, float z, float h)
    {
        float detail = Mathf.PerlinNoise(seedX + x * 0.15f, seedZ + z * 0.15f);
        var grass = Color.Lerp(new Color(0.42f, 0.66f, 0.28f), new Color(0.32f, 0.55f, 0.22f), detail);
        if (IsForest(x, z)) grass = Color.Lerp(grass, new Color(0.14f, 0.36f, 0.15f), 0.75f);
        var sand = Color.Lerp(new Color(0.88f, 0.81f, 0.58f), new Color(0.8f, 0.72f, 0.5f), detail);
        var rock = Color.Lerp(new Color(0.5f, 0.46f, 0.42f), new Color(0.38f, 0.35f, 0.32f), detail);
        var snow = new Color(0.95f, 0.96f, 0.98f);
        if (h < 0.2f) return Color.Lerp(new Color(0.55f, 0.52f, 0.4f), sand, Mathf.InverseLerp(-2f, 0.2f, h)); // 水底
        if (h < 1.0f) return Color.Lerp(sand, grass, Mathf.InverseLerp(0.4f, 1.0f, h));
        if (h < 4.5f) return grass;
        if (h < 7.5f) return Color.Lerp(grass, rock, Mathf.InverseLerp(4.5f, 6f, h));
        return Color.Lerp(rock, snow, Mathf.InverseLerp(7.5f, 8.3f, h));
    }

    bool IsForest(float x, float z) => Mathf.PerlinNoise(seedX + 300f + x * 0.04f, seedZ + z * 0.04f) > 0.55f;

    // ───────── 建立一段地形 ─────────

    GameObject MakeChunk(float z0)
    {
        var go = new GameObject("TerrainChunk");
        go.transform.position = new Vector3(0, 0, z0);
        var mb = new MeshBuilder();

        int nx = Mathf.RoundToInt(HalfWidth * 2f / Step), nz = Mathf.RoundToInt(ChunkLength / Step);
        var verts = new Vector3[nx + 1, nz + 1];
        var cols = new Color[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float x = -HalfWidth + i * Step, lz = j * Step;
                float h = Height(x, z0 + lz);
                verts[i, j] = new Vector3(x, h, lz);
                cols[i, j] = Ground(x, z0 + lz, h);
            }

        // 地面：顏色在頂點之間漸層，三角形各自的法線 (有一點低多邊形的立體感)
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                mb.Tri(verts[i, j], verts[i, j + 1], verts[i + 1, j + 1], cols[i, j], cols[i, j + 1], cols[i + 1, j + 1]);
                mb.Tri(verts[i, j], verts[i + 1, j + 1], verts[i + 1, j], cols[i, j], cols[i + 1, j + 1], cols[i + 1, j]);
            }

        // 水面：一整片在高度 0，和地面相交的地方就是彎彎曲曲的岸邊
        const float WaterStep = 6f;
        for (float wx = -HalfWidth; wx < HalfWidth; wx += WaterStep)
            for (float wz = 0; wz < ChunkLength; wz += WaterStep)
            {
                Color W(float px, float pz)
                {
                    float n = Mathf.PerlinNoise(seedX + px * 0.08f, seedZ + (z0 + pz) * 0.08f);
                    return Color.Lerp(new Color(0.2f, 0.5f, 0.72f), new Color(0.14f, 0.38f, 0.62f), n);
                }
                var p00 = new Vector3(wx, WaterLevel, wz);
                var p01 = new Vector3(wx, WaterLevel, wz + WaterStep);
                var p11 = new Vector3(wx + WaterStep, WaterLevel, wz + WaterStep);
                var p10 = new Vector3(wx + WaterStep, WaterLevel, wz);
                mb.Tri(p00, p01, p11, W(wx, wz), W(wx, wz + WaterStep), W(wx + WaterStep, wz + WaterStep));
                mb.Tri(p00, p11, p10, W(wx, wz), W(wx + WaterStep, wz + WaterStep), W(wx + WaterStep, wz));
            }

        // 樹、房子、石頭
        var rng = new System.Random(Mathf.RoundToInt(z0 * 7.3f) + (int)seedX);
        for (int k = 0; k < 140; k++)
        {
            float x = -HalfWidth + (float)rng.NextDouble() * HalfWidth * 2f;
            float lz = (float)rng.NextDouble() * ChunkLength;
            float h = Height(x, z0 + lz);
            if (h < 0.8f || h > 7f) continue;
            var p = new Vector3(x, h, lz);
            if (IsForest(x, z0 + lz))
                Tree(mb, p, 0.9f + (float)rng.NextDouble() * 0.7f, rng);
            else if (h < 3f && rng.NextDouble() < 0.12)
                House(mb, p, rng);
            else if (rng.NextDouble() < 0.25)
                Tree(mb, p, 0.8f + (float)rng.NextDouble() * 0.5f, rng);
            else if (rng.NextDouble() < 0.1)
                mb.Box(p + Vector3.up * 0.3f, new Vector3(1.2f, 0.7f, 1f), new Color(0.55f, 0.53f, 0.5f), (float)rng.NextDouble() * 90f);
        }

        var mesh = mb.ToMesh();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;

        // 飄過的雲 (在戰機下面)
        for (int k = 0; k < 2; k++)
        {
            if (rng.NextDouble() < 0.4) continue;
            var cloud = new GameObject("Cloud").transform;
            cloud.SetParent(go.transform, false);
            cloud.localPosition = new Vector3(-30f + (float)rng.NextDouble() * 60f, 7f + (float)rng.NextDouble() * 2f, (float)rng.NextDouble() * ChunkLength);
            for (int s = 0; s < 4; s++)
                Shapes.Part(PrimitiveType.Sphere, cloud, new Vector3(s * 1.6f - 2.4f, (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() * 1.5f),
                    new Vector3(3f, 1.2f, 2.4f) * (0.8f + (float)rng.NextDouble() * 0.5f), new Color(1f, 1f, 1f));
        }
        return go;
    }

    static void Tree(MeshBuilder mb, Vector3 p, float s, System.Random rng)
    {
        mb.Box(p + Vector3.up * 0.5f * s, new Vector3(0.3f, 1f, 0.3f) * s, new Color(0.45f, 0.3f, 0.18f), 0f);
        var green = new Color(0.12f + (float)rng.NextDouble() * 0.08f, 0.38f + (float)rng.NextDouble() * 0.15f, 0.15f);
        mb.Cone(p + Vector3.up * 0.8f * s, 1.1f * s, 1.8f * s, green);
        mb.Cone(p + Vector3.up * 1.7f * s, 0.8f * s, 1.5f * s, green * 1.1f);
    }

    static void House(MeshBuilder mb, Vector3 p, System.Random rng)
    {
        float rot = (float)rng.NextDouble() * 90f;
        Color[] walls = { new Color(0.95f, 0.92f, 0.85f), new Color(0.9f, 0.85f, 0.7f), new Color(0.85f, 0.88f, 0.95f) };
        Color[] roofs = { new Color(0.75f, 0.25f, 0.2f), new Color(0.3f, 0.4f, 0.7f), new Color(0.45f, 0.3f, 0.25f) };
        mb.Box(p + Vector3.up * 0.7f, new Vector3(2f, 1.4f, 1.6f), walls[rng.Next(walls.Length)], rot);
        mb.Roof(p + Vector3.up * 1.4f, new Vector3(2.2f, 0.9f, 1.8f), roofs[rng.Next(roofs.Length)], rot);
    }
}

// 組合網格的小工具：每個面各自的頂點 (平面陰影)，用頂點顏色上色
public class MeshBuilder
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col) => Tri(a, b, c, col, col, col);

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        colors.Add(ca); colors.Add(cb); colors.Add(cc);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(a, b, c, col);
        Tri(a, c, d, col);
    }

    public void Box(Vector3 center, Vector3 size, Color col, float rotY)
    {
        var r = Quaternion.Euler(0, rotY, 0);
        Vector3 P(float x, float y, float z) => center + r * Vector3.Scale(new Vector3(x, y, z), size * 0.5f);
        var shade = col * 0.85f; shade.a = 1f;
        Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), col);      // 上
        Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), shade); // 前
        Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), shade);     // 後
        Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), shade); // 左
        Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), shade);     // 右
    }

    // 三角形屋頂
    public void Roof(Vector3 bottomCenter, Vector3 size, Color col, float rotY)
    {
        var r = Quaternion.Euler(0, rotY, 0);
        Vector3 P(float x, float y, float z) => bottomCenter + r * new Vector3(x * size.x * 0.5f, y * size.y, z * size.z * 0.5f);
        var shade = col * 0.8f; shade.a = 1f;
        Quad(P(-1, 0, -1), P(-1, 1, 0), P(1, 1, 0), P(1, 0, -1), col);
        Quad(P(1, 0, 1), P(1, 1, 0), P(-1, 1, 0), P(-1, 0, 1), shade);
        Tri(P(-1, 0, 1), P(-1, 1, 0), P(-1, 0, -1), shade);
        Tri(P(1, 0, -1), P(1, 1, 0), P(1, 0, 1), shade);
    }

    // 六角錐 (樹冠)
    public void Cone(Vector3 baseCenter, float radius, float height, Color col)
    {
        const int sides = 6;
        var top = baseCenter + Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
            var p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
            var c = col * (0.85f + 0.15f * Mathf.Cos(a0 - 1f)); c.a = 1f;
            Tri(p1, p0, top, c);
        }
    }

    public Mesh ToMesh()
    {
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
