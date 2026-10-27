// ============================================================================
//  Phonics Island - world building: map, islands and capybaras (all primitives)
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        enum Acc { None, Yuzu, Flower, Glasses, Hat, SunHat, TopHat, Crown, Bandana, Scarf, Cape }

        class Capy
        {
            public Transform root, visual;
            public Material fur;
            public Color goodFur, badFur;
            public GameObject goodBits, badBits;
            public float scale = 1f;
        }

        class MapIsle
        {
            public int index;       // -1 = home island
            public Vector3 pos;
            public float r;
            public Transform root;
        }

        class FindSpot
        {
            public string key;
            public Vector3 pos;
            public bool collected;
            public Transform root;
            public float wrongFlash;
        }

        Camera cam;
        Light sun;
        static Material baseMaterial;
        readonly List<Material> sparkMats = new List<Material>();

        Transform hero;
        Capy heroCapy;

        Transform mapRoot, friendsRoot;
        readonly List<MapIsle> mapIsles = new List<MapIsle>();
        int nearIsland = -2;
        readonly List<System.Action<float>> mapAnims = new List<System.Action<float>>();

        Transform islandRoot, npc, npcMarker, boss;
        Capy npcCapy, bossCapy;
        readonly List<FindSpot> findSpots = new List<FindSpot>();
        int nearSpot = -1;
        readonly List<System.Action<float>> islandAnims = new List<System.Action<float>>();

        readonly Vector3 playerStart = new Vector3(0f, 0f, -4.5f);
        readonly Vector3 npcPos = new Vector3(-0.2f, 0f, 3.8f);
        readonly Vector3 bossPos = new Vector3(2.8f, 0f, 0.8f);
        readonly Vector3 battleSpot = new Vector3(-2.2f, 0f, 0.8f);

        // Fur colors tint the model's texture (white = original colors).
        static readonly Color HeroFur = Color.white;
        static readonly Color BadFur = new Color(0.5f, 0.46f, 0.58f);
        static readonly Color FriendFur = new Color(1f, 0.93f, 0.85f);

        static GameObject capyModel;
        static Texture2D capyTex;
        const float ModelScale = 0.34f;
        // Measured from orthographic views of the model: eyes at (+-0.2, 1.40, 0.86), head top y 1.52.
        static readonly Vector3 HeadOffset = new Vector3(0f, 0.728f, 0.31f);
        const float HeadScale = 0.55f;
        static readonly Color YuzuColor = new Color(1f, 0.62f, 0.1f);

        // ------------------------------------------------------------------
        //  Basics
        // ------------------------------------------------------------------
        static void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static Material MakeMat(Color c)
        {
            // 合併進佩佩喬治樂園後用內建管線：Resources/BaseMat (Standard) 讓 shader 留在建置檔裡
            if (baseMaterial == null) baseMaterial = Resources.Load<Material>("BaseMat");
            if (baseMaterial == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
                Destroy(tmp);
            }
            var m = new Material(baseMaterial);
            SetMatColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            return Prim(type, name, parent, pos, scale, MakeMat(color));
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            // Built-in meshes without colliders (CreatePrimitive would add colliders, which the
            // web build's stripped physics module can't create).
            GameObject g = new GameObject(name);
            g.AddComponent<MeshFilter>().sharedMesh = PrimMesh(type);
            g.AddComponent<MeshRenderer>();
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        GameObject PrimRot(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            GameObject g = Prim(type, name, parent, pos, scale, mat);
            g.transform.localRotation = Quaternion.Euler(euler);
            return g;
        }

        static Transform Group(string name, Transform parent, Vector3 pos)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Vector3 Rim(float angleDeg, float radius, float y = 0f)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }

        static readonly Dictionary<PrimitiveType, Mesh> primMeshes = new Dictionary<PrimitiveType, Mesh>();

        static Mesh PrimMesh(PrimitiveType type)
        {
            Mesh m;
            if (!primMeshes.TryGetValue(type, out m))
            {
                // Copies of the primitive meshes saved by BuildGame (Resources/PrimMeshes).
                m = Resources.Load<Mesh>("PrimMeshes/" + type);
                if (m == null)
                {
                    GameObject tmp = GameObject.CreatePrimitive(type);
                    m = tmp.GetComponent<MeshFilter>().sharedMesh;
                    Destroy(tmp);
                }
                primMeshes[type] = m;
            }
            return m;
        }

        void SetupScene()
        {
            // Hide the template's sample objects (camera, lights and volumes stay).
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == gameObject) continue;
                bool keep = root.GetComponentInChildren<Camera>(true) != null
                            || root.GetComponentInChildren<Light>(true) != null
                            || root.name.Contains("Volume");
                if (!keep) root.SetActive(false);
            }

            cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 300f;
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            foreach (Light l in FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            sparkMats.Add(MakeMat(new Color(1f, 0.9f, 0.2f)));
            sparkMats.Add(MakeMat(new Color(1f, 0.55f, 0.75f)));
            sparkMats.Add(MakeMat(new Color(1f, 0.6f, 0.2f)));
            sparkMats.Add(MakeMat(new Color(0.5f, 0.85f, 1f)));
            sparkMats.Add(MakeMat(Color.white));
        }

        void SetLook(Color sky, Color ambient, Color sunColor, float sunIntensity)
        {
            cam.backgroundColor = sky;
            RenderSettings.ambientLight = ambient;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }

        void RunAnims(float dt)
        {
            List<System.Action<float>> list = mode == Mode.Map ? mapAnims : islandAnims;
            for (int i = 0; i < list.Count; i++) list[i](dt);
        }

        // ------------------------------------------------------------------
        //  Capybaras (facing +Z)
        // ------------------------------------------------------------------
        Capy BuildCapy(string name, Transform parent, Vector3 pos, Color fur, float scale, bool bad, params Acc[] accs)
        {
            return BuildCapy(name, parent, pos, fur, scale, bad, Color.white, accs);
        }

        Capy BuildCapy(string name, Transform parent, Vector3 pos, Color fur, float scale, bool bad, Color accColor, params Acc[] accs)
        {
            var c = new Capy { goodFur = fur, badFur = BadFur, scale = scale };
            c.root = new GameObject(name).transform;
            c.root.SetParent(parent, false);
            c.root.localPosition = pos;
            c.visual = Group("Visual", c.root, Vector3.zero);
            Transform b = Group("Body", c.visual, Vector3.zero);
            b.localScale = Vector3.one * scale;

            // The licensed model (see Resources/Models/LICENSE.txt), tinted per character.
            if (capyModel == null) capyModel = Resources.Load<GameObject>("Models/Capybara");
            if (capyTex == null) capyTex = Resources.Load<Texture2D>("Models/Capybara_BaseColor");
            c.fur = MakeMat(bad ? BadFur : fur);
            if (capyTex != null)
            {
                c.fur.SetTexture("_BaseMap", capyTex);
                c.fur.SetTexture("_MainTex", capyTex);
            }
            GameObject model = Instantiate(capyModel, b, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * ModelScale;
            foreach (Renderer rd in model.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = c.fur;

            // Hats and faces were designed on a smaller head; this transform maps them onto the model's head.
            Transform head = Group("Head", b, HeadOffset);
            head.localScale = Vector3.one * HeadScale;

            c.goodBits = Group("GoodFace", head, Vector3.zero).gameObject;

            // Bad guy face: a bandit mask band around the eyes (which sit on the sides of the head) and angry eyebrows
            c.badBits = Group("BadFace", b, Vector3.zero).gameObject;
            Material black = MakeMat(new Color(0.08f, 0.07f, 0.1f));
            Material white = MakeMat(Color.white);
            Material red = MakeMat(new Color(0.75f, 0.1f, 0.15f));
            Prim(PrimitiveType.Cube, "Mask", c.badBits.transform, new Vector3(0f, 1.4f, 0.87f), new Vector3(0.5f, 0.1f, 0.3f), black);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Sphere, "Eye", c.badBits.transform, new Vector3(s * 0.25f, 1.405f, 0.9f), Vector3.one * 0.09f, white);
                Prim(PrimitiveType.Sphere, "Pupil", c.badBits.transform, new Vector3(s * 0.285f, 1.4f, 0.92f), Vector3.one * 0.045f, red);
                PrimRot(PrimitiveType.Cube, "Brow", c.badBits.transform, new Vector3(s * 0.25f, 1.48f, 0.9f),
                    new Vector3(0.04f, 0.03f, 0.17f), new Vector3(22f, 0f, 0f), black);
            }

            foreach (Acc a in accs) AddAccessory(a == Acc.Cape || a == Acc.Scarf || a == Acc.Glasses ? b : head, a, accColor);
            SetBad(c, bad);
            return c;
        }

        void SetBad(Capy c, bool bad)
        {
            c.goodBits.SetActive(!bad);
            c.badBits.SetActive(bad);
            SetMatColor(c.fur, bad ? c.badFur : c.goodFur);
        }

        void AddAccessory(Transform b, Acc a, Color col)
        {
            switch (a)
            {
                case Acc.Yuzu:
                {
                    Prim(PrimitiveType.Sphere, "Yuzu", b, new Vector3(0f, 1.52f, 0.62f), Vector3.one * 0.32f, YuzuColor);
                    PrimRot(PrimitiveType.Sphere, "YuzuLeaf", b, new Vector3(0.07f, 1.69f, 0.62f), new Vector3(0.16f, 0.04f, 0.09f),
                        new Vector3(0f, 0f, 20f), MakeMat(new Color(0.3f, 0.7f, 0.3f)));
                    break;
                }
                case Acc.Flower:
                {
                    Prim(PrimitiveType.Sphere, "Petals", b, new Vector3(0.25f, 1.45f, 0.62f), new Vector3(0.22f, 0.22f, 0.1f), col);
                    Prim(PrimitiveType.Sphere, "Center", b, new Vector3(0.25f, 1.45f, 0.67f), Vector3.one * 0.09f, new Color(1f, 0.9f, 0.3f));
                    break;
                }
                case Acc.Glasses:
                {
                    Material m = MakeMat(col);
                    // Body coordinates: capybara eyes are on the sides of the head.
                    for (int s = -1; s <= 1; s += 2)
                        PrimRot(PrimitiveType.Cylinder, "Lens", b, new Vector3(s * 0.235f, 1.4f, 0.87f), new Vector3(0.15f, 0.01f, 0.15f), new Vector3(0f, 0f, 90f), m);
                    Prim(PrimitiveType.Cube, "Bridge", b, new Vector3(0f, 1.47f, 0.9f), new Vector3(0.46f, 0.02f, 0.02f), m);
                    break;
                }
                case Acc.Hat:
                {
                    Material m = MakeMat(col);
                    Prim(PrimitiveType.Cylinder, "Brim", b, new Vector3(0f, 1.44f, 0.65f), new Vector3(0.62f, 0.03f, 0.62f), m);
                    Prim(PrimitiveType.Cylinder, "Top", b, new Vector3(0f, 1.56f, 0.65f), new Vector3(0.4f, 0.12f, 0.4f), m);
                    break;
                }
                case Acc.SunHat:
                {
                    Material m = MakeMat(col);
                    Prim(PrimitiveType.Cylinder, "Brim", b, new Vector3(0f, 1.44f, 0.65f), new Vector3(1.0f, 0.02f, 1.0f), m);
                    Prim(PrimitiveType.Sphere, "Top", b, new Vector3(0f, 1.5f, 0.65f), new Vector3(0.45f, 0.3f, 0.45f), m);
                    Prim(PrimitiveType.Cylinder, "Band", b, new Vector3(0f, 1.48f, 0.65f), new Vector3(0.47f, 0.03f, 0.47f), new Color(1f, 0.45f, 0.5f));
                    break;
                }
                case Acc.TopHat:
                {
                    Material m = MakeMat(col);
                    Prim(PrimitiveType.Cylinder, "Brim", b, new Vector3(0f, 1.44f, 0.65f), new Vector3(0.58f, 0.02f, 0.58f), m);
                    Prim(PrimitiveType.Cylinder, "Top", b, new Vector3(0f, 1.68f, 0.65f), new Vector3(0.36f, 0.24f, 0.36f), m);
                    Prim(PrimitiveType.Cylinder, "Band", b, new Vector3(0f, 1.5f, 0.65f), new Vector3(0.37f, 0.04f, 0.37f), new Color(0.9f, 0.3f, 0.3f));
                    break;
                }
                case Acc.Crown:
                {
                    Material gold = MakeMat(col);
                    Prim(PrimitiveType.Cylinder, "Ring", b, new Vector3(0f, 1.5f, 0.65f), new Vector3(0.5f, 0.09f, 0.5f), gold);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p = new Vector3(0f, 1.64f, 0.65f) + Rim(i * 60f, 0.21f);
                        PrimRot(PrimitiveType.Cube, "Spike", b, p, Vector3.one * 0.11f, new Vector3(45f, i * 60f, 45f), gold);
                    }
                    Prim(PrimitiveType.Sphere, "Gem", b, new Vector3(0f, 1.52f, 0.91f), Vector3.one * 0.1f, new Color(0.9f, 0.15f, 0.3f));
                    break;
                }
                case Acc.Bandana:
                {
                    Material m = MakeMat(col);
                    PrimRot(PrimitiveType.Cylinder, "Band", b, new Vector3(0f, 1.36f, 0.68f), new Vector3(0.76f, 0.07f, 0.88f), new Vector3(-12f, 0f, 0f), m);
                    Prim(PrimitiveType.Sphere, "Knot", b, new Vector3(0f, 1.36f, 0.24f), new Vector3(0.18f, 0.14f, 0.14f), m);
                    break;
                }
                case Acc.Scarf:
                {
                    Material m = MakeMat(col);
                    PrimRot(PrimitiveType.Cylinder, "Scarf", b, new Vector3(0f, 1.18f, 0.45f), new Vector3(0.78f, 0.07f, 0.78f), new Vector3(-25f, 0f, 0f), m);
                    PrimRot(PrimitiveType.Cube, "Tail", b, new Vector3(0.3f, 1.04f, 0.35f), new Vector3(0.14f, 0.32f, 0.05f), new Vector3(-10f, 0f, 15f), m);
                    break;
                }
                case Acc.Cape:
                {
                    PrimRot(PrimitiveType.Cube, "Cape", b, new Vector3(0f, 1.41f, -0.55f), new Vector3(0.82f, 0.04f, 1.0f), new Vector3(-12f, 0f, 0f), MakeMat(col));
                    break;
                }
            }
        }

        void BuildHero()
        {
            heroCapy = BuildCapy("Hero", null, Vector3.zero, HeroFur, 1f, false, new Color(0.95f, 0.25f, 0.25f), Acc.Yuzu, Acc.Scarf);
            hero = heroCapy.root;
        }

        // ------------------------------------------------------------------
        //  World map: home island with a hot spring + six lesson islands
        // ------------------------------------------------------------------
        static readonly float[] MapAngles = { 215f, 160f, 115f, 65f, 20f, -35f };
        const float MapRingRadius = 17f;

        void ApplyMapLook()
        {
            SetLook(new Color(0.6f, 0.84f, 1f), new Color(0.72f, 0.78f, 0.9f), new Color(1f, 0.96f, 0.88f), 1.15f);
        }

        void BuildMap()
        {
            mapRoot = new GameObject("Map").transform;
            Prim(PrimitiveType.Plane, "Sea", mapRoot, new Vector3(0f, -0.35f, 0f), new Vector3(30f, 1f, 30f), new Color(0.3f, 0.64f, 0.95f));

            // Home island
            var home = new MapIsle { index = -1, pos = Vector3.zero, r = 4.6f };
            home.root = Group("Home", mapRoot, Vector3.zero);
            IslandDisk(home.root, 5.4f, new Color(0.98f, 0.9f, 0.62f), new Color(0.55f, 0.86f, 0.45f), 0.8f);
            MakePalm(home.root, new Vector3(-3.2f, 0f, 2.2f), 1f, null);
            MakePalm(home.root, new Vector3(3.4f, 0f, 1.6f), 1f, null);
            BuildHotSpring(home.root, new Vector3(0f, 0f, 0.8f));
            mapIsles.Add(home);

            for (int i = 0; i < islands.Count; i++)
            {
                Vector3 p = Rim(MapAngles[i], MapRingRadius);
                var mi = new MapIsle { index = i, pos = p, r = 4.2f };
                mi.root = Group("Isle_" + i, mapRoot, p);
                BuildMapIsle(mi.root, islands[i].theme);
                mapIsles.Add(mi);
            }

            // Buoys showing the way from island to island
            Material red = MakeMat(new Color(0.95f, 0.3f, 0.3f));
            Material white = MakeMat(Color.white);
            Vector3 prev = Vector3.zero;
            float prevR = 5.4f;
            for (int i = 0; i < islands.Count; i++)
            {
                Vector3 next = Rim(MapAngles[i], MapRingRadius);
                Vector3 d = next - prev;
                float len = d.magnitude - prevR - 4.6f;
                Vector3 start = prev + d.normalized * (prevR + 0.8f);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / 2.6f));
                for (int k = 0; k <= n; k++)
                {
                    Vector3 bp = start + d.normalized * (len * k / n);
                    Prim(PrimitiveType.Sphere, "Buoy", mapRoot, bp + Vector3.up * -0.25f, Vector3.one * 0.32f, k % 2 == 0 ? red : white);
                }
                prev = next;
                prevR = 4.6f;
            }

            for (int i = 0; i < 6; i++)
            {
                Transform cl = MakeCloud(mapRoot, new Vector3(-36f + i * 13f, 12f + (i % 2) * 2f, 14f + (i % 3) * 6f));
                mapAnims.Add(dt => DriftCloud(cl, dt, 40f));
            }
        }

        void IslandDisk(Transform parent, float r, Color sand, Color grass, float grassFrac)
        {
            Prim(PrimitiveType.Cylinder, "Shallow", parent, new Vector3(0f, -0.34f, 0f), new Vector3(r * 2.5f, 0.02f, r * 2.5f), new Color(0.55f, 0.88f, 0.95f));
            Prim(PrimitiveType.Cylinder, "Sand", parent, new Vector3(0f, -0.25f, 0f), new Vector3(r * 2f, 0.25f, r * 2f), sand);
            if (grassFrac > 0f)
                Prim(PrimitiveType.Cylinder, "Grass", parent, new Vector3(0f, -0.23f, 0f), new Vector3(r * 2f * grassFrac, 0.25f, r * 2f * grassFrac), grass);
        }

        void BuildHotSpring(Transform parent, Vector3 c)
        {
            Transform hs = Group("HotSpring", parent, c);
            Prim(PrimitiveType.Cylinder, "Water", hs, new Vector3(0f, -0.02f, 0f), new Vector3(3.6f, 0.05f, 3.6f), new Color(0.55f, 0.88f, 0.86f));
            Material rock = MakeMat(new Color(0.62f, 0.6f, 0.62f));
            for (int i = 0; i < 14; i++)
            {
                float a = i * 360f / 14f;
                Prim(PrimitiveType.Sphere, "Rock", hs, Rim(a, 1.9f, 0.05f), new Vector3(0.7f, 0.45f, 0.6f), rock);
            }
            Material puff = MakeMat(new Color(1f, 1f, 1f));
            for (int i = 0; i < 6; i++)
            {
                Transform s = Prim(PrimitiveType.Sphere, "Steam", hs, Vector3.zero, Vector3.one * 0.4f, puff).transform;
                Vector3 basePos = Rim(i * 60f + 20f, 0.6f + (i % 3) * 0.35f, 0.2f);
                float phase = i * 0.45f;
                mapAnims.Add(dt =>
                {
                    float u = ((time * 0.4f + phase) % 1f);
                    s.localPosition = basePos + Vector3.up * (u * 1.8f) + new Vector3(Mathf.Sin(time + phase) * 0.15f, 0f, 0f);
                    s.localScale = Vector3.one * (0.25f + u * 0.45f) * (1f - u);
                });
            }
            friendsRoot = Group("Friends", hs, Vector3.zero);
        }

        // Befriended bosses relax in the hot spring with a yuzu on their head.
        void RefreshFriends()
        {
            for (int i = friendsRoot.childCount - 1; i >= 0; i--) Destroy(friendsRoot.GetChild(i).gameObject);
            int n = 0;
            for (int i = 0; i < islands.Count; i++)
            {
                if (!cleared[i]) continue;
                IslandDef d = islands[i];
                float a = 90f + n * 60f;
                Vector3 p = Rim(a, 0.95f, -0.42f);
                Capy f = BuildCapy("Friend_" + d.bossName, friendsRoot, p, FriendFur, 0.62f, false, d.bossAccColor, d.bossAcc, Acc.Yuzu);
                f.root.localRotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z).normalized * -1f + Vector3.back * 0.8f);
                Transform v = f.visual;
                float ph = i * 0.9f;
                mapAnims.Add(dt => { if (v != null) v.localPosition = new Vector3(0f, Mathf.Sin(time * 1.5f + ph) * 0.04f, 0f); });
                n++;
            }
        }

        void RefreshMap()
        {
            RefreshFriends();
        }

        void BuildMapIsle(Transform root, Theme theme)
        {
            switch (theme)
            {
                case Theme.Forest:
                    IslandDisk(root, 4.4f, new Color(0.93f, 0.85f, 0.6f), new Color(0.38f, 0.72f, 0.33f), 0.85f);
                    MakeRoundTree(root, new Vector3(-1.5f, 0f, 1f), 1f);
                    MakePine(root, new Vector3(1.2f, 0f, 1.5f), 1.1f);
                    MakeRoundTree(root, new Vector3(0.6f, 0f, -1.4f), 0.8f);
                    MakePine(root, new Vector3(-1.8f, 0f, -1.3f), 0.8f);
                    break;
                case Theme.Cave:
                    IslandDisk(root, 4.4f, new Color(0.62f, 0.56f, 0.52f), new Color(0.45f, 0.42f, 0.45f), 0.85f);
                    Prim(PrimitiveType.Sphere, "Mountain", root, new Vector3(0f, 0.6f, 0.6f), new Vector3(5.2f, 4f, 4.2f), new Color(0.48f, 0.45f, 0.5f));
                    Prim(PrimitiveType.Sphere, "Mouth", root, new Vector3(0f, 0.5f, -1.25f), new Vector3(1.8f, 2.0f, 0.8f), new Color(0.1f, 0.08f, 0.12f));
                    MakeCrystal(root, new Vector3(1.9f, 0f, -1.6f), new Color(0.5f, 0.9f, 1f), 0.8f);
                    MakeCrystal(root, new Vector3(-2f, 0f, -1.3f), new Color(1f, 0.5f, 0.85f), 0.7f);
                    break;
                case Theme.Beach:
                    IslandDisk(root, 4.4f, new Color(1f, 0.92f, 0.65f), Color.white, 0f);
                    MakePalm(root, new Vector3(-1.5f, 0f, 1f), 0.9f, null);
                    MakeUmbrella(root, new Vector3(1.2f, 0f, 0.2f), new Color(1f, 0.4f, 0.4f));
                    MakeSandCastle(root, new Vector3(-0.2f, 0f, -1.8f), 0.7f);
                    break;
                case Theme.Valley:
                    IslandDisk(root, 4.4f, new Color(0.9f, 0.85f, 0.6f), new Color(0.55f, 0.82f, 0.38f), 0.85f);
                    MakeMountain(root, new Vector3(-1f, 0f, 1f), 1.1f);
                    MakeMountain(root, new Vector3(1.5f, 0f, 0.6f), 0.8f);
                    MakeFlowerPatch(root, new Vector3(0.2f, 0f, -1.8f), 1.2f, 8);
                    break;
                case Theme.Village:
                    IslandDisk(root, 4.4f, new Color(0.93f, 0.86f, 0.62f), new Color(0.6f, 0.84f, 0.42f), 0.85f);
                    MakeHouse(root, new Vector3(-1.3f, 0f, 0.8f), 0f, new Color(1f, 0.9f, 0.7f), new Color(0.85f, 0.3f, 0.3f), 0.8f);
                    MakeHouse(root, new Vector3(1.4f, 0f, 1.0f), 0f, new Color(0.8f, 0.9f, 1f), new Color(0.3f, 0.5f, 0.85f), 0.7f);
                    MakeHouse(root, new Vector3(0.2f, 0f, -1.2f), 0f, new Color(1f, 0.85f, 0.85f), new Color(0.5f, 0.75f, 0.4f), 0.6f);
                    break;
                case Theme.Castle:
                    IslandDisk(root, 4.4f, new Color(0.7f, 0.66f, 0.7f), new Color(0.5f, 0.48f, 0.55f), 0.85f);
                    MakeCastle(root, new Vector3(0f, 0f, 0.4f), 0.55f);
                    break;
            }
        }

        // ------------------------------------------------------------------
        //  Lesson islands
        // ------------------------------------------------------------------
        void ClearIsland()
        {
            islandAnims.Clear();
            findSpots.Clear();
            if (islandRoot != null) Destroy(islandRoot.gameObject);
            islandRoot = null;
        }

        void BuildIsland(IslandDef d)
        {
            ClearIsland();
            obstacles.Clear();
            islandRoot = new GameObject("Island_" + d.nameEn).transform;
            Transform r = islandRoot;

            Color sea = new Color(0.3f, 0.66f, 0.95f);
            switch (d.theme)
            {
                case Theme.Cave:
                    SetLook(new Color(0.14f, 0.12f, 0.22f), new Color(0.38f, 0.36f, 0.5f), new Color(0.7f, 0.75f, 1f), 0.55f);
                    sea = new Color(0.12f, 0.2f, 0.4f);
                    break;
                case Theme.Castle:
                    SetLook(new Color(0.5f, 0.4f, 0.66f), new Color(0.6f, 0.55f, 0.72f), new Color(1f, 0.85f, 0.8f), 0.95f);
                    sea = new Color(0.3f, 0.35f, 0.65f);
                    break;
                case Theme.Beach:
                    SetLook(new Color(0.55f, 0.86f, 1f), new Color(0.75f, 0.8f, 0.9f), new Color(1f, 0.97f, 0.88f), 1.25f);
                    break;
                default:
                    SetLook(new Color(0.62f, 0.82f, 0.98f), new Color(0.72f, 0.78f, 0.9f), new Color(1f, 0.96f, 0.88f), 1.15f);
                    break;
            }
            Prim(PrimitiveType.Plane, "Sea", r, new Vector3(0f, -0.35f, 0f), new Vector3(20f, 1f, 20f), sea);

            switch (d.theme)
            {
                case Theme.Forest: BuildForest(r); break;
                case Theme.Cave: BuildCave(r); break;
                case Theme.Beach: BuildBeach(r, d); break;
                case Theme.Valley: BuildValley(r); break;
                case Theme.Village: BuildVillage(r); break;
                case Theme.Castle: BuildCastleIsland(r); break;
            }

            if (d.theme != Theme.Cave)
            {
                for (int i = 0; i < 5; i++)
                {
                    Transform cl = MakeCloud(r, new Vector3(-18f + i * 9f, 11f + (i % 2) * 2f, 8f + (i % 3) * 4f));
                    islandAnims.Add(dt => DriftCloud(cl, dt, 26f));
                }
            }

            // Helper NPC
            npcCapy = BuildCapy("NPC_" + d.npcName, r, npcPos, d.npcFur, 1f, false, d.npcAccColor, d.npcAcc);
            npc = npcCapy.root;
            npcMarker = Group("Marker", npc, new Vector3(0f, 2.4f, 0f));
            Material yellow = MakeMat(new Color(1f, 0.85f, 0.2f));
            Prim(PrimitiveType.Capsule, "Bar", npcMarker, new Vector3(0f, 0.18f, 0f), new Vector3(0.18f, 0.22f, 0.18f), yellow);
            Prim(PrimitiveType.Sphere, "Dot", npcMarker, new Vector3(0f, -0.2f, 0f), Vector3.one * 0.2f, yellow);
            obstacles.Add(new Obstacle { pos = npcPos, r = 0.7f });
            islandAnims.Add(dt =>
            {
                bool want = state == State.Explore;
                if (npcMarker.gameObject.activeSelf != want) npcMarker.gameObject.SetActive(want);
                npcMarker.localPosition = new Vector3(0f, 2.4f + Mathf.Sin(time * 3f) * 0.12f, 0f);
                npcMarker.Rotate(0f, 90f * dt, 0f);
                npcCapy.visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 2f)) * 0.05f, 0f);
            });

            // Bad capybara
            var accs = new List<Acc> { d.bossAcc };
            if (d.bossAcc == Acc.Crown) accs.Add(Acc.Cape);
            bossCapy = BuildCapy("Boss_" + d.bossName, r, bossPos, FriendFur, d.bossScale, true, d.bossAccColor, accs.ToArray());
            if (d.bossAcc == Acc.Crown) bossCapy.visual.Find("Body/Cape").GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.55f, 0.2f, 0.65f));
            boss = bossCapy.root;
            obstacles.Add(new Obstacle { pos = bossPos, r = 1.0f * d.bossScale });
            islandAnims.Add(dt =>
            {
                if (bossIdle) bossCapy.visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 2.4f)) * 0.12f, 0f);
            });
        }

        bool AvoidCenter(Vector3 p)
        {
            Vector3[] keep = { npcPos, bossPos, battleSpot, playerStart, Vector3.zero };
            foreach (Vector3 k in keep)
            {
                if ((p - k).magnitude < 2.1f) return false;
            }
            return true;
        }

        void ScatterFlowers(Transform r, int count, float minR, float maxR)
        {
            Color[] cols = { new Color(1f, 0.55f, 0.7f), new Color(1f, 0.9f, 0.35f), Color.white, new Color(0.7f, 0.6f, 1f) };
            var mats = new Material[cols.Length];
            for (int i = 0; i < cols.Length; i++) mats[i] = MakeMat(cols[i]);
            for (int i = 0; i < count; i++)
            {
                float ang = i * 137.5f;
                float rad = minR + (i * 0.37f) % (maxR - minR);
                Vector3 fp = Rim(ang, rad, 0.08f);
                if (!AvoidCenter(fp)) continue;
                Prim(PrimitiveType.Sphere, "Flower", r, fp, Vector3.one * 0.2f, mats[i % mats.Length]);
            }
        }

        void BuildForest(Transform r)
        {
            IslandDisk(r, 7.5f, new Color(0.95f, 0.87f, 0.6f), new Color(0.4f, 0.74f, 0.35f), 0.84f);
            float[] treeAngles = { 5f, 35f, 62f, 118f, 145f, 172f, 200f, 335f, 315f };
            for (int i = 0; i < treeAngles.Length; i++)
            {
                Vector3 p = Rim(treeAngles[i], 5.9f + (i % 2) * 0.5f);
                if (i % 2 == 0) MakeRoundTree(r, p, 1.1f + (i % 3) * 0.15f);
                else MakePine(r, p, 1.2f + (i % 3) * 0.15f);
                obstacles.Add(new Obstacle { pos = p, r = 0.5f });
            }
            MakeMushroom(r, Rim(240f, 4.6f), 1f);
            MakeMushroom(r, Rim(250f, 5.4f), 0.7f);
            MakeMushroom(r, Rim(300f, 5.1f), 0.9f);
            // Toy letter blocks
            Color[] blockCols = { new Color(1f, 0.45f, 0.45f), new Color(0.45f, 0.7f, 1f), new Color(1f, 0.85f, 0.3f), new Color(0.5f, 0.85f, 0.5f), new Color(0.8f, 0.55f, 1f) };
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = Rim(205f + i * 8f, 4.7f + (i % 2) * 0.4f, 0.2f);
                PrimRot(PrimitiveType.Cube, "Block", r, p, Vector3.one * 0.4f, new Vector3(0f, i * 23f, 0f), MakeMat(blockCols[i]));
            }
            ScatterFlowers(r, 18, 2.2f, 5.6f);
            AddButterflies(r, 4);
        }

        void BuildCave(Transform r)
        {
            IslandDisk(r, 7.5f, new Color(0.5f, 0.45f, 0.42f), new Color(0.36f, 0.33f, 0.36f), 0.84f);
            Material rock = MakeMat(new Color(0.42f, 0.38f, 0.44f));
            Material rock2 = MakeMat(new Color(0.34f, 0.31f, 0.36f));
            for (int i = 0; i < 12; i++)
            {
                float a = -30f + i * 21f;
                float h = 2.2f + (i * 0.7f) % 2.2f;
                Vector3 p = Rim(a, 6.6f + (i % 2) * 0.4f, h * 0.5f);
                Prim(PrimitiveType.Capsule, "Pillar", r, p, new Vector3(1.6f, h * 0.6f, 1.4f), i % 2 == 0 ? rock : rock2);
                obstacles.Add(new Obstacle { pos = new Vector3(p.x, 0f, p.z), r = 0.8f });
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = Rim(235f + i * 14f, 5.6f + (i % 2) * 0.6f, 0.25f);
                Prim(PrimitiveType.Capsule, "Stalagmite", r, p, new Vector3(0.35f, 0.35f + (i % 3) * 0.12f, 0.35f), rock);
            }
            Color[] crystal = { new Color(0.5f, 0.9f, 1f), new Color(1f, 0.5f, 0.85f), new Color(0.7f, 0.6f, 1f) };
            float[] cAngles = { 20f, 75f, 150f, 195f, 330f };
            for (int i = 0; i < cAngles.Length; i++)
            {
                Transform c = MakeCrystal(r, Rim(cAngles[i], 5.2f), crystal[i % 3], 1f);
                float ph = i;
                islandAnims.Add(dt => c.localScale = Vector3.one * (1f + Mathf.Sin(time * 2f + ph) * 0.05f));
            }
            float[] torchAngles = { 50f, 130f, 345f };
            foreach (float ta in torchAngles) MakeTorch(r, Rim(ta, 5.4f));
            // Fireflies
            Material glow = MakeMat(new Color(1f, 0.95f, 0.5f));
            for (int i = 0; i < 10; i++)
            {
                Transform f = Prim(PrimitiveType.Sphere, "Firefly", r, Vector3.zero, Vector3.one * 0.08f, glow).transform;
                Vector3 c0 = Rim(i * 36f, 2.5f + (i % 3), 1.2f + (i % 2));
                float ph = i * 1.3f;
                islandAnims.Add(dt => f.localPosition = c0 + new Vector3(Mathf.Sin(time * 0.7f + ph), Mathf.Sin(time * 1.3f + ph) * 0.4f, Mathf.Cos(time * 0.6f + ph)));
            }
        }

        void BuildBeach(Transform r, IslandDef d)
        {
            IslandDisk(r, 7.6f, new Color(1f, 0.92f, 0.65f), new Color(0.6f, 0.85f, 0.45f), 0.35f);
            Vector3[] palms = { Rim(30f, 6.3f), Rim(140f, 6.2f), Rim(175f, 6.0f), Rim(345f, 6.1f) };
            foreach (Vector3 p in palms) MakePalm(r, p, 1.1f, obstacles);
            MakeUmbrella(r, Rim(65f, 5.8f), new Color(1f, 0.4f, 0.4f));
            MakeUmbrella(r, Rim(110f, 6.0f), new Color(0.3f, 0.7f, 1f));
            MakeSandCastle(r, Rim(200f, 5.9f), 1f);
            obstacles.Add(new Obstacle { pos = Rim(200f, 5.9f), r = 0.8f });
            // Beach ball
            Transform ball = Group("Ball", r, Rim(255f, 5.8f, 0.3f));
            Prim(PrimitiveType.Sphere, "Ball", ball, Vector3.zero, Vector3.one * 0.6f, new Color(1f, 1f, 1f));
            PrimRot(PrimitiveType.Cylinder, "Stripe", ball, Vector3.zero, new Vector3(0.61f, 0.08f, 0.61f), new Vector3(0f, 0f, 90f), MakeMat(new Color(1f, 0.35f, 0.35f)));
            PrimRot(PrimitiveType.Cylinder, "Stripe", ball, Vector3.zero, new Vector3(0.61f, 0.08f, 0.61f), new Vector3(90f, 0f, 0f), MakeMat(new Color(0.3f, 0.6f, 1f)));
            islandAnims.Add(dt => ball.localPosition = Rim(255f, 5.8f, 0.3f + Mathf.Abs(Mathf.Sin(time * 2.5f)) * 0.4f));
            // A little crab walking sideways
            Transform crab = Group("CrabWalker", r, Rim(290f, 6.4f));
            Material crabMat = MakeMat(new Color(1f, 0.35f, 0.3f));
            Prim(PrimitiveType.Sphere, "Shell", crab, new Vector3(0f, 0.18f, 0f), new Vector3(0.5f, 0.25f, 0.38f), crabMat);
            Prim(PrimitiveType.Sphere, "ClawL", crab, new Vector3(-0.32f, 0.2f, 0.18f), Vector3.one * 0.16f, crabMat);
            Prim(PrimitiveType.Sphere, "ClawR", crab, new Vector3(0.32f, 0.2f, 0.18f), Vector3.one * 0.16f, crabMat);
            islandAnims.Add(dt => crab.localPosition = Rim(290f + Mathf.Sin(time * 0.6f) * 18f, 6.4f));

            // Hidden things to find
            Vector3[] spots =
            {
                new Vector3(-5f, 0f, -1.5f), new Vector3(-4.2f, 0f, 3.4f), new Vector3(-1.6f, 0f, -2.2f),
                new Vector3(2.1f, 0f, -3.1f), new Vector3(4.9f, 0f, -1.8f), new Vector3(5.1f, 0f, 2.9f), new Vector3(1.8f, 0f, 5.0f)
            };
            Material mound = MakeMat(new Color(0.95f, 0.84f, 0.55f));
            Material pole = MakeMat(new Color(0.6f, 0.42f, 0.25f));
            Material flag = MakeMat(new Color(1f, 0.4f, 0.5f));
            for (int i = 0; i < d.findItems.Length && i < spots.Length; i++)
            {
                Transform s = Group("Spot_" + d.findItems[i], r, spots[i]);
                Prim(PrimitiveType.Sphere, "Mound", s, new Vector3(0f, 0f, 0f), new Vector3(1.3f, 0.45f, 1.3f), mound);
                Prim(PrimitiveType.Cylinder, "Pole", s, new Vector3(0.45f, 0.5f, 0.2f), new Vector3(0.05f, 0.5f, 0.05f), pole);
                Prim(PrimitiveType.Cube, "Flag", s, new Vector3(0.62f, 0.85f, 0.2f), new Vector3(0.3f, 0.2f, 0.02f), flag);
                findSpots.Add(new FindSpot { key = d.findItems[i], pos = spots[i], root = s });
            }
        }

        void BuildValley(Transform r)
        {
            IslandDisk(r, 7.5f, new Color(0.92f, 0.86f, 0.6f), new Color(0.55f, 0.84f, 0.4f), 0.84f);
            float[] mAngles = { 25f, 60f, 95f, 130f, 160f };
            for (int i = 0; i < mAngles.Length; i++)
            {
                Vector3 p = Rim(mAngles[i], 7.2f + (i % 2) * 0.8f);
                MakeMountain(r, p, 1.8f + (i % 2) * 0.6f);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Rim(185f + i * 45f, 6.2f, -0.6f);
                if (i == 2) continue;
                Prim(PrimitiveType.Sphere, "Hill", r, p, new Vector3(3f, 2f, 2.6f), new Color(0.45f, 0.75f, 0.35f));
                obstacles.Add(new Obstacle { pos = new Vector3(p.x, 0f, p.z), r = 1.1f });
            }
            // A little stream with a bridge
            Prim(PrimitiveType.Cube, "Stream", r, new Vector3(-4.6f, -0.08f, 1f), new Vector3(1.0f, 0.04f, 9f), new Color(0.45f, 0.75f, 1f));
            Material wood = MakeMat(new Color(0.65f, 0.45f, 0.28f));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "Plank", r, new Vector3(-4.6f, 0.02f, -1.2f + i * 0.28f), new Vector3(1.5f, 0.06f, 0.22f), wood);
            MakeFlowerPatch(r, Rim(300f, 5f), 1.2f, 10);
            MakeFlowerPatch(r, Rim(20f, 4.8f), 1f, 8);
            ScatterFlowers(r, 14, 2.2f, 5.4f);
            AddButterflies(r, 5);
        }

        void BuildVillage(Transform r)
        {
            IslandDisk(r, 7.5f, new Color(0.93f, 0.86f, 0.62f), new Color(0.6f, 0.84f, 0.42f), 0.84f);
            Prim(PrimitiveType.Cylinder, "Plaza", r, new Vector3(0f, -0.09f, 0.5f), new Vector3(6f, 0.02f, 6f), new Color(0.88f, 0.8f, 0.62f));
            float[] hAngles = { 15f, 55f, 125f, 165f };
            Color[] walls = { new Color(1f, 0.9f, 0.7f), new Color(0.8f, 0.9f, 1f), new Color(1f, 0.85f, 0.85f), new Color(0.9f, 1f, 0.85f) };
            Color[] roofs = { new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.85f), new Color(0.5f, 0.75f, 0.4f), new Color(0.75f, 0.45f, 0.85f) };
            for (int i = 0; i < hAngles.Length; i++)
            {
                Vector3 p = Rim(hAngles[i], 6.0f);
                float face = 270f - hAngles[i];
                Transform h = MakeHouse(r, p, face, walls[i], roofs[i], 1.15f);
                obstacles.Add(new Obstacle { pos = p, r = 1.2f });
                // Chimney smoke
                Material smoke = MakeMat(new Color(0.92f, 0.92f, 0.95f));
                for (int k = 0; k < 3; k++)
                {
                    Transform s = Prim(PrimitiveType.Sphere, "Smoke", h, Vector3.zero, Vector3.one * 0.3f, smoke).transform;
                    float ph = k * 0.33f + i * 0.2f;
                    islandAnims.Add(dt =>
                    {
                        float u = (time * 0.35f + ph) % 1f;
                        s.localPosition = new Vector3(0.45f, 2.3f + u * 1.4f, -0.2f);
                        s.localScale = Vector3.one * (0.2f + u * 0.4f) * (1f - u * 0.8f);
                    });
                }
            }
            // Well
            Transform well = Group("Well", r, Rim(205f, 5.2f));
            Prim(PrimitiveType.Cylinder, "Ring", well, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.3f, 1f), new Color(0.65f, 0.62f, 0.65f));
            Prim(PrimitiveType.Cylinder, "Water", well, new Vector3(0f, 0.55f, 0f), new Vector3(0.8f, 0.02f, 0.8f), new Color(0.4f, 0.7f, 1f));
            obstacles.Add(new Obstacle { pos = Rim(205f, 5.2f), r = 0.6f });
            // Fence along the front
            Material fence = MakeMat(new Color(0.95f, 0.95f, 0.9f));
            for (int i = 0; i < 9; i++)
            {
                float a = 225f + i * 11f;
                if (a > 255f && a < 285f) continue;
                Prim(PrimitiveType.Cube, "Post", r, Rim(a, 6.6f, 0.3f), new Vector3(0.12f, 0.6f, 0.12f), fence);
            }
            // Villagers
            BuildCapy("Villager", r, Rim(95f, 5.5f), new Color(1f, 0.9f, 0.8f), 0.8f, false, new Color(0.4f, 0.6f, 0.95f), Acc.Hat)
                .root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Capy v2 = BuildCapy("Villager", r, Rim(330f, 5.4f), new Color(0.9f, 0.85f, 0.8f), 0.75f, false, new Color(1f, 0.6f, 0.8f), Acc.Flower);
            v2.root.localRotation = Quaternion.Euler(0f, -60f, 0f);
            islandAnims.Add(dt => v2.visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 3f)) * 0.1f, 0f));
            ScatterFlowers(r, 10, 2.5f, 5.2f);
        }

        void BuildCastleIsland(Transform r)
        {
            IslandDisk(r, 7.5f, new Color(0.7f, 0.66f, 0.7f), new Color(0.52f, 0.5f, 0.58f), 0.84f);
            MakeCastle(r, new Vector3(0f, 0f, 7.2f), 1.2f);
            for (int i = 0; i < 7; i++) obstacles.Add(new Obstacle { pos = new Vector3(-6f + i * 2f, 0f, 6.8f), r = 1f });
            MakeTorch(r, Rim(30f, 5.6f));
            MakeTorch(r, Rim(150f, 5.6f));
            MakeTorch(r, Rim(330f, 5.6f));
            MakeTorch(r, Rim(210f, 5.6f));
            // Bats
            Material batMat = MakeMat(new Color(0.2f, 0.15f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                Transform bat = Group("Bat", r, Vector3.zero);
                Prim(PrimitiveType.Sphere, "Body", bat, Vector3.zero, new Vector3(0.25f, 0.2f, 0.25f), batMat);
                Transform wl = PrimRot(PrimitiveType.Cube, "WingL", bat, new Vector3(-0.25f, 0f, 0f), new Vector3(0.4f, 0.03f, 0.2f), Vector3.zero, batMat).transform;
                Transform wr = PrimRot(PrimitiveType.Cube, "WingR", bat, new Vector3(0.25f, 0f, 0f), new Vector3(0.4f, 0.03f, 0.2f), Vector3.zero, batMat).transform;
                float ph = i * 1.6f;
                islandAnims.Add(dt =>
                {
                    float a = time * 0.5f + ph;
                    bat.localPosition = new Vector3(Mathf.Cos(a) * 5f, 4.5f + Mathf.Sin(a * 2f) * 0.6f, 4f + Mathf.Sin(a) * 2.5f);
                    float flap = Mathf.Sin(time * 14f + ph) * 35f;
                    wl.localRotation = Quaternion.Euler(0f, 0f, flap);
                    wr.localRotation = Quaternion.Euler(0f, 0f, -flap);
                });
            }
        }

        // ------------------------------------------------------------------
        //  Props
        // ------------------------------------------------------------------
        void MakePalm(Transform parent, Vector3 p, float s, List<Obstacle> obs)
        {
            Transform root = Group("Palm", parent, p);
            root.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0f, 1.1f, 0f), new Vector3(0.28f, 1.1f, 0.28f), new Color(0.65f, 0.45f, 0.28f));
            Material leafMat = MakeMat(new Color(0.3f, 0.75f, 0.35f));
            for (int i = 0; i < 4; i++)
                PrimRot(PrimitiveType.Sphere, "Leaf", root, new Vector3(0f, 2.3f, 0f), new Vector3(2.0f, 0.25f, 0.7f), new Vector3(8f, i * 45f, 0f), leafMat);
            Material nut = MakeMat(new Color(0.45f, 0.3f, 0.18f));
            Prim(PrimitiveType.Sphere, "Coconut", root, new Vector3(0.15f, 2.05f, 0.1f), Vector3.one * 0.22f, nut);
            Prim(PrimitiveType.Sphere, "Coconut", root, new Vector3(-0.12f, 2.05f, -0.1f), Vector3.one * 0.22f, nut);
            if (obs != null) obs.Add(new Obstacle { pos = p, r = 0.45f });
        }

        void MakeRoundTree(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("Tree", parent, p);
            t.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.7f, 0.3f), new Color(0.55f, 0.38f, 0.24f));
            Material leaf = MakeMat(new Color(0.3f, 0.68f, 0.3f));
            Prim(PrimitiveType.Sphere, "Crown", t, new Vector3(0f, 1.9f, 0f), Vector3.one * 1.6f, leaf);
            Prim(PrimitiveType.Sphere, "Crown2", t, new Vector3(0.5f, 1.6f, 0.2f), Vector3.one * 1.0f, leaf);
            Prim(PrimitiveType.Sphere, "Crown3", t, new Vector3(-0.45f, 1.7f, -0.2f), Vector3.one * 1.1f, leaf);
            Prim(PrimitiveType.Sphere, "Apple", t, new Vector3(0.3f, 1.7f, 0.75f), Vector3.one * 0.2f, new Color(0.95f, 0.25f, 0.25f));
        }

        void MakePine(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("Pine", parent, p);
            t.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.25f, 0.4f, 0.25f), new Color(0.5f, 0.34f, 0.22f));
            Material leaf = MakeMat(new Color(0.2f, 0.55f, 0.35f));
            for (int i = 0; i < 4; i++)
            {
                float w = 1.6f - i * 0.35f;
                Prim(PrimitiveType.Sphere, "Layer", t, new Vector3(0f, 1.0f + i * 0.55f, 0f), new Vector3(w, 0.55f, w), leaf);
            }
        }

        void MakeMushroom(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("Mushroom", parent, p);
            t.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cylinder, "Stem", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.18f, 0.2f, 0.18f), new Color(1f, 0.95f, 0.85f));
            Prim(PrimitiveType.Sphere, "Cap", t, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.35f, 0.6f), new Color(0.95f, 0.3f, 0.3f));
            Material dot = MakeMat(Color.white);
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Sphere, "Dot", t, Rim(i * 90f + 20f, 0.2f, 0.56f), Vector3.one * 0.09f, dot);
        }

        Transform MakeCrystal(Transform parent, Vector3 p, Color col, float s)
        {
            Transform t = Group("Crystal", parent, p);
            t.localScale = Vector3.one * s;
            Material m = MakeMat(col);
            PrimRot(PrimitiveType.Cube, "C1", t, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 1.0f, 0.3f), new Vector3(0f, 45f, 0f), m);
            PrimRot(PrimitiveType.Cube, "C2", t, new Vector3(0.25f, 0.3f, 0.1f), new Vector3(0.22f, 0.6f, 0.22f), new Vector3(0f, 30f, -20f), m);
            PrimRot(PrimitiveType.Cube, "C3", t, new Vector3(-0.22f, 0.28f, -0.05f), new Vector3(0.2f, 0.5f, 0.2f), new Vector3(10f, 60f, 22f), m);
            return t;
        }

        void MakeTorch(Transform parent, Vector3 p)
        {
            Transform t = Group("Torch", parent, p);
            Prim(PrimitiveType.Cylinder, "Stick", t, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), new Color(0.45f, 0.3f, 0.2f));
            Transform flame = Prim(PrimitiveType.Sphere, "Flame", t, new Vector3(0f, 1.35f, 0f), new Vector3(0.3f, 0.4f, 0.3f), new Color(1f, 0.65f, 0.15f)).transform;
            Light l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.35f);
            l.range = 6f;
            l.intensity = 2.2f;
            float ph = p.x;
            islandAnims.Add(dt =>
            {
                float f = 1f + Mathf.Sin(time * 11f + ph) * 0.1f + Mathf.Sin(time * 17f + ph) * 0.06f;
                flame.localScale = new Vector3(0.3f, 0.4f * f, 0.3f);
                l.intensity = 2.2f * f;
            });
        }

        void MakeUmbrella(Transform parent, Vector3 p, Color col)
        {
            Transform t = Group("Umbrella", parent, p);
            Prim(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, 0.9f, 0f), new Vector3(0.07f, 0.9f, 0.07f), new Color(0.95f, 0.95f, 0.95f));
            Prim(PrimitiveType.Sphere, "Top", t, new Vector3(0f, 1.8f, 0f), new Vector3(2f, 0.5f, 2f), col);
            Prim(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.15f, Color.white);
            Prim(PrimitiveType.Cube, "Towel", t, new Vector3(0.6f, -0.08f, -0.6f), new Vector3(0.8f, 0.02f, 1.4f), Color.Lerp(col, Color.white, 0.5f));
        }

        void MakeSandCastle(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("SandCastle", parent, p);
            t.localScale = Vector3.one * s;
            Material sand = MakeMat(new Color(0.93f, 0.8f, 0.5f));
            Prim(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f), sand);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Tower", t, Rim(45f + i * 90f, 0.75f, 0.5f), new Vector3(0.4f, 0.5f, 0.4f), sand);
            Prim(PrimitiveType.Cylinder, "Keep", t, new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.3f, 0.5f), sand);
            Prim(PrimitiveType.Cube, "Flag", t, new Vector3(0.12f, 1.4f, 0f), new Vector3(0.25f, 0.16f, 0.02f), new Color(1f, 0.4f, 0.5f));
            Prim(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, 1.3f, 0f), new Vector3(0.03f, 0.2f, 0.03f), Color.white);
        }

        void MakeMountain(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("Mountain", parent, p);
            t.localScale = Vector3.one * s;
            Material rock = MakeMat(new Color(0.55f, 0.58f, 0.62f));
            Prim(PrimitiveType.Sphere, "Base", t, new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 2.2f, 2.6f), rock);
            Prim(PrimitiveType.Sphere, "Peak", t, new Vector3(0f, 1.25f, 0f), new Vector3(1.4f, 1.3f, 1.4f), rock);
            Prim(PrimitiveType.Sphere, "Snow", t, new Vector3(0f, 1.75f, 0f), new Vector3(0.9f, 0.55f, 0.9f), Color.white);
        }

        void MakeFlowerPatch(Transform parent, Vector3 p, float radius, int n)
        {
            Color[] cols = { new Color(1f, 0.5f, 0.6f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.55f, 1f), Color.white };
            for (int i = 0; i < n; i++)
            {
                Vector3 fp = p + Rim(i * 137.5f, radius * Mathf.Sqrt((i + 0.5f) / n), 0.12f);
                Prim(PrimitiveType.Cylinder, "Stem", parent, fp - Vector3.up * 0.06f, new Vector3(0.03f, 0.1f, 0.03f), new Color(0.3f, 0.6f, 0.3f));
                Prim(PrimitiveType.Sphere, "Bloom", parent, fp + Vector3.up * 0.06f, Vector3.one * 0.18f, cols[i % cols.Length]);
            }
        }

        Transform MakeHouse(Transform parent, Vector3 p, float yaw, Color wall, Color roof, float s)
        {
            Transform t = Group("House", parent, p);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cube, "Walls", t, new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.4f, 1.4f), wall);
            Transform roofT = Group("Roof", t, new Vector3(0f, 1.4f, 0f));
            roofT.localScale = new Vector3(1.25f, 0.75f, 1.6f);
            PrimRot(PrimitiveType.Cube, "RoofBlock", roofT, Vector3.zero, Vector3.one, new Vector3(0f, 0f, 45f), MakeMat(roof));
            Prim(PrimitiveType.Cube, "Door", t, new Vector3(0f, 0.4f, -0.71f), new Vector3(0.4f, 0.8f, 0.04f), new Color(0.55f, 0.36f, 0.22f));
            Material win = MakeMat(new Color(0.7f, 0.9f, 1f));
            Prim(PrimitiveType.Cube, "Window", t, new Vector3(-0.5f, 0.85f, -0.71f), new Vector3(0.3f, 0.3f, 0.04f), win);
            Prim(PrimitiveType.Cube, "Window", t, new Vector3(0.5f, 0.85f, -0.71f), new Vector3(0.3f, 0.3f, 0.04f), win);
            Prim(PrimitiveType.Cube, "Chimney", t, new Vector3(0.45f, 1.95f, -0.2f), new Vector3(0.25f, 0.5f, 0.25f), new Color(0.6f, 0.4f, 0.35f));
            return t;
        }

        void MakeCastle(Transform parent, Vector3 p, float s)
        {
            Transform t = Group("Castle", parent, p);
            t.localScale = Vector3.one * s;
            Material stone = MakeMat(new Color(0.6f, 0.58f, 0.66f));
            Material dark = MakeMat(new Color(0.42f, 0.4f, 0.5f));
            Material roof = MakeMat(new Color(0.5f, 0.25f, 0.6f));
            Material flag = MakeMat(new Color(0.9f, 0.3f, 0.45f));
            Prim(PrimitiveType.Cube, "Wall", t, new Vector3(0f, 1.2f, 0f), new Vector3(9f, 2.4f, 1.2f), stone);
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Cube, "Merlon", t, new Vector3(-4f + i, 2.6f, -0.3f), new Vector3(0.5f, 0.45f, 0.5f), stone);
            Prim(PrimitiveType.Cube, "Gate", t, new Vector3(0f, 0.8f, -0.61f), new Vector3(1.6f, 1.6f, 0.05f), dark);
            Prim(PrimitiveType.Cylinder, "GateTop", t, new Vector3(0f, 1.6f, -0.61f), new Vector3(1.6f, 0.03f, 1.6f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 tp = new Vector3(s2 * 4.6f, 0f, 0f);
                Prim(PrimitiveType.Cylinder, "Tower", t, tp + Vector3.up * 2f, new Vector3(1.8f, 2f, 1.8f), stone);
                Prim(PrimitiveType.Capsule, "TowerRoof", t, tp + Vector3.up * 4.4f, new Vector3(1.9f, 0.9f, 1.9f), roof);
                Prim(PrimitiveType.Cylinder, "FlagPole", t, tp + Vector3.up * 5.6f, new Vector3(0.05f, 0.5f, 0.05f), Color.white);
                Transform f = Prim(PrimitiveType.Cube, "Flag", t, tp + new Vector3(0.3f, 5.9f, 0f), new Vector3(0.6f, 0.35f, 0.03f), flag).transform;
                float ph = s2;
                List<System.Action<float>> anims = parent == islandRoot ? islandAnims : mapAnims;
                anims.Add(dt => f.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 3f + ph) * 20f, 0f));
            }
            Prim(PrimitiveType.Cube, "Keep", t, new Vector3(0f, 3f, 0.6f), new Vector3(2.6f, 2.2f, 1.6f), stone);
            Prim(PrimitiveType.Capsule, "KeepRoof", t, new Vector3(0f, 4.6f, 0.6f), new Vector3(2.2f, 1.1f, 1.6f), roof);
        }

        Transform MakeCloud(Transform parent, Vector3 p)
        {
            Transform c = Group("Cloud", parent, p);
            Material m = MakeMat(Color.white);
            for (int k = 0; k < 3; k++)
            {
                float sx = (k == 1) ? 2f : 1.5f;
                Prim(PrimitiveType.Sphere, "Puff", c, new Vector3(k * 1.1f - 1.1f, k == 1 ? 0.4f : 0f, 0f), new Vector3(sx, 1.3f, 1.5f), m);
            }
            return c;
        }

        static void DriftCloud(Transform c, float dt, float halfWidth)
        {
            c.localPosition += Vector3.right * 0.35f * dt;
            if (c.localPosition.x > halfWidth) c.localPosition -= Vector3.right * halfWidth * 2f;
        }

        void AddButterflies(Transform r, int n)
        {
            Color[] cols = { new Color(1f, 0.6f, 0.8f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 0.8f, 1f), Color.white, new Color(0.8f, 0.6f, 1f) };
            for (int i = 0; i < n; i++)
            {
                Transform b = Group("Butterfly", r, Vector3.zero);
                Material m = MakeMat(cols[i % cols.Length]);
                Transform wl = Prim(PrimitiveType.Cube, "WingL", b, new Vector3(-0.1f, 0f, 0f), new Vector3(0.18f, 0.02f, 0.16f), m).transform;
                Transform wr = Prim(PrimitiveType.Cube, "WingR", b, new Vector3(0.1f, 0f, 0f), new Vector3(0.18f, 0.02f, 0.16f), m).transform;
                Vector3 c0 = Rim(i * 72f + 30f, 3.5f, 1.3f);
                float ph = i * 1.7f;
                islandAnims.Add(dt =>
                {
                    float a = time * 0.6f + ph;
                    b.localPosition = c0 + new Vector3(Mathf.Cos(a) * 1.4f, Mathf.Sin(a * 2.3f) * 0.3f, Mathf.Sin(a) * 1.4f);
                    b.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    float flap = Mathf.Sin(time * 18f + ph) * 50f;
                    wl.localRotation = Quaternion.Euler(0f, 0f, flap);
                    wr.localRotation = Quaternion.Euler(0f, 0f, -flap);
                });
            }
        }
    }
}
