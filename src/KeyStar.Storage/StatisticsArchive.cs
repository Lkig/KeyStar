using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KeyStar.Storage;

// 重置记录时把当前数据库整份移动到存档目录，不删除任何历史数据。
public static class StatisticsArchive
{
    public const string ArchiveDirectoryName = "archives";

    public static string GetArchiveDirectory(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath))
            ?? throw new InvalidOperationException("数据库路径缺少父目录。");
        return Path.Combine(directory, ArchiveDirectoryName);
    }

    // 归档当前数据库并返回存档文件的完整路径；调用前必须关闭所有连接。
    public static async Task<string> ArchiveAsync(
        string databasePath,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(databasePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到当前数据库文件。", fullPath);
        }

        // 先把 WAL 合并回主库，保证存档是完整且紧凑的一份数据。
        await CheckpointAsync(fullPath, cancellationToken);
        var integrity = await QueryIntegrityAsync(fullPath, cancellationToken);
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"存档前的完整性检查失败，未移动任何文件：{integrity}");
        }

        var archiveDirectory = GetArchiveDirectory(fullPath);
        Directory.CreateDirectory(archiveDirectory);
        var destination = CreateArchivePath(archiveDirectory, timestamp);

        // 主库和 WAL/SHM 一起改名，避免留下不完整的一份存档。
        File.Move(fullPath, destination);
        MoveSidecarFile(fullPath + "-wal", destination + "-wal");
        MoveSidecarFile(fullPath + "-shm", destination + "-shm");
        return destination;
    }

    public static string CreateArchivePath(string archiveDirectory, DateTimeOffset timestamp)
    {
        var baseName = $"key-stats-{timestamp.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        var candidate = Path.Combine(archiveDirectory, baseName + ".db");
        for (var suffix = 1; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(archiveDirectory, $"{baseName}-{suffix}.db");
        }

        return candidate;
    }

    private static async Task CheckpointAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<string> QueryIntegrityAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture)
            ?? string.Empty;
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        // 不使用连接池，确保方法返回前文件句柄已经释放，可以被移动或改名。
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void MoveSidecarFile(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
    }
}
