using System.Diagnostics;

public class ServerMonitorService
{
    private readonly DateTime _startTime;
    private long _totalRequests;
    private readonly object _lockObject = new object();

    public ServerMonitorService()
    {
        _startTime = DateTime.UtcNow;
        _totalRequests = 0;
    }

    public void IncrementRequests()
    {
        lock (_lockObject)
        {
            _totalRequests++;
        }
    }

    public dynamic GetServerStats()
    {
        var process = Process.GetCurrentProcess();

        return new
        {
            // Время работы
            StartTime = _startTime,
            Uptime = DateTime.UtcNow - _startTime,

            // Память
            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
            VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,

            // Процессор
            CpuTime = process.TotalProcessorTime,
            UserProcessorTime = process.UserProcessorTime,

            // Процесс
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            ProcessId = process.Id,

            // Статистика приложения
            TotalRequests = _totalRequests,

            // Системная информация
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            CurrentTime = DateTime.UtcNow
        };
    }
}