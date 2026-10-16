using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyStar.App.Input;
using KeyStar.App.Controls;
using KeyStar.Core;
using KeyStar.Storage;
using Microsoft.Win32;

namespace KeyStar.App;

public partial class MainWindow : Window
{
    private readonly KeyboardCounter _counter = new();
    private readonly ObservableCollection<KeyCountRow> _rows;
    private readonly ICollectionView _rowsView;
    private readonly List<HeatmapControlBase> _heatmaps;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly StartupRegistrationService _startupRegistration;
    private readonly SettingsStore _settingsStore;
    private AppSettings _settings;
    private TrayIconService? _trayIcon;
    private RawInputKeyboardSource? _rawInput;
    private GamepadInputSource? _gamepad;
    private KeyStarRepository? _repository;
    private StatisticsRecorder? _recorder;
    private DateTimeOffset _lastStorageUiRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastHeatmapUiRefresh = DateTimeOffset.MinValue;
    private bool _storageRefreshRunning;
    private bool _heatmapRefreshRunning;
    private bool _isClosing;
    private bool _closeCompleted;
    private bool _rangeControlsReady;
    private bool _startupSettingChanging;
    private bool _windowPlacementDirty;
    private DateTimeOffset _windowPlacementChangedUtc;
    private bool _settingsLoaded;
    private string _activeRangePreset = "All";
    private (long StartUtc, long EndUtc)? _lastCustomRange;
    private (long StartUtc, long EndUtc)? _selectedRange;
    private ulong[] _selectedCounts = new ulong[KeyCatalog.Count];
    private ulong[] _selectedStoredCounts = new ulong[KeyCatalog.Count];
    private ulong[] _selectedStoredTimeCounts = new ulong[TenMinuteTimelineControl.SegmentCount];
    private uint[]? _lastLiveCounts;
    private long? _selectedLiveBucketStart;
    private HashSet<KeyId> _lastVisualPressed = [];

    public MainWindow()
    {
        InitializeComponent();

        _settingsStore = new SettingsStore(Path.Combine(DataDirectoryPath, "settings.json"));
        _settings = LoadSettingsOrDefault();
        ApplySavedWindowPlacement();

        _startupRegistration = new StartupRegistrationService(
            Environment.ProcessPath ?? throw new InvalidOperationException("无法确定 KeyStar 可执行文件路径。"));
        InitializeStartupSetting();
        _trayIcon = new TrayIconService(
            () => Dispatcher.BeginInvoke(ShowFromTray),
            () => Dispatcher.BeginInvoke(TogglePauseFromTray),
            () => Dispatcher.BeginInvoke(SaveFromTray),
            () => Dispatcher.BeginInvoke(ExitFromTray));

        _rows = new ObservableCollection<KeyCountRow>(
            KeyCatalog.Definitions.Select(definition => new KeyCountRow(definition)));
        _rowsView = CollectionViewSource.GetDefaultView(_rows);
        _rowsView.SortDescriptions.Add(new SortDescription(nameof(KeyCountRow.DisplayOrder), ListSortDirection.Ascending));
        _rowsView.Filter = FilterRow;
        KeyGrid.ItemsSource = _rowsView;

        InitializeRangeEditors();
        _rangeControlsReady = true;
        SetCustomRangeEnabled(isCustom: false);
        _heatmaps = [HeatmapKeyboard, HeatmapGamepad];
        foreach (var heatmap in _heatmaps)
        {
            heatmap.SetData(_selectedCounts, new HashSet<KeyId>(), HeatScaleMode.SquareRoot);
        }

        DailyTimeline.SetData(_selectedStoredTimeCounts);

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        SizeChanged += MainWindow_PlacementChanged;
        LocationChanged += MainWindow_PlacementChanged;
        StateChanged += MainWindow_PlacementChanged;
    }

    private AppSettings LoadSettingsOrDefault()
    {
        try
        {
            var settings = _settingsStore.LoadOrCreate();
            _settingsLoaded = true;
            return settings;
        }
        catch (Exception)
        {
            // 设置文件不可用时先用默认值且暂不回写，避免把默认值覆盖到用户设置上；
            // 初始化存储时会再尝试一次加载。
            _settingsLoaded = false;
            return new AppSettings();
        }
    }

    // 恢复上次的窗口大小和位置；数值不可用或超出屏幕时退回默认大小并居中。
    private void ApplySavedWindowPlacement()
    {
        if (_settings.WindowWidth is not { } width || _settings.WindowHeight is not { } height)
        {
            return;
        }

        if (width < MinWidth || height < MinHeight || width > 20000 || height > 20000)
        {
            return;
        }

        Width = width;
        Height = height;

        if (_settings.WindowLeft is { } left && _settings.WindowTop is { } top &&
            IsPlacementVisible(left, top, width, height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            CenterWindow(width, height);
        }

        if (_settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void CenterWindow(double width, double height)
    {
        var workArea = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Left + ((workArea.Width - width) / 2);
        Top = workArea.Top + ((workArea.Height - height) / 2);
    }

    // 至少要有一块区域落在虚拟屏幕内，避免换显示器或改分辨率后窗口跑到看不见的地方。
    private static bool IsPlacementVisible(double left, double top, double width, double height)
    {
        var visible = new Rect(left, top, width, height);
        visible.Intersect(new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight));
        return visible.Width >= 160 && visible.Height >= 80;
    }

    private void MainWindow_PlacementChanged(object? sender, EventArgs eventArgs)
    {
        _windowPlacementDirty = true;
        _windowPlacementChangedUtc = DateTimeOffset.UtcNow;
    }

    private void CaptureWindowPlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width < MinWidth || bounds.Height < MinHeight)
        {
            return;
        }

        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
    }

    // 拖动窗口时会连续触发变化事件，停顿 1 秒后再写文件。
    private void PersistWindowPlacementIfIdle()
    {
        if (_windowPlacementDirty &&
            DateTimeOffset.UtcNow - _windowPlacementChangedUtc >= TimeSpan.FromSeconds(1))
        {
            PersistWindowPlacement();
        }
    }

    private void PersistWindowPlacement()
    {
        if (!_windowPlacementDirty || !_settingsLoaded)
        {
            return;
        }

        CaptureWindowPlacement();
        try
        {
            _settingsStore.Save(_settings);
            _windowPlacementDirty = false;
        }
        catch (Exception)
        {
            // 保存窗口位置失败不影响统计，隐藏或退出时会再试一次。
        }
    }

    private async void MainWindow_SourceInitialized(object? sender, EventArgs eventArgs)
    {
        try
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            _rawInput = new RawInputKeyboardSource(windowHandle, _counter, OnCountedKey);
            _refreshTimer.Start();
        }
        catch (Exception exception)
        {
            SetErrorStatus();
            LastInputText.Text = $"采集启动失败：{exception.Message}";
            MessageBox.Show(
                this,
                $"无法启动键盘采集。\n\n{exception.Message}",
                "KeyStar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        StartGamepadCapture();

        try
        {
            await InitializeStorageAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_isClosing)
        {
        }
        catch (Exception exception)
        {
            if (!_isClosing)
            {
                SetStorageError(exception.Message);
            }
        }
    }

    // 键盘、鼠标和手柄共用同一个落盘回调。
    private void OnCountedKey(KeyId keyId, int count) => _recorder?.RecordKeyPress(keyId, count);

    private void StartGamepadCapture()
    {
        try
        {
            _gamepad = new GamepadInputSource(new XInputGamepadProvider(), _counter, OnCountedKey);
        }
        catch (Exception exception)
        {
            // 手柄采集不可用时键盘和鼠标仍然正常工作。
            _gamepad = null;
            GamepadStatusText.Text = $"手柄：采集不可用（{exception.Message}）";
        }
    }

    private string BuildGamepadStatusText()
    {
        if (_gamepad is null)
        {
            return GamepadStatusText.Text;
        }

        if (!_gamepad.IsAvailable)
        {
            return $"手柄：采集不可用（{_gamepad.LastInputDescription}）";
        }

        return _gamepad.ConnectedCount == 0
            ? "手柄：未连接"
            : $"手柄：已连接 {_gamepad.ConnectedCount} 个 · {_gamepad.LastInputDescription}";
    }

    private void RefreshTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _rawInput?.ReconcileSystemShortcutState();
        var snapshot = _counter.GetSnapshot();
        TotalCountText.Text = snapshot.TotalCount.ToString("N0");

        foreach (var row in _rows)
        {
            row.IsPressed = snapshot.PressedKeys.Contains(row.Id);
        }

        var selectedCountsChanged = RefreshLiveSelectedCounts();
        if (selectedCountsChanged || !_lastVisualPressed.SetEquals(snapshot.PressedKeys))
        {
            _lastVisualPressed = snapshot.PressedKeys.ToHashSet();
            RenderHeatmap(snapshot.PressedKeys);
        }

        PressedKeysText.Text = snapshot.PressedKeys.Count == 0
            ? "无"
            : string.Join("  +  ", snapshot.PressedKeys
                .OrderBy(key => (int)key)
                .Select(key => KeyCatalog.Get(key).DisplayName));

        if (_rawInput is not null)
        {
            LastInputText.Text = _rawInput.LastInputDescription;
            UnknownInputText.Text = _rawInput.LastUnrecognizedDescription is { } unknown
                ? $"未识别输入：{_rawInput.UnrecognizedMakeCount:N0} · 最近 {unknown}"
                : $"未识别输入：{_rawInput.UnrecognizedMakeCount:N0}";
        }

        var gamepadText = BuildGamepadStatusText();
        if (!string.Equals(GamepadStatusText.Text, gamepadText, StringComparison.Ordinal))
        {
            GamepadStatusText.Text = gamepadText;
        }

        if (ActiveOnlyCheckBox.IsChecked == true)
        {
            _rowsView.Refresh();
        }

        if (DateTimeOffset.UtcNow - _lastStorageUiRefresh >= TimeSpan.FromSeconds(2))
        {
            _ = RefreshStorageUiAsync();
        }

        PersistWindowPlacementIfIdle();
    }

    private async void RangePresetRadio_Checked(object sender, RoutedEventArgs eventArgs)
    {
        if (!_rangeControlsReady || sender is not RadioButton { Tag: string preset })
        {
            return;
        }

        _activeRangePreset = preset;
        var isCustom = preset == "Custom";
        SetCustomRangeEnabled(isCustom);
        RangeValidationText.Text = string.Empty;
        if (isCustom)
        {
            // 优先恢复上一次确认过的自定义区间，其次使用最近 30 天作为起点。
            SetRangeEditorValues(_lastCustomRange ?? GetDefaultCustomRange());
        }

        if (!isCustom)
        {
            await ApplyActivePresetAsync();
        }
    }

    private async void ApplyRangeButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (StartDatePicker.SelectedDate is not { } startDate || EndDatePicker.SelectedDate is not { } endDate)
            {
                throw new InvalidOperationException("请选择开始和结束日期。");
            }

            if (!TimeSpan.TryParseExact(StartTimeTextBox.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var startTime) ||
                !TimeSpan.TryParseExact(EndTimeTextBox.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var endTime))
            {
                throw new InvalidOperationException("时间格式应为 HH:mm，例如 09:30。");
            }

            var startLocal = DateTime.SpecifyKind(startDate.Date + startTime, DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind(endDate.Date + endTime, DateTimeKind.Unspecified);

            var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, TimeZoneInfo.Local), TimeSpan.Zero);
            var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, TimeZoneInfo.Local), TimeSpan.Zero);
            var range = (TimeBuckets.ToMinuteStart(startUtc), TimeBuckets.ToMinuteStart(endUtc));
            if (range.Item2 <= range.Item1)
            {
                throw new InvalidOperationException("结束时间必须晚于开始时间。");
            }

            _activeRangePreset = "Custom";
            _selectedRange = range;
            _lastCustomRange = range;
            RangeValidationText.Text = string.Empty;
            await RefreshSelectedRangeAsync(force: true);
        }
        catch (Exception exception) when (!_isClosing)
        {
            RangeValidationText.Text = exception.Message;
        }
    }

    private void ScaleModeRadio_Checked(object sender, RoutedEventArgs eventArgs)
    {
        if (_rangeControlsReady)
        {
            RenderHeatmap(_counter.GetSnapshot().PressedKeys);
        }
    }

    private void InitializeRangeEditors()
    {
        SetRangeEditorValues(GetDefaultCustomRange());
    }

    private static (long StartUtc, long EndUtc) GetDefaultCustomRange()
    {
        // 默认展示最近 30 天，用户可在此基础上调整。
        var end = DateTime.Now;
        var start = end.Date.AddDays(-30);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(start, DateTimeKind.Unspecified), TimeZoneInfo.Local), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(end, DateTimeKind.Unspecified), TimeZoneInfo.Local), TimeSpan.Zero);
        return (TimeBuckets.ToMinuteStart(startUtc), TimeBuckets.ToMinuteStart(endUtc));
    }

    private void SetCustomRangeEnabled(bool isCustom)
    {
        CustomRangePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ApplyActivePresetAsync()
    {
        var range = await ResolvePresetRangeAsync(_activeRangePreset);
        _selectedRange = range;
        SetRangeEditorValues(range);
        await RefreshSelectedRangeAsync(force: true);
    }

    private async Task<(long StartUtc, long EndUtc)> ResolvePresetRangeAsync(string preset)
    {
        var currentMinute = TimeBuckets.ToMinuteStart(DateTimeOffset.UtcNow);
        var endUtc = currentMinute + TimeBuckets.MinuteSeconds;
        return preset switch
        {
            "All" => await ResolveAllRangeAsync(currentMinute, endUtc),
            _ => GetTodayRangeUtc(),
        };
    }

    private async Task<(long StartUtc, long EndUtc)> ResolveAllRangeAsync(long currentMinute, long currentEnd)
    {
        var storedRange = _repository is null
            ? null
            : await _repository.GetDataRangeAsync(_lifetimeCancellation.Token);
        return storedRange is null
            ? (currentMinute, currentEnd)
            : (Math.Min(storedRange.StartUtc, currentMinute), Math.Max(storedRange.EndUtc, currentEnd));
    }

    private void SetRangeEditorValues((long StartUtc, long EndUtc) range)
    {
        var startLocal = DateTimeOffset.FromUnixTimeSeconds(range.StartUtc).ToLocalTime();
        var endLocal = DateTimeOffset.FromUnixTimeSeconds(range.EndUtc).ToLocalTime();
        StartDatePicker.SelectedDate = startLocal.Date;
        StartTimeTextBox.Text = startLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndDatePicker.SelectedDate = endLocal.Date;
        EndTimeTextBox.Text = endLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private HeatScaleMode GetScaleMode() =>
        ScaleModeLinearRadio.IsChecked == true
            ? HeatScaleMode.Linear
            : HeatScaleMode.SquareRoot;

    private void RenderHeatmap(IReadOnlySet<KeyId> pressedKeys)
    {
        var scaleMode = GetScaleMode();
        foreach (var heatmap in _heatmaps)
        {
            heatmap.SetData(_selectedCounts, pressedKeys, scaleMode);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        SetPaused(!_counter.IsPaused);
    }

    private void SetPaused(bool shouldPause)
    {
        _counter.SetPaused(shouldPause);
        _trayIcon?.SetPaused(shouldPause);

        if (shouldPause)
        {
            PauseButton.Content = "恢复采集";
            StatusText.Text = "已暂停";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 84));
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 180, 84));
            StatusBadge.Background = new SolidColorBrush(Color.FromRgb(42, 31, 12));
        }
        else
        {
            PauseButton.Content = "暂停采集";
            SetCollectingStatus();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        var result = MessageBox.Show(
            this,
            "只清空本次运行计数。已经保存的历史数据不会删除。\n\n确定继续吗？",
            "确认清零本次显示",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            Keyboard.ClearFocus();
            return;
        }

        _counter.ClearCounts(resetPressedState: true);
        _rawInput?.ClearDiagnostics();
        Keyboard.ClearFocus();
        RefreshTimer_Tick(sender, EventArgs.Empty);
    }

    private async void SaveNowButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        if (_recorder is null)
        {
            return;
        }

        SaveNowButton.IsEnabled = false;
        try
        {
            await _recorder.FlushNowAsync(_lifetimeCancellation.Token);
            await RefreshStorageUiAsync(force: true);
        }
        catch (Exception exception) when (!_isClosing)
        {
            SetStorageError(exception.Message);
        }
        finally
        {
            if (!_isClosing)
            {
                SaveNowButton.IsEnabled = _recorder is not null;
            }
        }
    }

    private async void ResetButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_recorder is null || _repository is null)
        {
            Keyboard.ClearFocus();
            return;
        }

        var result = MessageBox.Show(
            this,
            "重置记录会新建一个空存档继续统计。\n\n" +
            $"当前数据会完整归档到 Data\\{StatisticsArchive.ArchiveDirectoryName} 目录，" +
            "不会删除，也不会丢失任何历史记录。\n\n确定重置吗？",
            "重置记录（新建存档）",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            Keyboard.ClearFocus();
            return;
        }

        ResetButton.IsEnabled = false;
        try
        {
            // 先落盘并关闭连接，才能安全地移动数据库文件。
            await _recorder.DisposeAsync();
            _recorder = null;
            _repository.Dispose();
            _repository = null;

            var archivePath = await StatisticsArchive.ArchiveAsync(
                DatabasePath,
                DateTimeOffset.Now,
                _lifetimeCancellation.Token);

            _counter.ClearCounts(resetPressedState: true);
            _rawInput?.ClearDiagnostics();
            _gamepad?.ResetDiagnostics();

            await InitializeStorageAsync(_lifetimeCancellation.Token);
            await RefreshStorageUiAsync(force: true);
            RefreshTimer_Tick(sender, EventArgs.Empty);

            MessageBox.Show(
                this,
                $"已新建存档并继续记录。\n\n上一份数据已归档到：\n{archivePath}",
                "重置记录完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception) when (!_isClosing)
        {
            MessageBox.Show(
                this,
                $"重置失败：\n\n{exception.Message}",
                "KeyStar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            try
            {
                // 尽量恢复到可记录状态，避免重置失败后完全停止统计。
                if (_recorder is null)
                {
                    await InitializeStorageAsync(_lifetimeCancellation.Token);
                }
            }
            catch (Exception recoveryException) when (!_isClosing)
            {
                SetStorageError(recoveryException.Message);
            }
        }
        finally
        {
            if (!_isClosing)
            {
                ResetButton.IsEnabled = _repository is not null;
            }
        }

        Keyboard.ClearFocus();
    }

    private void InitializeStartupSetting()
    {
        _startupSettingChanging = true;
        try
        {
            StartupCheckBox.IsChecked = _startupRegistration.IsEnabledForCurrentExecutable();
        }
        catch (Exception exception)
        {
            StartupCheckBox.IsChecked = false;
            StartupCheckBox.ToolTip = $"读取启动项失败：{exception.Message}";
        }
        finally
        {
            _startupSettingChanging = false;
        }
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs eventArgs)
    {
        if (_startupSettingChanging)
        {
            return;
        }

        var requestedState = StartupCheckBox.IsChecked == true;
        try
        {
            _startupRegistration.SetEnabled(requestedState);
            StartupCheckBox.ToolTip = requestedState
                ? "已为当前 Windows 用户启用；程序将从当前绿色版路径静默启动。"
                : "未启用开机自启动。";
        }
        catch (Exception exception)
        {
            _startupSettingChanging = true;
            StartupCheckBox.IsChecked = !requestedState;
            _startupSettingChanging = false;
            MessageBox.Show(
                this,
                $"无法修改 Windows 启动项。\n\n{exception.Message}",
                "KeyStar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_recorder is null || _repository is null || _selectedRange is not { } selectedRange)
        {
            return;
        }

        var rangeStartLocal = DateTimeOffset.FromUnixTimeSeconds(selectedRange.StartUtc).ToLocalTime();
        var rangeEndLocal = DateTimeOffset.FromUnixTimeSeconds(selectedRange.EndUtc).ToLocalTime();

        var dialog = new SaveFileDialog
        {
            Title = "导出所选时间范围的分钟聚合",
            Filter = "CSV 文件 (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = ".csv",
            FileName = $"key-stats-{rangeStartLocal:yyyyMMdd-HHmm}-{rangeEndLocal:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ExportButton.IsEnabled = false;
        try
        {
            await _recorder.FlushNowAsync(_lifetimeCancellation.Token);
            await _repository.ExportCsvAsync(
                selectedRange.StartUtc,
                selectedRange.EndUtc,
                dialog.FileName,
                _lifetimeCancellation.Token);
            MessageBox.Show(this, "CSV 导出完成。", "KeyStar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (!_isClosing)
        {
            MessageBox.Show(this, $"导出失败：\n\n{exception.Message}", "KeyStar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (!_isClosing)
            {
                ExportButton.IsEnabled = _repository is not null;
            }
        }
    }

    private async Task InitializeStorageAsync(CancellationToken cancellationToken)
    {
        if (!_settingsLoaded)
        {
            // 构造时同步读取失败才会走到这里；成功时直接复用内存中的设置，
            // 避免把窗口尺寸等尚未写回的改动丢掉。
            _settings = await _settingsStore.LoadOrCreateAsync(cancellationToken);
            _settingsLoaded = true;
        }

        var settings = _settings;

        var repository = new KeyStarRepository(DatabasePath);
        try
        {
            await repository.InitializeAsync(cancellationToken);
            var recorder = await StatisticsRecorder.CreateAsync(
                repository,
                TimeSpan.FromSeconds(settings.FlushIntervalSeconds),
                cancellationToken: cancellationToken);

            if (_isClosing)
            {
                await recorder.DisposeAsync();
                repository.Dispose();
                return;
            }

            _repository = repository;
            _recorder = recorder;
            SaveNowButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
            await RefreshStorageUiAsync(force: true);
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    private async Task RefreshStorageUiAsync(bool force = false)
    {
        if (_storageRefreshRunning || _repository is null)
        {
            return;
        }

        if (!force && DateTimeOffset.UtcNow - _lastStorageUiRefresh < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _storageRefreshRunning = true;
        try
        {
            var liveSnapshot = _recorder?.GetCurrentSnapshot();
            var storedTotal = await _repository.QueryTotalCountAsync(
                liveSnapshot?.BucketStartUtc,
                _lifetimeCancellation.Token);
            var totalCount = checked(storedTotal + (ulong)(liveSnapshot?.TotalCount ?? 0));
            TotalHistoryText.Text = $"总数量：{totalCount:N0}";

            var savedText = _recorder?.LastSavedUtc is { } savedUtc
                ? savedUtc.ToLocalTime().ToString("HH:mm:ss")
                : "尚未保存";
            var sizeMegabytes = _repository.GetStorageSizeBytes() / 1024d / 1024d;
            var errorText = _recorder?.LastError is { Length: > 0 } error
                ? $" · 最近错误：{error}"
                : string.Empty;
            StorageStatusText.Text = $"本地数据：Data\\key-stats.db · 上次保存 {savedText} · {sizeMegabytes:F2} MB{errorText}";
            _lastStorageUiRefresh = DateTimeOffset.UtcNow;
            if (_activeRangePreset != "Custom")
            {
                _selectedRange = await ResolvePresetRangeAsync(_activeRangePreset);
                SetRangeEditorValues(_selectedRange.Value);
            }

            await RefreshSelectedRangeAsync(force);
        }
        catch (OperationCanceledException) when (_isClosing)
        {
        }
        catch (Exception exception)
        {
            if (!_isClosing)
            {
                SetStorageError(exception.Message);
            }
        }
        finally
        {
            _storageRefreshRunning = false;
        }
    }

    private async Task RefreshSelectedRangeAsync(bool force = false)
    {
        if (_heatmapRefreshRunning || _repository is null || _selectedRange is not { } range)
        {
            return;
        }

        if (!force && _lastHeatmapUiRefresh != DateTimeOffset.MinValue)
        {
            return;
        }

        _heatmapRefreshRunning = true;
        try
        {
            var storedCounts = new ulong[KeyCatalog.Count];
            var storedTimeCounts = new ulong[TenMinuteTimelineControl.SegmentCount];
            var liveSnapshot = _recorder?.GetCurrentSnapshot();
            if (liveSnapshot is not null &&
                liveSnapshot.BucketStartUtc >= range.StartUtc &&
                liveSnapshot.BucketStartUtc < range.EndUtc)
            {
                await AccumulateStoredRangeAsync(range.StartUtc, liveSnapshot.BucketStartUtc, storedCounts);
                await AccumulateStoredTimeRangeAsync(range.StartUtc, liveSnapshot.BucketStartUtc, storedTimeCounts);
                await AccumulateStoredRangeAsync(
                    liveSnapshot.BucketStartUtc + TimeBuckets.MinuteSeconds,
                    range.EndUtc,
                    storedCounts);
                await AccumulateStoredTimeRangeAsync(
                    liveSnapshot.BucketStartUtc + TimeBuckets.MinuteSeconds,
                    range.EndUtc,
                    storedTimeCounts);
                _selectedLiveBucketStart = liveSnapshot.BucketStartUtc;
                _lastLiveCounts = (uint[])liveSnapshot.Counts.Clone();
            }
            else
            {
                await AccumulateStoredRangeAsync(range.StartUtc, range.EndUtc, storedCounts);
                await AccumulateStoredTimeRangeAsync(range.StartUtc, range.EndUtc, storedTimeCounts);
                _selectedLiveBucketStart = null;
                _lastLiveCounts = null;
            }

            _selectedStoredCounts = storedCounts;
            _selectedStoredTimeCounts = storedTimeCounts;
            var displayCounts = (ulong[])storedCounts.Clone();
            var displayTimeCounts = (ulong[])storedTimeCounts.Clone();
            if (liveSnapshot is not null && _selectedLiveBucketStart == liveSnapshot.BucketStartUtc)
            {
                AddCounts(displayCounts, liveSnapshot.Counts);
                AddLiveTimeCount(displayTimeCounts, liveSnapshot);
            }

            ApplySelectedCounts(displayCounts, displayTimeCounts);
            var pressedKeys = _counter.GetSnapshot().PressedKeys;
            RenderHeatmap(pressedKeys);
            HeatmapRangeText.Text = FormatRange(range);
            HeatmapUpdatedText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
            _lastHeatmapUiRefresh = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) when (_isClosing)
        {
        }
        catch (Exception exception)
        {
            if (!_isClosing)
            {
                RangeValidationText.Text = $"查询失败：{exception.Message}";
            }
        }
        finally
        {
            _heatmapRefreshRunning = false;
        }
    }

    private bool RefreshLiveSelectedCounts()
    {
        if (_recorder is null || _selectedRange is not { } range || _selectedLiveBucketStart is null)
        {
            return false;
        }

        var liveSnapshot = _recorder.GetCurrentSnapshot();
        if (liveSnapshot.BucketStartUtc != _selectedLiveBucketStart)
        {
            _ = RefreshStorageUiAsync(force: true);
            return false;
        }

        if (liveSnapshot.BucketStartUtc < range.StartUtc || liveSnapshot.BucketStartUtc >= range.EndUtc ||
            (_lastLiveCounts is not null && _lastLiveCounts.AsSpan().SequenceEqual(liveSnapshot.Counts)))
        {
            return false;
        }

        _lastLiveCounts = (uint[])liveSnapshot.Counts.Clone();
        var displayCounts = (ulong[])_selectedStoredCounts.Clone();
        var displayTimeCounts = (ulong[])_selectedStoredTimeCounts.Clone();
        AddCounts(displayCounts, liveSnapshot.Counts);
        AddLiveTimeCount(displayTimeCounts, liveSnapshot);
        ApplySelectedCounts(displayCounts, displayTimeCounts);
        return true;
    }

    private void ApplySelectedCounts(ulong[] counts, ulong[] timeCounts)
    {
        _selectedCounts = counts;
        var totalCount = counts.Aggregate(0UL, (sum, count) => checked(sum + count));
        HeatmapTotalText.Text = $"所选范围总计：{totalCount:N0}";
        GamepadTotalText.Text = $"手柄按键总计：{SumGamepadCounts(counts):N0}";
        foreach (var row in _rows)
        {
            row.Count = counts[(int)row.Id];
        }

        if (ActiveOnlyCheckBox.IsChecked == true)
        {
            _rowsView.Refresh();
        }

        DailyTimeline.SetData(timeCounts);
        var peakCount = timeCounts.Max();
        if (peakCount == 0)
        {
            DailyTimelineSummaryText.Text = "暂无输入";
        }
        else
        {
            var peakIndex = Array.IndexOf(timeCounts, peakCount);
            var startMinutes = peakIndex * 10;
            var endMinutes = startMinutes + 10;
            DailyTimelineSummaryText.Text =
                $"峰值 {FormatTimelineTime(startMinutes)}—{FormatTimelineTime(endMinutes)} · {peakCount:N0} 次";
        }
    }

    private static ulong SumGamepadCounts(IReadOnlyList<ulong> counts)
    {
        var total = 0UL;
        for (var index = (int)KeyId.GamepadA; index <= (int)KeyId.GamepadDpadRight; index++)
        {
            total = checked(total + counts[index]);
        }

        return total;
    }

    private static void AddLiveTimeCount(ulong[] destination, MinuteBucketSnapshot snapshot)
    {
        var localTime = DateTimeOffset.FromUnixTimeSeconds(snapshot.BucketStartUtc).ToLocalTime();
        var index = (localTime.Hour * 6) + (localTime.Minute / 10);
        destination[index] = checked(destination[index] + (ulong)snapshot.TotalCount);
    }

    private static string FormatTimelineTime(int totalMinutes) =>
        totalMinutes == 24 * 60
            ? "24:00"
            : $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";

    private async Task AccumulateStoredRangeAsync(long startUtc, long endUtc, ulong[] destination)
    {
        if (_repository is null || startUtc >= endUtc)
        {
            return;
        }

        var aggregate = await _repository.QueryAggregateAsync(startUtc, endUtc, _lifetimeCancellation.Token);
        AddCounts(destination, aggregate.Counts);
    }

    private async Task AccumulateStoredTimeRangeAsync(long startUtc, long endUtc, ulong[] destination)
    {
        if (_repository is null || startUtc >= endUtc)
        {
            return;
        }

        var series = await _repository.QueryTenMinuteTotalsAsync(
            startUtc,
            endUtc,
            _lifetimeCancellation.Token);
        foreach (var bucket in series)
        {
            var localTime = DateTimeOffset.FromUnixTimeSeconds(bucket.BucketStartUtc).ToLocalTime();
            var index = (localTime.Hour * 6) + (localTime.Minute / 10);
            destination[index] = checked(destination[index] + bucket.TotalCount);
        }
    }

    private static void AddCounts(ulong[] destination, IReadOnlyList<ulong> source)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = checked(destination[index] + source[index]);
        }
    }

    private static void AddCounts(ulong[] destination, IReadOnlyList<uint> source)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = checked(destination[index] + source[index]);
        }
    }

    private static string FormatRange((long StartUtc, long EndUtc) range)
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(range.StartUtc).ToLocalTime();
        var end = DateTimeOffset.FromUnixTimeSeconds(range.EndUtc).ToLocalTime();
        return $"{start:yyyy-MM-dd HH:mm} — {end:yyyy-MM-dd HH:mm}（结束不含）";
    }

    private static (long StartUtc, long EndUtc) GetTodayRangeUtc()
    {
        var localStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.Today, TimeZoneInfo.Local);
        var startUtc = TimeBuckets.ToMinuteStart(new DateTimeOffset(localStart, TimeSpan.Zero));
        var currentMinute = TimeBuckets.ToMinuteStart(DateTimeOffset.UtcNow);
        return (startUtc, currentMinute + TimeBuckets.MinuteSeconds);
    }

    private void FilterChanged(object sender, RoutedEventArgs eventArgs) => _rowsView.Refresh();

    private bool FilterRow(object item)
    {
        if (ActiveOnlyCheckBox?.IsChecked != true)
        {
            return true;
        }

        return item is KeyCountRow row && (row.Count > 0 || row.IsPressed);
    }

    private void SetCollectingStatus()
    {
        StatusText.Text = "正在采集";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(63, 191, 111));
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(63, 191, 111));
        StatusBadge.Background = new SolidColorBrush(Color.FromRgb(15, 42, 29));
    }

    private void SetErrorStatus()
    {
        StatusText.Text = "启动失败";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(252, 61, 33));
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(252, 61, 33));
        StatusBadge.Background = new SolidColorBrush(Color.FromRgb(42, 16, 12));
    }

    private void SetStorageError(string message)
    {
        TotalHistoryText.Text = "总数量：不可用";
        StorageStatusText.Text = $"本地数据错误：{message}";
        SaveNowButton.IsEnabled = false;
        ExportButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
    }

    private static string DataDirectoryPath => Path.Combine(AppContext.BaseDirectory, "Data");

    private static string DatabasePath => Path.Combine(DataDirectoryPath, "key-stats.db");

    public void ShowFromTray()
    {
        if (_isClosing)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void TogglePauseFromTray() => SetPaused(!_counter.IsPaused);

    private async void SaveFromTray()
    {
        await SaveNowAsync();
    }

    private async void ExitFromTray()
    {
        await ExitApplicationAsync(confirm: true);
    }

    public async void ExitFromExternalRequest()
    {
        await ExitApplicationAsync(confirm: false);
    }

    private async Task ExitApplicationAsync(bool confirm)
    {
        if (_isClosing || _closeCompleted)
        {
            return;
        }

        if (confirm)
        {
            var message = "完整退出后将停止键盘和鼠标采集。\n\n确定退出 KeyStar 吗？";
            var result = IsVisible
                ? MessageBox.Show(this, message, "完整退出 KeyStar", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                : MessageBox.Show(message, "完整退出 KeyStar", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _isClosing = true;
        PersistWindowPlacement();
        IsEnabled = false;
        _refreshTimer.Stop();
        _rawInput?.Dispose();
        _rawInput = null;
        _gamepad?.Dispose();
        _gamepad = null;
        _lifetimeCancellation.Cancel();

        try
        {
            if (_recorder is not null)
            {
                await _recorder.DisposeAsync();
            }
        }
        catch
        {
            // 退出阶段无法安全恢复界面；已经提交的 SQLite 事务仍保持一致。
        }

        _recorder = null;
        _repository?.Dispose();
        _repository = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _closeCompleted = true;
        Application.Current.Shutdown();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (_closeCompleted)
        {
            return;
        }

        PersistWindowPlacement();
        eventArgs.Cancel = true;
        Hide();
        _trayIcon?.ShowHiddenNotification();
    }

    private void MainWindow_Closed(object? sender, EventArgs eventArgs)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _lifetimeCancellation.Dispose();
    }
}
