using KeyStar.Core;

namespace KeyStar.App.Controls;

// 手柄热力图：按手柄实际形状摆放按键，单独占用一个标签页，不与键盘混排。
public sealed class GamepadHeatmapControl : HeatmapControlBase
{
    public GamepadHeatmapControl()
        : base(12, 6)
    {
        BuildGamepad();
    }

    private void BuildGamepad()
    {
        // 左半部分：扳机、肩键、十字键、左摇杆按下。
        Add(KeyId.GamepadLeftTrigger, "LT", 0, 0, 3);
        Add(KeyId.GamepadLeftBumper, "LB", 0, 1, 3);
        Add(KeyId.GamepadDpadUp, "↑", 1, 2);
        Add(KeyId.GamepadDpadLeft, "←", 0, 3);
        Add(KeyId.GamepadDpadRight, "→", 2, 3);
        Add(KeyId.GamepadDpadDown, "↓", 1, 4);
        Add(KeyId.GamepadLeftStick, "左摇杆", 0, 5, 3);

        // 中部：View 与 Menu。
        Add(KeyId.GamepadView, "View", 4, 2, 2);
        Add(KeyId.GamepadMenu, "Menu", 6, 2, 2);

        // 右半部分：扳机、肩键、ABXY、右摇杆按下。
        Add(KeyId.GamepadRightTrigger, "RT", 9, 0, 3);
        Add(KeyId.GamepadRightBumper, "RB", 9, 1, 3);
        Add(KeyId.GamepadY, "Y", 9, 2);
        Add(KeyId.GamepadX, "X", 8, 3);
        Add(KeyId.GamepadB, "B", 10, 3);
        Add(KeyId.GamepadA, "A", 9, 4);
        Add(KeyId.GamepadRightStick, "右摇杆", 9, 5, 3);
    }
}
