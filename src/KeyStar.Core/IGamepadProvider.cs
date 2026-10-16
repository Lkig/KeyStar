namespace KeyStar.Core;

// 手柄采集的统一入口：以后新增其他品牌（原生 HID、DirectInput 等）只需再实现一个提供程序，
// 计数、存储和界面都使用同一套 GamepadState / GamepadButton 模型。
public interface IGamepadProvider
{
    int SlotCount { get; }

    bool TryGetState(int slot, out GamepadState state);
}
