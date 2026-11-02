using UnityEngine;

// 一朵小花 (莖、葉子、五片花瓣、花心)。樂園的花園入口和九九乘法花園都用這個。
public static class GardenFlower
{
    // 同一排用同一個顏色，一排就是一組
    public static Color RowColor(int row) => Color.HSVToRGB(Mathf.Repeat(row * 0.13f + 0.95f, 1f), 0.55f, 1f);

    public static Transform Make(Transform parent, Vector3 localPos, Color petal, float size = 1f)
    {
        var f = new GameObject("Flower").transform;
        f.SetParent(parent, false);
        f.localPosition = localPos;
        f.localScale = Vector3.one * size;
        var green = new Color(0.3f, 0.65f, 0.3f);
        Shapes.Part(PrimitiveType.Cylinder, f, new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.3f, 0.06f), green);
        var leaf = Shapes.Part(PrimitiveType.Sphere, f, new Vector3(0.1f, 0.22f, 0), new Vector3(0.22f, 0.05f, 0.1f), green);
        leaf.transform.localRotation = Quaternion.Euler(0, 0, 25f);
        var head = new GameObject("Head").transform;
        head.SetParent(f, false);
        head.localPosition = new Vector3(0, 0.62f, 0);
        head.localRotation = Quaternion.Euler(-35f, 0, 0); // 有點朝向鏡頭
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            Shapes.Part(PrimitiveType.Sphere, head, new Vector3(Mathf.Cos(a) * 0.15f, 0, Mathf.Sin(a) * 0.15f), new Vector3(0.2f, 0.07f, 0.2f), petal);
        }
        Shapes.Part(PrimitiveType.Sphere, head, new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.09f, 0.14f), new Color(1f, 0.85f, 0.25f));
        return f;
    }
}
