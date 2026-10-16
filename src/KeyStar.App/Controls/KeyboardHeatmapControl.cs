using System.Windows;
using KeyStar.Core;

namespace KeyStar.App.Controls;

public sealed class KeyboardHeatmapControl : HeatmapControlBase
{
    private const int KeyboardColumnCount = 24;
    private const double MouseGap = 0.4;
    private const int MouseColumnCount = 3;
    private const double MouseWideCapWidth = 1.5;

    private static readonly (KeyId Id, string Label)[] MouseRowTop =
    [
        (KeyId.MouseLeftButton, "左键"),
        (KeyId.MouseMiddleButton, "中键"),
        (KeyId.MouseRightButton, "右键"),
    ];

    private static readonly (KeyId Id, string Label)[] MouseRowMiddle =
    [
        (KeyId.MouseXButton2, "前侧键"),
        (KeyId.MouseWheelUp, "上滚轮"),
    ];

    private static readonly (KeyId Id, string Label)[] MouseRowBottom =
    [
        (KeyId.MouseXButton1, "后侧键"),
        (KeyId.MouseWheelDown, "下滚轮"),
    ];

    public KeyboardHeatmapControl()
        : base(KeyboardColumnCount + MouseGap + MouseColumnCount, 6)
    {
        BuildKeyboard();
        BuildMouseBlock();
    }

    // 键盘和鼠标各自取最大值，避免滚轮次数很大时把键盘色阶压到最浅。
    protected override int GetScaleGroup(KeyId keyId) => IsMouseKey(keyId) ? 1 : 0;

    private void BuildKeyboard()
    {
        Add(KeyId.Escape, "Esc", 0, 0);
        AddSequence(2, 0,
            K(KeyId.F1, "F1"), K(KeyId.F2, "F2"), K(KeyId.F3, "F3"), K(KeyId.F4, "F4"));
        AddSequence(6.5, 0,
            K(KeyId.F5, "F5"), K(KeyId.F6, "F6"), K(KeyId.F7, "F7"), K(KeyId.F8, "F8"));
        AddSequence(11, 0,
            K(KeyId.F9, "F9"), K(KeyId.F10, "F10"), K(KeyId.F11, "F11"), K(KeyId.F12, "F12"));
        AddSequence(15.5, 0,
            K(KeyId.PrintScreen, "PrtSc"), K(KeyId.ScrollLock, "ScrLk"), K(KeyId.Pause, "Pause"));

        AddSequence(0, 1,
            K(KeyId.Grave, "`"), K(KeyId.Digit1, "1"), K(KeyId.Digit2, "2"), K(KeyId.Digit3, "3"),
            K(KeyId.Digit4, "4"), K(KeyId.Digit5, "5"), K(KeyId.Digit6, "6"), K(KeyId.Digit7, "7"),
            K(KeyId.Digit8, "8"), K(KeyId.Digit9, "9"), K(KeyId.Digit0, "0"), K(KeyId.Minus, "-"),
            K(KeyId.Equal, "="), K(KeyId.Backspace, "Backspace", 2));
        AddSequence(16, 1,
            K(KeyId.Insert, "Ins"), K(KeyId.Home, "Home"), K(KeyId.PageUp, "PgUp"));
        AddSequence(20, 1,
            K(KeyId.NumLock, "Num"), K(KeyId.NumpadDivide, "/"), K(KeyId.NumpadMultiply, "*"), K(KeyId.NumpadSubtract, "-"));

        AddSequence(0, 2,
            K(KeyId.Tab, "Tab", 1.5), K(KeyId.Q, "Q"), K(KeyId.W, "W"), K(KeyId.E, "E"), K(KeyId.R, "R"),
            K(KeyId.T, "T"), K(KeyId.Y, "Y"), K(KeyId.U, "U"), K(KeyId.I, "I"), K(KeyId.O, "O"), K(KeyId.P, "P"),
            K(KeyId.LeftBracket, "["), K(KeyId.RightBracket, "]"), K(KeyId.Backslash, "\\", 1.5));
        AddSequence(16, 2,
            K(KeyId.Delete, "Del"), K(KeyId.End, "End"), K(KeyId.PageDown, "PgDn"));
        AddSequence(20, 2,
            K(KeyId.Numpad7, "7"), K(KeyId.Numpad8, "8"), K(KeyId.Numpad9, "9"));
        Add(KeyId.NumpadAdd, "+", 23, 2, 1, 2);

        AddSequence(0, 3,
            K(KeyId.CapsLock, "Caps", 1.75), K(KeyId.A, "A"), K(KeyId.S, "S"), K(KeyId.D, "D"), K(KeyId.F, "F"),
            K(KeyId.G, "G"), K(KeyId.H, "H"), K(KeyId.J, "J"), K(KeyId.K, "K"), K(KeyId.L, "L"),
            K(KeyId.Semicolon, ";"), K(KeyId.Apostrophe, "'"), K(KeyId.Enter, "Enter", 2.25));
        AddSequence(20, 3,
            K(KeyId.Numpad4, "4"), K(KeyId.Numpad5, "5"), K(KeyId.Numpad6, "6"));

        AddSequence(0, 4,
            K(KeyId.LeftShift, "Shift", 2.25), K(KeyId.Z, "Z"), K(KeyId.X, "X"), K(KeyId.C, "C"), K(KeyId.V, "V"),
            K(KeyId.B, "B"), K(KeyId.N, "N"), K(KeyId.M, "M"), K(KeyId.Comma, ","), K(KeyId.Period, "."),
            K(KeyId.Slash, "/"), K(KeyId.RightShift, "Shift", 2.75));
        Add(KeyId.ArrowUp, "↑", 17, 4);
        AddSequence(20, 4,
            K(KeyId.Numpad1, "1"), K(KeyId.Numpad2, "2"), K(KeyId.Numpad3, "3"));
        Add(KeyId.NumpadEnter, "Enter", 23, 4, 1, 2);

        AddSequence(0, 5,
            K(KeyId.LeftControl, "Ctrl", 1.25), K(KeyId.LeftWindows, "Win", 1.25), K(KeyId.LeftAlt, "Alt", 1.25),
            K(KeyId.Space, "Space", 6.25), K(KeyId.RightAlt, "Alt", 1.25), K(KeyId.RightWindows, "Win", 1.25),
            K(KeyId.Application, "Menu", 1.25), K(KeyId.RightControl, "Ctrl", 1.25));
        AddSequence(16, 5,
            K(KeyId.ArrowLeft, "←"), K(KeyId.ArrowDown, "↓"), K(KeyId.ArrowRight, "→"));
        Add(KeyId.Numpad0, "0", 20, 5, 2);
        Add(KeyId.NumpadDecimal, ".", 22, 5);
    }

    // 鼠标按键模仿小键盘，整体排在键盘最右侧；与数字键盘顶部对齐，占三列三行。
    private void BuildMouseBlock()
    {
        const double startX = KeyboardColumnCount + MouseGap;
        const double startY = 1;
        AddSequence(startX, startY, MouseRowTop.Select(key => K(key.Id, key.Label)).ToArray());
        AddSequence(
            startX,
            startY + 1,
            MouseRowMiddle.Select(key => K(key.Id, key.Label, MouseWideCapWidth)).ToArray());
        AddSequence(
            startX,
            startY + 2,
            MouseRowBottom.Select(key => K(key.Id, key.Label, MouseWideCapWidth)).ToArray());
    }

    private static bool IsMouseKey(KeyId keyId) => keyId is >= KeyId.MouseLeftButton and <= KeyId.MouseXButton2;
}
