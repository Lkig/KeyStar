using KeyStar.Core;
using KeyStar.Storage;

var tests = new (string Name, Func<Task> Run)[]
{
    ("标准字母按扫描码映射", Sync(StandardLetterMapsByScanCode)),
    ("左右修饰键分离", Sync(LeftAndRightModifiersAreDistinct)),
    ("主 Enter 与数字键盘 Enter 分离", Sync(MainAndNumpadEnterAreDistinct)),
    ("导航键与数字键盘分离", Sync(NavigationAndNumpadAreDistinct)),
    ("小键盘导航键按功能记录", Sync(NumpadNavigationKeysFollowVirtualKey)),
    ("F13 到 F24 映射", Sync(ExtendedFunctionKeysMap)),
    ("媒体键回退映射", Sync(MediaKeyFallbackMaps)),
    ("媒体虚拟键优先于冲突扫描码", Sync(MediaVirtualKeyWinsOverScanCode)),
    ("长按自动连发只计一次", Sync(RepeatedMakeCountsOnce)),
    ("松开后再次按下重新计数", Sync(ReleaseThenPressCountsAgain)),
    ("两把键盘分别按下会各计一次", Sync(TwoDevicesCountSeparately)),
    ("组合键中的每个物理键分别计数", Sync(ChordKeysCountSeparately)),
    ("系统切换丢失松开消息后可恢复计数", Sync(SystemSwitchReleaseCanBeReconciled)),
    ("Alt+Tab 补偿与 Raw Input 不会重复计数", Sync(AltTabFallbackAndRawInputAreDeduplicated)),
    ("鼠标左右键分别计数且按住不连发", Sync(MouseButtonsCountSeparately)),
    ("鼠标中键与侧键分别计数且按住不连发", Sync(MouseMiddleAndSideButtonsCountSeparately)),
    ("滚轮上下滚按刻度累计且不进入按下状态", Sync(WheelNotchesCountWithoutPressedState)),
    ("暂停期间滚轮不计数", Sync(WheelStopsCountingWhilePaused)),
    ("新增键位只追加在枚举末尾", Sync(NewKeysAreAppendedAfterExistingKeys)),
    ("手柄按键映射到通用键位", Sync(GamepadButtonsMapToKeyCatalog)),
    ("手柄扳机按阈值判定按下", Sync(GamepadTriggersUseThreshold)),
    ("手柄按住不重复计数，松开再按重新计数", Sync(GamepadHeldButtonCountsOnce)),
    ("手柄多个按键同时按下分别计数", Sync(GamepadChordCountsSeparately)),
    ("手柄重新连接后不会把按住的键算成新按下", Sync(GamepadResetIgnoresHeldButtons)),
    ("手柄采集：连接后按下计一次、按住不连发", Sync(GamepadInputCountsPressOnce)),
    ("手柄采集：断开清理按下状态且重连不误计", Sync(GamepadInputClearsStateOnDisconnect)),
    ("手柄采集：多手柄分别计数", Sync(GamepadCountsPerSlotSeparately)),
    ("手柄采集：暂停期间不计数", Sync(GamepadRespectsPause)),
    ("手柄采集：驱动异常只停用手柄", Sync(GamepadFailureOnlyDisablesGamepad)),
    ("手柄按键可经采集器写入数据库", GamepadPressesPersistThroughRecorderAsync),
    ("暂停期间不计数且恢复不误计长按", Sync(PauseMaintainsPressedState)),
    ("清零不破坏按下状态", Sync(ClearDoesNotBreakPressedState)),
    ("界面清零可重置残留按下状态", Sync(ClearCanResetPressedState)),
    ("设备移除会清理按下状态", Sync(RemovingDeviceClearsPressedState)),
    ("未知键不会写入计数", Sync(UnknownKeyIsIgnored)),
    ("稀疏计数向量往返一致", Sync(CountVectorRoundTrips)),
    ("阶段 2 的 Key Map v1 数据仍可读取", LegacyKeyMapRowsRemainReadableAsync),
    ("新增手柄键位后 Key Map v3 数据仍可读取", LegacyKeyMapV3RowsRemainReadableAsync),
    ("重置记录会归档旧库并新建空库", ArchiveKeepsPreviousDataAsync),
    ("同一秒内多次归档不会覆盖存档", ArchiveNamesDoNotCollideAsync),
    ("数据库一分钟写入与绝对快照更新", RepositoryUpsertIsAbsoluteAsync),
    ("全部范围边界覆盖首末有效分钟", DataRangeTracksSparseBoundsAsync),
    ("历史总数量可排除当前分钟", TotalCountCanExcludeCurrentMinuteAsync),
    ("十分钟派生汇总正确", TenMinuteRollupIsCorrectAsync),
    ("十分钟时段序列正确处理范围边缘", TenMinuteSeriesHandlesRangeEdgesAsync),
    ("同一分钟重启后继续累计", RecorderResumesCurrentMinuteAsync),
    ("滚轮刻度与侧键可通过采集器落库", WheelNotchesPersistThroughRecorderAsync),
    ("CSV 导出包含分钟与键位", CsvExportContainsMinuteRowsAsync),
    ("设置文件可原子创建和读取", SettingsStoreRoundTripsAsync),
};

var failures = new List<string>();
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
        Console.WriteLine($"FAIL  {name}\n      {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"结果：{tests.Length - failures.Count}/{tests.Length} 通过");
if (failures.Count > 0)
{
    Console.WriteLine(string.Join(Environment.NewLine, failures));
    Environment.ExitCode = 1;
}

return;

static Func<Task> Sync(Action action) => () =>
{
    action();
    return Task.CompletedTask;
};

static void StandardLetterMapsByScanCode()
{
    Equal(KeyId.A, Normalize(0x1E, 0x41));
    Equal(KeyId.Q, Normalize(0x10, 0x51));
    Equal(KeyId.Space, Normalize(0x39, 0x20));
}

static void LeftAndRightModifiersAreDistinct()
{
    Equal(KeyId.LeftShift, Normalize(0x2A, 0x10));
    Equal(KeyId.RightShift, Normalize(0x36, 0x10));
    Equal(KeyId.LeftControl, Normalize(0x1D, 0x11));
    Equal(KeyId.RightControl, Normalize(0x1D, 0x11, RawKeyFlags.E0));
    Equal(KeyId.LeftAlt, Normalize(0x38, 0x12));
    Equal(KeyId.RightAlt, Normalize(0x38, 0x12, RawKeyFlags.E0));
}

static void MainAndNumpadEnterAreDistinct()
{
    Equal(KeyId.Enter, Normalize(0x1C, 0x0D));
    Equal(KeyId.NumpadEnter, Normalize(0x1C, 0x0D, RawKeyFlags.E0));
}

static void NavigationAndNumpadAreDistinct()
{
    Equal(KeyId.Numpad7, Normalize(0x47, 0x67));
    Equal(KeyId.Home, Normalize(0x47, 0x24, RawKeyFlags.E0));
    Equal(KeyId.NumpadDecimal, Normalize(0x53, 0x6E));
    Equal(KeyId.Delete, Normalize(0x53, 0x2E, RawKeyFlags.E0));
}

static void NumpadNavigationKeysFollowVirtualKey()
{
    // NumLock 关闭时小键盘按导航功能工作，Windows 给出的是导航虚拟键，
    // 这类键盘（例如 98 配列）上的 Home/End 必须按功能记录。
    Equal(KeyId.Home, Normalize(0x47, 0x24));
    Equal(KeyId.End, Normalize(0x4F, 0x23));
    Equal(KeyId.PageUp, Normalize(0x49, 0x21));
    Equal(KeyId.PageDown, Normalize(0x51, 0x22));
    Equal(KeyId.ArrowLeft, Normalize(0x4B, 0x25));
    Equal(KeyId.ArrowUp, Normalize(0x48, 0x26));
    Equal(KeyId.ArrowRight, Normalize(0x4D, 0x27));
    Equal(KeyId.ArrowDown, Normalize(0x50, 0x28));
    Equal(KeyId.Insert, Normalize(0x52, 0x2D));
    Equal(KeyId.Delete, Normalize(0x53, 0x2E));

    // NumLock 打开时同一个物理键仍然是数字键盘键。
    Equal(KeyId.Numpad7, Normalize(0x47, 0x67));
    Equal(KeyId.Numpad1, Normalize(0x4F, 0x61));
    Equal(KeyId.NumpadDecimal, Normalize(0x53, 0x6E));

    // 主键区导航键（带 E0）和数字键盘导航键合并到同一个键位。
    Equal(KeyId.Home, Normalize(0x47, 0x24, RawKeyFlags.E0));
    Equal(KeyId.End, Normalize(0x4F, 0x23, RawKeyFlags.E0));
    Equal(KeyId.ArrowUp, Normalize(0x48, 0x26, RawKeyFlags.E0));
    Equal(KeyId.Delete, Normalize(0x53, 0x2E, RawKeyFlags.E0));

    // 个别键盘在 NumLock 关闭时仍上报数字键盘虚拟键，按扫描码补正后同样记录为导航键。
    Equal(KeyId.Home, KeyNormalizer.Normalize(NumpadFallback(0x47, 0x67, numLockOn: false)));
    Equal(KeyId.End, KeyNormalizer.Normalize(NumpadFallback(0x4F, 0x61, numLockOn: false)));
    Equal(KeyId.ArrowDown, KeyNormalizer.Normalize(NumpadFallback(0x50, 0x62, numLockOn: false)));
    Equal(KeyId.Delete, KeyNormalizer.Normalize(NumpadFallback(0x53, 0x6E, numLockOn: false)));
    Equal(KeyId.Insert, KeyNormalizer.Normalize(NumpadFallback(0x52, 0x60, numLockOn: false)));

    // NumLock 打开时不做补正，仍然是数字键盘键。
    Equal(KeyId.Numpad7, KeyNormalizer.Normalize(NumpadFallback(0x47, 0x67, numLockOn: true)));
    Equal(KeyId.Numpad1, KeyNormalizer.Normalize(NumpadFallback(0x4F, 0x61, numLockOn: true)));

    // 运算键不受补正影响。
    Equal(KeyId.NumpadAdd, KeyNormalizer.Normalize(NumpadFallback(0x4E, 0x6B, numLockOn: false)));
    Equal(KeyId.NumpadDivide, Normalize(0x35, 0x6F, RawKeyFlags.E0));
}

static RawKeyEvent NumpadFallback(ushort scanCode, ushort virtualKey, bool numLockOn) =>
    KeyNormalizer.ApplyNumpadNavigationFallback(
        new RawKeyEvent(scanCode, RawKeyFlags.None, virtualKey),
        numLockOn);

static void ExtendedFunctionKeysMap()
{
    Equal(KeyId.F13, Normalize(0, 0x7C));
    Equal(KeyId.F24, Normalize(0, 0x87));
}

static void MediaKeyFallbackMaps()
{
    Equal(KeyId.MediaPlayPause, Normalize(0, 0xB3, RawKeyFlags.E0));
    Equal(KeyId.VolumeUp, Normalize(0, 0xAF, RawKeyFlags.E0));
}

static void MediaVirtualKeyWinsOverScanCode()
{
    Equal(KeyId.VolumeMute, Normalize(0x22, 0xAD, RawKeyFlags.E0));
}

static void RepeatedMakeCountsOnce()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.Process(1, MakeA());
    counter.Process(1, MakeA());
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void ReleaseThenPressCountsAgain()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.Process(1, BreakA());
    counter.Process(1, MakeA());
    var snapshot = counter.GetSnapshot();
    Equal(2L, snapshot.Counts[(int)KeyId.A]);
    Equal(2L, snapshot.TotalCount);
}

static void TwoDevicesCountSeparately()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.Process(2, MakeA());
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void ChordKeysCountSeparately()
{
    var counter = new KeyboardCounter();

    // Ctrl+C：两个按下消息都必须分别计数。
    counter.Process(1, new RawKeyEvent(0x1D, RawKeyFlags.None, 0x11));
    counter.Process(1, new RawKeyEvent(0x2E, RawKeyFlags.None, 0x43));
    counter.Process(1, new RawKeyEvent(0x2E, RawKeyFlags.Break, 0x43));

    // Ctrl 保持按下时再按 V：Ctrl 仍是一次物理按下，V 另计一次。
    counter.Process(1, new RawKeyEvent(0x2F, RawKeyFlags.None, 0x56));
    counter.Process(1, new RawKeyEvent(0x2F, RawKeyFlags.Break, 0x56));
    counter.Process(1, new RawKeyEvent(0x1D, RawKeyFlags.Break, 0x11));

    var snapshot = counter.GetSnapshot();
    Equal(1L, snapshot.Counts[(int)KeyId.LeftControl]);
    Equal(1L, snapshot.Counts[(int)KeyId.C]);
    Equal(1L, snapshot.Counts[(int)KeyId.V]);
    Equal(3L, snapshot.TotalCount);
}

static void SystemSwitchReleaseCanBeReconciled()
{
    var counter = new KeyboardCounter();
    var altDown = new RawKeyEvent(0x38, RawKeyFlags.None, 0x12);
    var tabDown = new RawKeyEvent(0x0F, RawKeyFlags.None, 0x09);

    counter.Process(1, altDown);
    counter.Process(1, tabDown);

    // 模拟 Windows 接管 Alt+Tab 后，应用没有收到对应松开消息。
    counter.ReleaseKey(KeyId.LeftAlt);
    counter.ReleaseKey(KeyId.Tab);
    counter.Process(1, altDown);
    counter.Process(1, tabDown);

    var snapshot = counter.GetSnapshot();
    Equal(2L, snapshot.Counts[(int)KeyId.LeftAlt]);
    Equal(2L, snapshot.Counts[(int)KeyId.Tab]);
}

static void AltTabFallbackAndRawInputAreDeduplicated()
{
    var counter = new KeyboardCounter();
    var tabDown = new RawKeyEvent(0x0F, RawKeyFlags.None, 0x09);
    var tabUp = new RawKeyEvent(0x0F, RawKeyFlags.Break, 0x09);

    Equal(true, counter.ProcessButton(0, KeyId.Tab, isBreak: false).Counted);
    Equal(false, counter.ProcessDetailed(7, tabDown).Counted);
    counter.ProcessDetailed(7, tabUp);

    Equal(true, counter.ProcessDetailed(7, tabDown).Counted);
    Equal(false, counter.ProcessButton(0, KeyId.Tab, isBreak: false).Counted);
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.Tab]);
}

static void MouseButtonsCountSeparately()
{
    var counter = new KeyboardCounter();
    Equal(true, counter.ProcessButton(9, KeyId.MouseLeftButton, isBreak: false).Counted);
    Equal(false, counter.ProcessButton(9, KeyId.MouseLeftButton, isBreak: false).Counted);
    counter.ProcessButton(9, KeyId.MouseRightButton, isBreak: false);

    var heldSnapshot = counter.GetSnapshot();
    Equal(1L, heldSnapshot.Counts[(int)KeyId.MouseLeftButton]);
    Equal(1L, heldSnapshot.Counts[(int)KeyId.MouseRightButton]);
    Equal(true, heldSnapshot.PressedKeys.Contains(KeyId.MouseLeftButton));
    Equal(true, heldSnapshot.PressedKeys.Contains(KeyId.MouseRightButton));

    counter.ProcessButton(9, KeyId.MouseLeftButton, isBreak: true);
    counter.ProcessButton(9, KeyId.MouseLeftButton, isBreak: false);
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.MouseLeftButton]);
}

static void MouseMiddleAndSideButtonsCountSeparately()
{
    var counter = new KeyboardCounter();
    Equal(true, counter.ProcessButton(9, KeyId.MouseMiddleButton, isBreak: false).Counted);
    Equal(false, counter.ProcessButton(9, KeyId.MouseMiddleButton, isBreak: false).Counted);
    counter.ProcessButton(9, KeyId.MouseXButton1, isBreak: false);
    counter.ProcessButton(9, KeyId.MouseXButton2, isBreak: false);

    var heldSnapshot = counter.GetSnapshot();
    Equal(1L, heldSnapshot.Counts[(int)KeyId.MouseMiddleButton]);
    Equal(1L, heldSnapshot.Counts[(int)KeyId.MouseXButton1]);
    Equal(1L, heldSnapshot.Counts[(int)KeyId.MouseXButton2]);
    Equal(true, heldSnapshot.PressedKeys.Contains(KeyId.MouseMiddleButton));
    Equal(true, heldSnapshot.PressedKeys.Contains(KeyId.MouseXButton2));

    counter.ProcessButton(9, KeyId.MouseXButton1, isBreak: true);
    counter.ProcessButton(9, KeyId.MouseXButton1, isBreak: false);
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.MouseXButton1]);
}

static void WheelNotchesCountWithoutPressedState()
{
    var counter = new KeyboardCounter();
    Equal(true, counter.ProcessMomentaryEvent(KeyId.MouseWheelUp, 3).Counted);
    counter.ProcessMomentaryEvent(KeyId.MouseWheelDown);
    Equal(true, counter.ProcessMomentaryEvent(KeyId.MouseWheelDown, 2).Counted);

    var snapshot = counter.GetSnapshot();
    Equal(3L, snapshot.Counts[(int)KeyId.MouseWheelUp]);
    Equal(3L, snapshot.Counts[(int)KeyId.MouseWheelDown]);
    Equal(6L, snapshot.TotalCount);

    // 滚轮是瞬时事件，不能像按键那样停留在“按下中”。
    Equal(0, snapshot.PressedKeys.Count);
    Equal(false, counter.ProcessMomentaryEvent(KeyId.MouseWheelUp, 0).Counted);
    Equal(3L, counter.GetSnapshot().Counts[(int)KeyId.MouseWheelUp]);
}

static void WheelStopsCountingWhilePaused()
{
    var counter = new KeyboardCounter();
    counter.SetPaused(true);
    Equal(false, counter.ProcessMomentaryEvent(KeyId.MouseWheelUp).Counted);
    counter.SetPaused(false);
    Equal(true, counter.ProcessMomentaryEvent(KeyId.MouseWheelUp).Counted);
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.MouseWheelUp]);
}

static void NewKeysAreAppendedAfterExistingKeys()
{
    // 键位下标会写入数据库，新增键位只能追加在末尾，否则旧数据会整体错位。
    Equal(0, (int)KeyId.Escape);
    Equal((int)KeyId.MouseLeftButton + 1, (int)KeyId.MouseRightButton);
    Equal((int)KeyId.MouseRightButton + 1, (int)KeyId.MouseMiddleButton);
    Equal((int)KeyId.MouseMiddleButton + 1, (int)KeyId.MouseWheelUp);
    Equal((int)KeyId.MouseWheelUp + 1, (int)KeyId.MouseWheelDown);
    Equal((int)KeyId.MouseWheelDown + 1, (int)KeyId.MouseXButton1);
    Equal((int)KeyId.MouseXButton1 + 1, (int)KeyId.MouseXButton2);
    // 手柄键位追加在鼠标之后，最后一个成员是十字键右。
    Equal((int)KeyId.MouseXButton2 + 1, (int)KeyId.GamepadA);
    Equal(KeyCatalog.Count, (int)KeyId.GamepadDpadRight + 1);
    Equal(KeyCatalog.Count, KeyCatalog.Definitions.Count);
    Equal("鼠标", KeyCatalog.Get(KeyId.MouseWheelUp).Category);
    Equal("鼠标", KeyCatalog.Get(KeyId.MouseXButton1).Category);
    // XButton1 是靠后的“后退”侧键，XButton2 是靠前的“前进”侧键。
    Equal("后侧键", KeyCatalog.Get(KeyId.MouseXButton1).DisplayName);
    Equal("前侧键", KeyCatalog.Get(KeyId.MouseXButton2).DisplayName);
}

static void GamepadButtonsMapToKeyCatalog()
{
    // 手柄按键在 KeyId 末尾连续排列，且顺序与 GamepadButton 枚举一一对应。
    Equal(GamepadButtonMapper.Buttons.Count, (int)KeyId.GamepadDpadRight - (int)KeyId.GamepadA + 1);
    Equal(KeyCatalog.Count - GamepadButtonMapper.Buttons.Count, (int)KeyId.GamepadA);
    Equal(KeyCatalog.Count - 1, (int)KeyId.GamepadDpadRight);
    Equal((byte)30, GamepadButtonMapper.TriggerThreshold);
    Equal(KeyId.GamepadA, GamepadButtonMapper.ToKeyId(GamepadButton.A));
    Equal(KeyId.GamepadY, GamepadButtonMapper.ToKeyId(GamepadButton.Y));
    Equal(KeyId.GamepadLeftBumper, GamepadButtonMapper.ToKeyId(GamepadButton.LeftBumper));
    Equal(KeyId.GamepadLeftTrigger, GamepadButtonMapper.ToKeyId(GamepadButton.LeftTrigger));
    Equal(KeyId.GamepadDpadRight, GamepadButtonMapper.ToKeyId(GamepadButton.DpadRight));
    Equal(KeyId.GamepadRightStick, GamepadButtonMapper.ToKeyId(GamepadButton.RightStick));

    var seen = new HashSet<KeyId>();
    foreach (var button in GamepadButtonMapper.Buttons)
    {
        var keyId = GamepadButtonMapper.ToKeyId(button);
        Equal(true, seen.Add(keyId));
        Equal("手柄", KeyCatalog.Get(keyId).Category);
        Equal(true, KeyCatalog.Get(keyId).DisplayName.StartsWith("手柄", StringComparison.Ordinal));
    }
}

static void GamepadTriggersUseThreshold()
{
    var tracker = new GamepadButtonTracker();
    Span<GamepadButtonChange> changes = stackalloc GamepadButtonChange[GamepadButtonMapper.Buttons.Count];

    // 低于阈值不算按下。
    Equal(0, tracker.Update(new GamepadState(GamepadButtons.None, 29, 0), changes));

    // 达到阈值算一次按下。
    Equal(1, tracker.Update(new GamepadState(GamepadButtons.None, 30, 0), changes));
    Equal(GamepadButton.LeftTrigger, changes[0].Button);
    Equal(true, changes[0].IsPressed);

    // 扳机继续按到底不会重复计数。
    Equal(0, tracker.Update(new GamepadState(GamepadButtons.None, 255, 0), changes));

    // 松开左扳机同时按下右扳机。
    Equal(2, tracker.Update(new GamepadState(GamepadButtons.None, 0, 40), changes));
    Equal(GamepadButton.LeftTrigger, changes[0].Button);
    Equal(false, changes[0].IsPressed);
    Equal(GamepadButton.RightTrigger, changes[1].Button);
    Equal(true, changes[1].IsPressed);
}

static void GamepadHeldButtonCountsOnce()
{
    var tracker = new GamepadButtonTracker();
    Span<GamepadButtonChange> changes = stackalloc GamepadButtonChange[GamepadButtonMapper.Buttons.Count];
    var pressed = new GamepadState(GamepadButtons.A, 0, 0);

    Equal(1, tracker.Update(pressed, changes));
    Equal(GamepadButton.A, changes[0].Button);
    Equal(true, changes[0].IsPressed);

    // 按住不放继续采样不产生变化。
    Equal(0, tracker.Update(pressed, changes));

    // 松开。
    Equal(1, tracker.Update(GamepadState.Empty, changes));
    Equal(false, changes[0].IsPressed);

    // 再次按下重新计数。
    Equal(1, tracker.Update(pressed, changes));
    Equal(true, changes[0].IsPressed);
}

static void GamepadChordCountsSeparately()
{
    var counter = new KeyboardCounter();
    var tracker = new GamepadButtonTracker();
    Span<GamepadButtonChange> changes = stackalloc GamepadButtonChange[GamepadButtonMapper.Buttons.Count];
    // 同时按下 A、十字键上、左扳机。
    var state = new GamepadState(GamepadButtons.A | GamepadButtons.DpadUp, 200, 0);
    var changeCount = tracker.Update(state, changes);
    Equal(3, changeCount);

    for (var index = 0; index < changeCount; index++)
    {
        var change = changes[index];
        counter.ProcessButton(
            0x5F10_0000,
            GamepadButtonMapper.ToKeyId(change.Button),
            isBreak: !change.IsPressed);
    }

    var snapshot = counter.GetSnapshot();
    Equal(1L, snapshot.Counts[(int)KeyId.GamepadA]);
    Equal(1L, snapshot.Counts[(int)KeyId.GamepadDpadUp]);
    Equal(1L, snapshot.Counts[(int)KeyId.GamepadLeftTrigger]);
    Equal(3L, snapshot.TotalCount);
    Equal(3, snapshot.PressedKeys.Count);

    // 按住不放继续采样不会重复计数。
    Equal(0, tracker.Update(state, changes));
    Equal(3L, counter.GetSnapshot().TotalCount);
}

static void GamepadResetIgnoresHeldButtons()
{
    var tracker = new GamepadButtonTracker();
    Span<GamepadButtonChange> changes = stackalloc GamepadButtonChange[GamepadButtonMapper.Buttons.Count];
    var held = new GamepadState(GamepadButtons.B, 0, 0);

    Equal(1, tracker.Update(held, changes));

    // 断开连接后重置：重新连接时采集端会把首次采样当作起始状态丢弃。
    tracker.Reset();
    Equal(1, tracker.Update(held, changes));
    Equal(true, changes[0].IsPressed);
    Equal(0, tracker.Update(held, changes));
}

static void GamepadInputCountsPressOnce()
{
    var counter = new KeyboardCounter();
    var provider = new FakeGamepadProvider(1);
    var countedKeys = new List<KeyId>();
    using var source = new GamepadInputSource(
        provider,
        counter,
        (keyId, _) => countedKeys.Add(keyId),
        TestSettings.PollIntervalMilliseconds);

    // 未连接时不做任何事。
    source.PollOnce();
    Equal(0, source.ConnectedCount);
    Equal(0L, counter.GetSnapshot().TotalCount);

    // 连接时已经按住 A：只当作起始状态，不计数。
    provider.Connect(0, new GamepadState(GamepadButtons.A, 0, 0));
    source.PollOnce();
    Equal(1, source.ConnectedCount);
    Equal(0L, counter.GetSnapshot().TotalCount);

    // 按住不放继续采样。
    source.PollOnce();
    source.PollOnce();
    Equal(0L, counter.GetSnapshot().TotalCount);

    // 松开后重新按下才算一次。
    provider.SetState(0, GamepadState.Empty);
    source.PollOnce();
    provider.SetState(0, new GamepadState(GamepadButtons.A, 0, 0));
    source.PollOnce();
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.GamepadA]);
    Equal(1, countedKeys.Count);
    Equal(KeyId.GamepadA, countedKeys[0]);
    Equal(true, counter.GetSnapshot().PressedKeys.Contains(KeyId.GamepadA));
    Contains("手柄 A", source.LastInputDescription);

    // 松开后不再处于按下状态。
    provider.SetState(0, GamepadState.Empty);
    source.PollOnce();
    Equal(false, counter.GetSnapshot().PressedKeys.Contains(KeyId.GamepadA));
}

static void GamepadInputClearsStateOnDisconnect()
{
    var counter = new KeyboardCounter();
    var provider = new FakeGamepadProvider(1);
    using var source = new GamepadInputSource(provider, counter, null, TestSettings.PollIntervalMilliseconds);

    provider.Connect(0, GamepadState.Empty);
    source.PollOnce();
    provider.SetState(0, new GamepadState(GamepadButtons.B, 0, 0));
    source.PollOnce();
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.GamepadB]);
    Equal(true, counter.GetSnapshot().PressedKeys.Contains(KeyId.GamepadB));

    // 拔出后“按下中”状态必须清理。
    provider.Disconnect(0);
    source.PollOnce();
    Equal(0, source.ConnectedCount);
    Equal(0, counter.GetSnapshot().PressedKeys.Count);

    // 重新连接时仍然按住 B，不应再计一次。
    provider.Connect(0, new GamepadState(GamepadButtons.B, 0, 0));
    source.PollOnce();
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.GamepadB]);
}

static void GamepadCountsPerSlotSeparately()
{
    var counter = new KeyboardCounter();
    var provider = new FakeGamepadProvider(2);
    using var source = new GamepadInputSource(provider, counter, null, TestSettings.PollIntervalMilliseconds);

    provider.Connect(0, GamepadState.Empty);
    provider.Connect(1, GamepadState.Empty);
    source.PollOnce();
    Equal(2, source.ConnectedCount);

    // 两个手柄同时按 A，各自计一次。
    provider.SetState(0, new GamepadState(GamepadButtons.A, 0, 0));
    provider.SetState(1, new GamepadState(GamepadButtons.A, 0, 0));
    source.PollOnce();
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.GamepadA]);

    // 其中一个松开再按，仍按各自手柄计数。
    provider.SetState(1, GamepadState.Empty);
    source.PollOnce();
    provider.SetState(1, new GamepadState(GamepadButtons.A, 0, 0));
    source.PollOnce();
    Equal(3L, counter.GetSnapshot().Counts[(int)KeyId.GamepadA]);
}

static void GamepadRespectsPause()
{
    var counter = new KeyboardCounter();
    var provider = new FakeGamepadProvider(1);
    using var source = new GamepadInputSource(provider, counter, null, TestSettings.PollIntervalMilliseconds);

    provider.Connect(0, GamepadState.Empty);
    source.PollOnce();

    counter.SetPaused(true);
    provider.SetState(0, new GamepadState(GamepadButtons.X, 0, 0));
    source.PollOnce();
    Equal(0L, counter.GetSnapshot().Counts[(int)KeyId.GamepadX]);

    counter.SetPaused(false);
    provider.SetState(0, GamepadState.Empty);
    source.PollOnce();
    provider.SetState(0, new GamepadState(GamepadButtons.X, 0, 0));
    source.PollOnce();
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.GamepadX]);
}

static void GamepadFailureOnlyDisablesGamepad()
{
    var counter = new KeyboardCounter();
    var provider = new FakeGamepadProvider(1);
    using var source = new GamepadInputSource(provider, counter, null, TestSettings.PollIntervalMilliseconds);

    provider.Connect(0, GamepadState.Empty);
    source.PollOnce();
    Equal(true, source.IsAvailable);

    // 驱动异常只把手柄采集停掉，不应向上抛出。
    provider.FailNextRequest = true;
    source.PollOnce();
    Equal(false, source.IsAvailable);
    Contains("手柄采集不可用", source.LastInputDescription);
}

static void PauseMaintainsPressedState()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.SetPaused(true);
    counter.Process(1, BreakA());
    counter.Process(1, MakeA());
    counter.SetPaused(false);
    counter.Process(1, MakeA());
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.A]);
    counter.Process(1, BreakA());
    counter.Process(1, MakeA());
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void ClearDoesNotBreakPressedState()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.ClearCounts();
    counter.Process(1, MakeA());
    Equal(0L, counter.GetSnapshot().Counts[(int)KeyId.A]);
    counter.Process(1, BreakA());
    counter.Process(1, MakeA());
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void ClearCanResetPressedState()
{
    var counter = new KeyboardCounter();
    counter.Process(1, MakeA());
    counter.ClearCounts(resetPressedState: true);
    counter.Process(1, MakeA());
    Equal(1L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void RemovingDeviceClearsPressedState()
{
    var counter = new KeyboardCounter();
    counter.Process(17, MakeA());
    counter.RemoveDevice(17);
    counter.Process(17, MakeA());
    Equal(2L, counter.GetSnapshot().Counts[(int)KeyId.A]);
}

static void UnknownKeyIsIgnored()
{
    var counter = new KeyboardCounter();
    Equal(false, counter.Process(1, new RawKeyEvent(0x7F, RawKeyFlags.None, 0xFE)));
    Equal(0L, counter.GetSnapshot().TotalCount);
}

static void CountVectorRoundTrips()
{
    var counts = new uint[KeyCatalog.Count];
    counts[(int)KeyId.A] = 17;
    counts[(int)KeyId.Space] = 43;
    counts[(int)KeyId.NumpadEnter] = 2;
    counts[(int)KeyId.MouseWheelUp] = 512;
    counts[(int)KeyId.MouseWheelDown] = 3;
    counts[(int)KeyId.MouseXButton1] = 9;
    var encoded = CountVectorCodec.Encode(counts);
    var decoded = CountVectorCodec.Decode(encoded);
    Equal(counts.Length, decoded.Length);
    for (var index = 0; index < counts.Length; index++)
    {
        Equal(counts[index], decoded[index]);
    }
}

static async Task LegacyKeyMapRowsRemainReadableAsync()
{
    await WithRepositoryAsync(async (directory, repository) =>
    {
        const long minuteStart = 1_800_000_000;
        var blob = CountVectorCodec.Encode(NewCounts((KeyId.C, 4), (KeyId.LeftControl, 2)));
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "test.db"),
        }.ToString();

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO minute_buckets (
                    bucket_start_utc, keymap_version, counts, total_count, updated_at_utc)
                VALUES ($start, 1, $counts, 6, $start);
                """;
            command.Parameters.AddWithValue("$start", minuteStart);
            command.Parameters.AddWithValue("$counts", blob);
            await command.ExecuteNonQueryAsync();
        }

        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(4UL, aggregate.Counts[(int)KeyId.C]);
        Equal(2UL, aggregate.Counts[(int)KeyId.LeftControl]);
        Equal(0UL, aggregate.Counts[(int)KeyId.MouseLeftButton]);
        // 新增鼠标键位后，旧向量按原下标解码，新键位保持为 0。
        Equal(0UL, aggregate.Counts[(int)KeyId.MouseMiddleButton]);
        Equal(0UL, aggregate.Counts[(int)KeyId.MouseWheelUp]);
        Equal(0UL, aggregate.Counts[(int)KeyId.MouseXButton2]);
    });
}

static async Task LegacyKeyMapV3RowsRemainReadableAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long minuteStart = 1_800_000_000;
        var blob = CountVectorCodec.Encode(NewCounts((KeyId.GamepadA, 6), (KeyId.MouseWheelDown, 9)));
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = repository.DatabasePath,
        }.ToString();

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO minute_buckets (
                    bucket_start_utc, keymap_version, counts, total_count, updated_at_utc)
                VALUES ($start, 3, $counts, 15, $start);
                """;
            command.Parameters.AddWithValue("$start", minuteStart);
            command.Parameters.AddWithValue("$counts", blob);
            await command.ExecuteNonQueryAsync();
        }

        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(6UL, aggregate.Counts[(int)KeyId.GamepadA]);
        Equal(9UL, aggregate.Counts[(int)KeyId.MouseWheelDown]);
        Equal(0UL, aggregate.Counts[(int)KeyId.GamepadDpadRight]);
    });
}

static async Task ArchiveKeepsPreviousDataAsync()
{
    var directory = CreateTestDirectory();
    var databasePath = Path.Combine(directory, "key-stats.db");
    const long minuteStart = 1_800_000_000;
    try
    {
        using (var repository = new KeyStarRepository(databasePath))
        {
            await repository.InitializeAsync();
            await repository.UpsertMinuteBucketAsync(new MinuteBucketSnapshot(
                minuteStart,
                NewCounts((KeyId.A, 3), (KeyId.GamepadA, 5), (KeyId.MouseWheelUp, 7))));
        }

        var archivePath = await StatisticsArchive.ArchiveAsync(
            databasePath,
            DateTimeOffset.FromUnixTimeSeconds(minuteStart));

        Contains(StatisticsArchive.ArchiveDirectoryName, archivePath);
        Equal(true, File.Exists(archivePath));
        Equal(false, File.Exists(databasePath));

        // 存档内容与归档前完全一致。
        using (var archived = new KeyStarRepository(archivePath))
        {
            await archived.InitializeAsync();
            Equal("ok", await archived.CheckIntegrityAsync());
            var aggregate = await archived.QueryAggregateAsync(minuteStart, minuteStart + 60);
            Equal(15UL, aggregate.TotalCount);
            Equal(3UL, aggregate.Counts[(int)KeyId.A]);
            Equal(5UL, aggregate.Counts[(int)KeyId.GamepadA]);
            Equal(7UL, aggregate.Counts[(int)KeyId.MouseWheelUp]);
        }

        // 重置后从空库重新开始。
        using (var fresh = new KeyStarRepository(databasePath))
        {
            await fresh.InitializeAsync();
            Equal(0UL, await fresh.QueryTotalCountAsync());
            Equal(true, await fresh.GetDataRangeAsync() is null);
        }
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }
}

static async Task ArchiveNamesDoNotCollideAsync()
{
    var directory = CreateTestDirectory();
    var databasePath = Path.Combine(directory, "key-stats.db");
    var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    try
    {
        using (var repository = new KeyStarRepository(databasePath))
        {
            await repository.InitializeAsync();
        }

        var first = await StatisticsArchive.ArchiveAsync(databasePath, timestamp);

        using (var repository = new KeyStarRepository(databasePath))
        {
            await repository.InitializeAsync();
        }

        var second = await StatisticsArchive.ArchiveAsync(databasePath, timestamp);
        Equal(false, string.Equals(first, second, StringComparison.OrdinalIgnoreCase));
        Equal(true, File.Exists(first));
        Equal(true, File.Exists(second));
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RepositoryUpsertIsAbsoluteAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long minuteStart = 1_800_000_000;
        var counts = NewCounts((KeyId.A, 3));
        await repository.UpsertMinuteBucketAsync(new MinuteBucketSnapshot(minuteStart, counts));

        var loaded = await repository.GetMinuteBucketAsync(minuteStart);
        Equal(3u, loaded!.Counts[(int)KeyId.A]);

        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(minuteStart, NewCounts((KeyId.A, 5))));
        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(5UL, aggregate.TotalCount);
        Equal("ok", await repository.CheckIntegrityAsync());
    });
}

static async Task DataRangeTracksSparseBoundsAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long firstMinute = 1_800_000_000;
        const long lastMinute = firstMinute + 3_600;
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(firstMinute, NewCounts((KeyId.A, 1))));
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(lastMinute, NewCounts((KeyId.MouseLeftButton, 2))));

        var range = await repository.GetDataRangeAsync();
        Equal(firstMinute, range!.StartUtc);
        Equal(lastMinute + 60, range.EndUtc);
    });
}

static async Task TotalCountCanExcludeCurrentMinuteAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long firstMinute = 1_800_000_000;
        const long currentMinute = firstMinute + 60;
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(firstMinute, NewCounts((KeyId.A, 2))));
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(currentMinute, NewCounts((KeyId.W, 3))));

        Equal(5UL, await repository.QueryTotalCountAsync());
        Equal(2UL, await repository.QueryTotalCountAsync(currentMinute));
    });
}

static async Task TenMinuteRollupIsCorrectAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long rollupStart = 1_800_000_000;
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(rollupStart, NewCounts((KeyId.A, 2))));
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(rollupStart + 60, NewCounts((KeyId.B, 3))));

        var aggregate = await repository.QueryAggregateAsync(rollupStart, rollupStart + 600);
        Equal(5UL, aggregate.TotalCount);
        Equal(2UL, aggregate.Counts[(int)KeyId.A]);
        Equal(3UL, aggregate.Counts[(int)KeyId.B]);
    });
}

static async Task TenMinuteSeriesHandlesRangeEdgesAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        const long baseTime = 1_800_000_000;
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(baseTime + 60, NewCounts((KeyId.A, 1))));
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(baseTime + 600, NewCounts((KeyId.B, 2))));
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(baseTime + 1_200, NewCounts((KeyId.C, 3))));

        var series = await repository.QueryTenMinuteTotalsAsync(baseTime + 60, baseTime + 1_260);
        Equal(3, series.Count);
        Equal(baseTime, series[0].BucketStartUtc);
        Equal(1UL, series[0].TotalCount);
        Equal(baseTime + 600, series[1].BucketStartUtc);
        Equal(2UL, series[1].TotalCount);
        Equal(baseTime + 1_200, series[2].BucketStartUtc);
        Equal(3UL, series[2].TotalCount);
    });
}

static async Task RecorderResumesCurrentMinuteAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        var clock = new ManualClock(DateTimeOffset.FromUnixTimeSeconds(1_800_000_030));
        await using (var recorder = await StatisticsRecorder.CreateAsync(
            repository,
            TimeSpan.FromHours(1),
            clock))
        {
            recorder.RecordKeyPress(KeyId.A);
            await recorder.FlushNowAsync();
        }

        await using (var recorder = await StatisticsRecorder.CreateAsync(
            repository,
            TimeSpan.FromHours(1),
            clock))
        {
            recorder.RecordKeyPress(KeyId.A);
            await recorder.FlushNowAsync();
        }

        var minuteStart = TimeBuckets.ToMinuteStart(clock.UtcNow);
        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(2UL, aggregate.TotalCount);
    });
}

static async Task GamepadPressesPersistThroughRecorderAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        var clock = new ManualClock(DateTimeOffset.FromUnixTimeSeconds(1_800_000_030));
        var counter = new KeyboardCounter();
        var provider = new FakeGamepadProvider(1);
        await using var recorder = await StatisticsRecorder.CreateAsync(repository, TimeSpan.FromHours(1), clock);
        using var source = new GamepadInputSource(
            provider,
            counter,
            recorder.RecordKeyPress,
            TestSettings.PollIntervalMilliseconds);

        // 连接 → 按下 A、RB、LT → 全部松开 → 再按一次 A。
        provider.Connect(0, GamepadState.Empty);
        source.PollOnce();
        provider.SetState(0, new GamepadState(GamepadButtons.A | GamepadButtons.RightBumper, 100, 0));
        source.PollOnce();
        provider.SetState(0, GamepadState.Empty);
        source.PollOnce();
        provider.SetState(0, new GamepadState(GamepadButtons.A, 0, 0));
        source.PollOnce();
        await recorder.FlushNowAsync();

        var minuteStart = TimeBuckets.ToMinuteStart(clock.UtcNow);
        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(2UL, aggregate.Counts[(int)KeyId.GamepadA]);
        Equal(1UL, aggregate.Counts[(int)KeyId.GamepadRightBumper]);
        Equal(1UL, aggregate.Counts[(int)KeyId.GamepadLeftTrigger]);
        Equal(4UL, aggregate.TotalCount);
    });
}

static async Task WheelNotchesPersistThroughRecorderAsync()
{
    await WithRepositoryAsync(async (_, repository) =>
    {
        var clock = new ManualClock(DateTimeOffset.FromUnixTimeSeconds(1_800_000_030));
        await using (var recorder = await StatisticsRecorder.CreateAsync(
            repository,
            TimeSpan.FromHours(1),
            clock))
        {
            // 一次滚轮事件可能包含多个刻度，必须按刻度数落库。
            recorder.RecordKeyPress(KeyId.MouseWheelDown, 3);
            recorder.RecordKeyPress(KeyId.MouseXButton1);
            await recorder.FlushNowAsync();
        }

        var minuteStart = TimeBuckets.ToMinuteStart(clock.UtcNow);
        var aggregate = await repository.QueryAggregateAsync(minuteStart, minuteStart + 60);
        Equal(3UL, aggregate.Counts[(int)KeyId.MouseWheelDown]);
        Equal(1UL, aggregate.Counts[(int)KeyId.MouseXButton1]);
        Equal(4UL, aggregate.TotalCount);
    });
}

static async Task CsvExportContainsMinuteRowsAsync()
{
    await WithRepositoryAsync(async (directory, repository) =>
    {
        const long minuteStart = 1_800_000_000;
        await repository.UpsertMinuteBucketAsync(
            new MinuteBucketSnapshot(
                minuteStart,
                NewCounts((KeyId.Space, 4), (KeyId.MouseWheelDown, 7))));
        var csvPath = Path.Combine(directory, "export.csv");
        await repository.ExportCsvAsync(minuteStart, minuteStart + 60, csvPath);
        var csv = await File.ReadAllTextAsync(csvPath);
        Contains("minute_start_local", csv);
        Contains("Space", csv);
        Contains(",4", csv);
        Contains("MouseWheelDown", csv);
        Contains("滚轮下滚", csv);
        Contains(",7", csv);
    });
}

static async Task SettingsStoreRoundTripsAsync()
{
    var directory = CreateTestDirectory();
    try
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var settings = await store.LoadOrCreateAsync();
        Equal(30, settings.FlushIntervalSeconds);
        settings.FlushIntervalSeconds = 45;
        settings.WindowLeft = 120.5;
        settings.WindowTop = -40;
        settings.WindowWidth = 1440;
        settings.WindowHeight = 900;
        settings.WindowMaximized = true;
        await store.SaveAsync(settings);
        Equal(45, (await store.LoadOrCreateAsync()).FlushIntervalSeconds);

        // 启动时用同步读取恢复窗口位置，内容必须一致。
        var reloaded = store.LoadOrCreate();
        Equal(45, reloaded.FlushIntervalSeconds);
        Equal(1440d, reloaded.WindowWidth!.Value);
        Equal(900d, reloaded.WindowHeight!.Value);
        Equal(120.5d, reloaded.WindowLeft!.Value);
        Equal(-40d, reloaded.WindowTop!.Value);
        Equal(true, reloaded.WindowMaximized);

        // 窗口尺寸变化时走同步保存。
        reloaded.WindowWidth = 1600;
        store.Save(reloaded);
        Equal(1600d, store.LoadOrCreate().WindowWidth!.Value);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task WithRepositoryAsync(Func<string, KeyStarRepository, Task> test)
{
    var directory = CreateTestDirectory();
    try
    {
        using var repository = new KeyStarRepository(Path.Combine(directory, "test.db"));
        await repository.InitializeAsync();
        await test(directory, repository);
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }
}

static string CreateTestDirectory()
{
    var directory = Path.Combine(Path.GetTempPath(), "KeyStar.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
}

static uint[] NewCounts(params (KeyId Key, uint Count)[] values)
{
    var counts = new uint[KeyCatalog.Count];
    foreach (var (key, count) in values)
    {
        counts[(int)key] = count;
    }

    return counts;
}

static void Contains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"没有找到期望文本：{expected}");
    }
}

static KeyId? Normalize(ushort scanCode, ushort virtualKey, RawKeyFlags flags = RawKeyFlags.None) =>
    KeyNormalizer.Normalize(new RawKeyEvent(scanCode, flags, virtualKey));

static RawKeyEvent MakeA() => new(0x1E, RawKeyFlags.None, 0x41);
static RawKeyEvent BreakA() => new(0x1E, RawKeyFlags.Break, 0x41);

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
    }
}

file sealed class ManualClock(DateTimeOffset utcNow) : IUtcClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

file static class TestSettings
{
    // 测试用超长轮询间隔：定时器在测试期间不会自己触发，采样全部由 PollOnce 驱动。
    public const int PollIntervalMilliseconds = 3_600_000;
}

// 可编排的手柄提供程序，用来在没有真实硬件的情况下验证采集链路。
file sealed class FakeGamepadProvider(int slotCount) : IGamepadProvider
{
    private readonly bool[] _connected = new bool[slotCount];
    private readonly GamepadState[] _states = new GamepadState[slotCount];

    public int SlotCount => slotCount;

    public bool FailNextRequest { get; set; }

    public void Connect(int slot, GamepadState state)
    {
        _connected[slot] = true;
        _states[slot] = state;
    }

    public void Disconnect(int slot) => _connected[slot] = false;

    public void SetState(int slot, GamepadState state) => _states[slot] = state;

    public bool TryGetState(int slot, out GamepadState state)
    {
        if (FailNextRequest)
        {
            FailNextRequest = false;
            throw new InvalidOperationException("测试用手柄异常。");
        }

        state = _states[slot];
        return _connected[slot];
    }
}
