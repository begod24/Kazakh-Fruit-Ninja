using UnityEngine;
using UnityEngine.InputSystem;

namespace KazakhNinja
{
    /// <summary>One blade: the primary touch on phones, the left mouse button or pen in the editor.</summary>
    public static class PointerInput
    {
        public static bool TryGetPressedPosition(out Vector2 position)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            Pen pen = Pen.current;
            if (pen != null && pen.tip.isPressed)
            {
                position = pen.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }
    }
}
