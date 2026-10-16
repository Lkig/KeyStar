namespace KeyStar.Core;

// 手柄按键的通用命名，按 Xbox 布局排列。
// 其他品牌（PlayStation 布局、国产手柄等）由采集端映射到同一套按键，计数与存储无需区分品牌。
public enum GamepadButton
{
    A, B, X, Y,
    LeftBumper, RightBumper,
    LeftTrigger, RightTrigger,
    View, Menu,
    LeftStick, RightStick,
    DpadUp, DpadDown, DpadLeft, DpadRight,
}
