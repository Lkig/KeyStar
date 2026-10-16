using System.Text;
using System.Text.Json;

namespace KeyStar.Storage;

public sealed class SettingsStore(string settingsPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string SettingsPath { get; } = Path.GetFullPath(settingsPath);

    public async Task<AppSettings> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        await using var stream = File.OpenRead(SettingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("设置文件为空或格式不正确。");
        settings.Validate();
        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("设置文件缺少父目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    // 主窗口需要在构造时同步拿到窗口尺寸，否则会先按默认大小显示再跳变。
    public AppSettings LoadOrCreate()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new InvalidDataException("设置文件为空或格式不正确。");
        settings.Validate();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("设置文件缺少父目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: false))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush();
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
