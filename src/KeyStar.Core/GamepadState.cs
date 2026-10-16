namespace KeyStar.Core;

// 与 XInput 的按键位保持一致，便于采集端直接转换。
[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Menu = 0x0010,
    View = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LeftBumper = 0x0100,
    RightBumper = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

// 一次采样得到的手柄状态：只保留按键位和扳机数值，不保留摇杆坐标。
public readonly record struct GamepadState(GamepadButtons Buttons, byte LeftTrigger, byte RightTrigger)
{
    public static GamepadState Empty => new(GamepadButtons.None, 0, 0);
}
