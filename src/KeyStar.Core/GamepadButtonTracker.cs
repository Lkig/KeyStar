namespace KeyStar.Core;

// 把连续采样转换成按键状态跳变，避免按住不放被反复计数。
public sealed class GamepadButtonTracker
{
    private readonly bool[] _pressed = new bool[GamepadButtonMapper.Buttons.Count];

    // 把本次相对上次的变化写入 changes，返回变化数量（不会超过 changes.Length）。
    public int Update(GamepadState state, Span<GamepadButtonChange> changes)
    {
        var changeCount = 0;
        for (var index = 0; index < _pressed.Length; index++)
        {
            var button = GamepadButtonMapper.Buttons[index];
            var isPressed = GamepadButtonMapper.IsPressed(state, button);
            if (isPressed == _pressed[index])
            {
                continue;
            }

            _pressed[index] = isPressed;
            if (changeCount < changes.Length)
            {
                changes[changeCount++] = new GamepadButtonChange(button, isPressed);
            }
        }

        return changeCount;
    }

    // 手柄断开时调用，避免重新连接后把仍然按住的键算成一次新按下。
    public void Reset() => Array.Clear(_pressed);
}
