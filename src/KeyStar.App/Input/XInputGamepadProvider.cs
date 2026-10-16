using System.Runtime.InteropServices;
using KeyStar.Core;

namespace KeyStar.App.Input;

// Xbox 手柄以及工作在 XInput 兼容模式下的其他手柄（多数国产手柄、Steam 输入下的 DualSense 等）。
public sealed class XInputGamepadProvider : IGamepadProvider
{
    private const uint ErrorSuccess = 0;

    public XInputGamepadProvider(int slotCount = 4)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        SlotCount = slotCount;
    }

    public int SlotCount { get; }

    public bool TryGetState(int slot, out GamepadState state)
    {
        state = GamepadState.Empty;
        if (slot < 0 || slot >= SlotCount)
        {
            return false;
        }

        if (XInputGetState((uint)slot, out var nativeState) != ErrorSuccess)
        {
            return false;
        }

        // XInput 的按键位与 GamepadButtons 一一对应。
        state = new GamepadState(
            (GamepadButtons)nativeState.Gamepad.Buttons,
            nativeState.Gamepad.LeftTrigger,
            nativeState.Gamepad.RightTrigger);
        return true;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint index, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }
}
