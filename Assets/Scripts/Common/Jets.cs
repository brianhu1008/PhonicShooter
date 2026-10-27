using UnityEngine;

// 用基本形狀拼出戰機 (機頭朝 +z)。遊樂園的戰機設施和九九乘法戰機共用
public static class Jets
{
    public static Transform Fighter(Transform parent, Color main, Color accent, Color glow)
    {
        var root = new GameObject("Jet").transform;
        root.SetParent(parent, false);
        var dark = new Color(0.18f, 0.2f, 0.24f);
        var glass = new Color(0.35f, 0.85f, 1f);

        // 機身、機頭、座艙
        Rot(Shapes.Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.45f, 1.4f, 0.45f), main), 90, 0, 0);
        Shapes.Part(PrimitiveType.Sphere, root, new Vector3(0, 0, 1.4f), new Vector3(0.45f, 0.42f, 1.0f), main);
        Shapes.Part(PrimitiveType.Sphere, root, new Vector3(0, 0.22f, 0.55f), new Vector3(0.3f, 0.26f, 0.75f), glass);

        // 後掠主翼 + 翼尖色帶
        for (int s = -1; s <= 1; s += 2)
        {
            Rot(Shapes.Part(PrimitiveType.Cube, root, new Vector3(0.95f * s, 0, -0.25f), new Vector3(1.8f, 0.08f, 1.1f), main), 0, -22f * s, 0);
            Rot(Shapes.Part(PrimitiveType.Cube, root, new Vector3(1.65f * s, 0.05f, -0.55f), new Vector3(0.45f, 0.06f, 0.7f), accent), 0, -22f * s, 0);
            // 尾翼
            Rot(Shapes.Part(PrimitiveType.Cube, root, new Vector3(0.55f * s, 0, -1.3f), new Vector3(0.9f, 0.06f, 0.5f), main), 0, -15f * s, 0);
            Rot(Shapes.Part(PrimitiveType.Cube, root, new Vector3(0.28f * s, 0.32f, -1.2f), new Vector3(0.06f, 0.6f, 0.6f), accent), 0, 0, -12f * s);
            // 引擎和噴焰
            Rot(Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0.24f * s, -0.05f, -1.35f), new Vector3(0.26f, 0.25f, 0.26f), dark), 90, 0, 0);
            Shapes.Part(PrimitiveType.Sphere, root, new Vector3(0.24f * s, -0.05f, -1.68f), new Vector3(0.24f, 0.24f, 0.45f), glow);
            // 機翼下的飛彈
            Rot(Shapes.Part(PrimitiveType.Cylinder, root, new Vector3(0.9f * s, -0.12f, -0.1f), new Vector3(0.1f, 0.4f, 0.1f), Color.white), 90, 0, 0);
        }
        return root;
    }

    // 玩家：藍白色
    public static Transform Player(Transform parent) =>
        Fighter(parent, new Color(0.35f, 0.55f, 0.95f), Color.white, new Color(1f, 0.85f, 0.3f));

    // 敵機：機頭朝 -z (朝玩家飛)
    public static Transform Enemy(Transform parent, Color main)
    {
        var holder = new GameObject("EnemyJet").transform;
        holder.SetParent(parent, false);
        var jet = Fighter(holder, main, new Color(0.25f, 0.25f, 0.28f), new Color(1f, 0.4f, 0.2f));
        jet.localRotation = Quaternion.Euler(0, 180f, 0);
        return holder;
    }

    static GameObject Rot(GameObject go, float x, float y, float z)
    {
        go.transform.localRotation = Quaternion.Euler(x, y, z);
        return go;
    }

    // 兩點之間的一根柱子 (摩天輪的支架、輪輻用)
    public static GameObject Beam(Transform parent, Vector3 a, Vector3 b, float thickness, Color color)
    {
        var go = Shapes.Part(PrimitiveType.Cylinder, parent, (a + b) / 2f, new Vector3(thickness, (b - a).magnitude / 2f, thickness), color);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        return go;
    }
}
