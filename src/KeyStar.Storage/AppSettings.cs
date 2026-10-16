namespace KeyStar.Storage;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public int FlushIntervalSeconds { get; set; } = 30;

    public bool CountKeyRepeat { get; set; }

    public int? RetentionDays { get; set; }

    // 主窗口上次的大小与位置（设备独立像素）。为空表示使用默认大小和居中原位。
    // 这些字段不参与 Validate，数值不可用时界面会退回默认值，避免脏数据让设置整体不可读。
    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public bool WindowMaximized { get; set; }

    public void Validate()
    {
        if (Version != 1)
        {
            throw new InvalidDataException($"不支持的设置版本：{Version}。");
        }

        if (FlushIntervalSeconds is < 5 or > 300)
        {
            throw new InvalidDataException("FlushIntervalSeconds 必须在 5～300 秒之间。");
        }

        if (RetentionDays is <= 0)
        {
            throw new InvalidDataException("RetentionDays 必须为空或大于 0。");
        }
    }
}
