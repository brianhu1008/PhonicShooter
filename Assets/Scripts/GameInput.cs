using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 只需要滑鼠 (或觸控螢幕)：移動準星、點一下發射。
// 同時支援新版 Input System 與舊版 Input Manager，看專案設定用哪一個。
public static class GameInput
{
#if ENABLE_INPUT_SYSTEM
    // 螢幕座標 (左下角為原點)
    public static Vector2 PointerPosition => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
    public static bool Fire => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    public static bool Pause => Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
    public static bool Replay => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    // 正在用手指觸控 (iPad)
    public static bool IsTouch => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
    // 滑鼠、手指或觸控筆正按著
    public static bool PointerHeld => Pointer.current != null && Pointer.current.press.isPressed;
    // 鍵盤方向鍵 / WASD
    public static Vector2 Move
    {
        get
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.rightArrowKey.isPressed || k.dKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed || k.aKey.isPressed ? 1f : 0f);
            float y = (k.upArrowKey.isPressed || k.wKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed || k.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }
#else
    public static bool IsTouch => Input.touchCount > 0;
    public static bool PointerHeld => Input.GetMouseButton(0);
    public static Vector2 Move => new Vector2(
        (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1f : 0f),
        (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1f : 0f));
    public static Vector2 PointerPosition => Input.mousePosition;
    public static bool Fire => Input.GetMouseButtonDown(0);
    public static bool Pause => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
    public static bool Replay => Input.GetKeyDown(KeyCode.R);
#endif
}
