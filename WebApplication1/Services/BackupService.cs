using System.Diagnostics;

public class BackupService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public BackupService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    private string FindPgDumpPath()
    {
        // Возможные пути к pg_dump
        var possiblePaths = new[]
        {
            @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\13\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\12\bin\pg_dump.exe",
            @"pg_dump", // Если добавлен в PATH
            @"C:\PostgreSQL\bin\pg_dump.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (System.IO.File.Exists(path))
                return path;

            // Проверяем через which/where команду
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "pg_dump",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output) && System.IO.File.Exists(output.Trim()))
                    return output.Trim();
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        throw new Exception("pg_dump не найден. Убедитесь, что PostgreSQL установлен и добавлен в PATH.");
    }

    public async Task<string> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        var backupDir = Path.Combine(_environment.WebRootPath, "backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
        var filePath = Path.Combine(backupDir, fileName);

        var pgDumpPath = FindPgDumpPath();

        var processInfo = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-h {connectionStringBuilder.Host} -p {connectionStringBuilder.Port} " +
                       $"-U {connectionStringBuilder.Username} -d {connectionStringBuilder.Database} " +
                       $"-f \"{filePath}\" -w",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        processInfo.Environment["PGPASSWORD"] = connectionStringBuilder.Password;

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new Exception("Не удалось запустить процесс pg_dump");

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return fileName;
        else
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Backup failed (exit code: {process.ExitCode}): {error}");
        }
    }

    public List<string> GetBackupFiles()
    {
        var backupDir = Path.Combine(_environment.WebRootPath, "backups");
        if (!Directory.Exists(backupDir))
            return new List<string>();

        return Directory.GetFiles(backupDir, "*.sql")
            .Select(Path.GetFileName)
            .OrderDescending()
            .ToList();
    }

    public async Task RestoreBackupAsync(string fileName)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        var backupDir = Path.Combine(_environment.WebRootPath, "backups");
        var filePath = Path.Combine(backupDir, fileName);

        // Дропаем все соединения к БД
        var dropConnectionsSql = $@"
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE pg_stat_activity.datname = '{connectionStringBuilder.Database}'
            AND pid <> pg_backend_pid();";

        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        using var cmd = new Npgsql.NpgsqlCommand(dropConnectionsSql, conn);
        await cmd.ExecuteNonQueryAsync();
        await conn.CloseAsync();

        // Восстанавливаем бэкап
        var processInfo = new ProcessStartInfo
        {
            FileName = "psql",
            Arguments = $"-h {connectionStringBuilder.Host} -p {connectionStringBuilder.Port} " +
                       $"-U {connectionStringBuilder.Username} -d {connectionStringBuilder.Database} " +
                       $"-f \"{filePath}\" -w",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        processInfo.Environment["PGPASSWORD"] = connectionStringBuilder.Password;

        using var process = Process.Start(processInfo);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Restore failed: {await process.StandardError.ReadToEndAsync()}");
    }

    public long GetBackupSize(string fileName)
    {
        var backupDir = Path.Combine(_environment.WebRootPath, "backups");
        var filePath = Path.Combine(backupDir, fileName);

        if (System.IO.File.Exists(filePath))
            return new FileInfo(filePath).Length;

        return 0;
    }
}