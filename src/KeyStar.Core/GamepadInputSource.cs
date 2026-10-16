namespace KeyStar.Core;

// 轮询手柄状态并把按键跳变交给计数器：只在按下/松开的瞬间计数，按住不会连发。
// 与具体品牌无关，实际读取由 IGamepadProvider 提供（当前为 XInput）。
public sealed class GamepadInputSource : IDisposable
{
    public const int DefaultPollIntervalMilliseconds = 16;

    // 手柄使用独立的伪设备句柄，与键盘、鼠标的 Raw Input 句柄区分开，便于多手柄分别计数。
    private const nint DeviceHandleBase = 0x5F10_0000;

    private readonly IGamepadProvider _provider;
    private readonly KeyboardCounter _counter;
    private readonly Action<KeyId, int>? _countedKeySink;
    private readonly GamepadButtonTracker[] _trackers;
    private readonly bool[] _connected;
    private readonly GamepadButtonChange[] _changes = new GamepadButtonChange[GamepadButtonMapper.Buttons.Count];
    private readonly Timer _pollTimer;
    private readonly object _pollGate = new();
    private int _connectedCount;
    private int _isAvailable = 1;
    private bool _disposed;

    public GamepadInputSource(
        IGamepadProvider provider,
        KeyboardCounter counter,
        Action<KeyId, int>? countedKeySink = null,
        int pollIntervalMilliseconds = DefaultPollIntervalMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(counter);
        if (pollIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pollIntervalMilliseconds));
        }

        _provider = provider;
        _counter = counter;
        _countedKeySink = countedKeySink;
        _trackers = new GamepadButtonTracker[provider.SlotCount];
        _connected = new bool[provider.SlotCount];
        for (var slot = 0; slot < provider.SlotCount; slot++)
        {
            _trackers[slot] = new GamepadButtonTracker();
        }

        _pollTimer = new Timer(
            _ => PollOnce(),
            null,
            pollIntervalMilliseconds,
            pollIntervalMilliseconds);
    }

    public string LastInputDescription { get; private set; } = "等待手柄输入…";

    public int ConnectedCount => Volatile.Read(ref _connectedCount);

    public bool IsAvailable => Volatile.Read(ref _isAvailable) != 0;

    public void ResetDiagnostics() => LastInputDescription = "等待手柄输入…";

    // 同步执行一次采样；定时器每秒调用约 60 次，测试也可以直接驱动。
    public void PollOnce()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_pollGate)
            {
                if (_disposed)
                {
                    return;
                }

                PollLocked();
            }
        }
        catch (Exception exception)
        {
            // 手柄驱动或系统接口不可用时不影响到键盘鼠标采集。
            Volatile.Write(ref _isAvailable, 0);
            LastInputDescription = $"手柄采集不可用：{exception.Message}";
            Dispose();
        }
    }

    private void PollLocked()
    {
        var connectedCount = 0;
        for (var slot = 0; slot < _provider.SlotCount; slot++)
        {
            if (!_provider.TryGetState(slot, out var gamepadState))
            {
                MarkDisconnected(slot);
                continue;
            }

            connectedCount++;
            if (!_connected[slot])
            {
                // 刚连接时忽略当前已经按住的键，避免把起始状态算成一次按下。
                _connected[slot] = true;
                _trackers[slot].Update(gamepadState, _changes);
                continue;
            }

            var changeCount = _trackers[slot].Update(gamepadState, _changes);
            for (var index = 0; index < changeCount; index++)
            {
                var change = _changes[index];
                var keyId = GamepadButtonMapper.ToKeyId(change.Button);
                var result = _counter.ProcessButton(DeviceHandle(slot), keyId, isBreak: !change.IsPressed);
                if (change.IsPressed)
                {
                    if (result.Counted)
                    {
                        _countedKeySink?.Invoke(keyId, 1);
                    }

                    LastInputDescription = $"最近识别：{KeyCatalog.Get(keyId).DisplayName}";
                }
            }
        }

        Volatile.Write(ref _connectedCount, connectedCount);
    }

    private void MarkDisconnected(int slot)
    {
        if (!_connected[slot])
        {
            return;
        }

        _connected[slot] = false;
        _trackers[slot].Reset();
        _counter.RemoveDevice(DeviceHandle(slot));
    }

    private static nint DeviceHandle(int slot) => DeviceHandleBase + slot;

    public void Dispose()
    {
        lock (_pollGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pollTimer.Dispose();
        }
    }
}
