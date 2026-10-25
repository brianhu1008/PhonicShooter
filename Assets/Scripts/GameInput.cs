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
#else
    public static Vector2 PointerPosition => Input.mousePosition;
    public static bool Fire => Input.GetMouseButtonDown(0);
    public static bool Pause => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
    public static bool Replay => Input.GetKeyDown(KeyCode.R);
#endif
}
