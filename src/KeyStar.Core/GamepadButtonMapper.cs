namespace KeyStar.Core;

public readonly record struct GamepadButtonChange(GamepadButton Button, bool IsPressed);

public static class GamepadButtonMapper
{
    // 扳机是模拟量，达到阈值即视为一次按下。
    public const byte TriggerThreshold = 30;

    public static IReadOnlyList<GamepadButton> Buttons { get; } = Enum.GetValues<GamepadButton>();

    public static KeyId ToKeyId(GamepadButton button) => (KeyId)((int)KeyId.GamepadA + (int)button);

    public static bool IsPressed(GamepadState state, GamepadButton button) => button switch
    {
        GamepadButton.A => (state.Buttons & GamepadButtons.A) != 0,
        GamepadButton.B => (state.Buttons & GamepadButtons.B) != 0,
        GamepadButton.X => (state.Buttons & GamepadButtons.X) != 0,
        GamepadButton.Y => (state.Buttons & GamepadButtons.Y) != 0,
        GamepadButton.LeftBumper => (state.Buttons & GamepadButtons.LeftBumper) != 0,
        GamepadButton.RightBumper => (state.Buttons & GamepadButtons.RightBumper) != 0,
        GamepadButton.LeftTrigger => state.LeftTrigger >= TriggerThreshold,
        GamepadButton.RightTrigger => state.RightTrigger >= TriggerThreshold,
        GamepadButton.View => (state.Buttons & GamepadButtons.View) != 0,
        GamepadButton.Menu => (state.Buttons & GamepadButtons.Menu) != 0,
        GamepadButton.LeftStick => (state.Buttons & GamepadButtons.LeftStick) != 0,
        GamepadButton.RightStick => (state.Buttons & GamepadButtons.RightStick) != 0,
        GamepadButton.DpadUp => (state.Buttons & GamepadButtons.DpadUp) != 0,
        GamepadButton.DpadDown => (state.Buttons & GamepadButtons.DpadDown) != 0,
        GamepadButton.DpadLeft => (state.Buttons & GamepadButtons.DpadLeft) != 0,
        GamepadButton.DpadRight => (state.Buttons & GamepadButtons.DpadRight) != 0,
        _ => false,
    };
}
