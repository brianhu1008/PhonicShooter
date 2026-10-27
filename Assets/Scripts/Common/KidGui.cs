using UnityEngine;

// 大字、大按鈕的介面工具 (IMGUI)。以 1920x1080 設計，窄螢幕 (iPad 4:3) 以寬度為準
public class KidGui
{
    public GUIStyle Small, Mid, Big, Huge, Button, Hint;
    public float W = 1920f, H = 1080f, Scale = 1f;
    Texture2D white;

    // 每次 OnGUI 一開始呼叫
    public void Begin()
    {
        if (Small == null)
        {
            white = Texture2D.whiteTexture;
            Small = Style(34);
            Mid = Style(46);
            Big = Style(80);
            Huge = Style(110);
            Hint = Style(26);
            Button = new GUIStyle(GUI.skin.button) { font = ZhText.Font, fontSize = 40, wordWrap = true, richText = true };
            Button.normal.textColor = Color.white;
            Button.hover.textColor = Color.yellow;
        }
        // 橫放以 1920x1080 設計；直放 (iPad 轉正) 以 1080x1920 設計
        Scale = Portrait ? Mathf.Min(Screen.height / 1920f, Screen.width / 1080f) : Mathf.Min(Screen.height / 1080f, Screen.width / 1920f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        W = Screen.width / Scale;
        H = Screen.height / Scale;
    }

    GUIStyle Style(int size)
    {
        var s = new GUIStyle(GUI.skin.label) { font = ZhText.Font, fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        s.normal.textColor = Color.white;
        return s;
    }

    public static bool Portrait => Screen.height > Screen.width;

    // 螢幕座標 (左下原點) → 介面座標
    public Vector2 ToGui(Vector2 screen) => new Vector2(screen.x / Scale, (Screen.height - screen.y) / Scale);

    public void Box(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    public void Label(Rect r, string text, GUIStyle style, Color color)
    {
        // 加一點陰影比較好讀
        var old = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, 0.6f * color.a);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }

    public bool Dialog(string title, string sub, out int choice, params string[] buttons)
    {
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
        var panel = new Rect(W / 2 - 450, H / 2 - (260 + buttons.Length * 120) / 2f, 900, 260 + buttons.Length * 120);
        Box(panel, new Color(0.15f, 0.2f, 0.4f, 0.95f));
        Label(new Rect(panel.x, panel.y + 30, panel.width, 110), title, Big, new Color(1f, 0.9f, 0.4f));
        Label(new Rect(panel.x, panel.y + 140, panel.width, 80), sub, Mid, Color.white);
        choice = -1;
        for (int i = 0; i < buttons.Length; i++)
            if (GUI.Button(new Rect(panel.x + 150, panel.y + 240 + i * 120, 600, 100), buttons[i], Button)) choice = i;
        return choice >= 0;
    }

    public void Bar(Rect r, float frac, Color color, string text)
    {
        Box(r, new Color(0, 0, 0, 0.5f));
        var inner = new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8);
        Box(inner, new Color(0.25f, 0.05f, 0.05f, 0.9f));
        Box(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(frac), inner.height), color);
        if (!string.IsNullOrEmpty(text)) Label(inner, "<size=26>" + text + "</size>", Small, Color.white);
    }
}
