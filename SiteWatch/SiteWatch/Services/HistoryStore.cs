using Azure;
using Azure.Data.Tables;
using SiteWatch.Models;

namespace SiteWatch.Services;

public class HistoryStore
{
    private readonly TableClient? _table;
    private readonly List<CheckResult> _memory = new();
    private readonly ILogger<HistoryStore> _log;
    private readonly object _lock = new();

    public bool UsingTableStorage => _table is not null;

    public HistoryStore(IConfiguration config, ILogger<HistoryStore> log)
    {
        _log = log;
        var cs = config.GetConnectionString("Storage");

        if (string.IsNullOrWhiteSpace(cs))
        {
            _log.LogWarning("No storage connection string configured; using in-memory history.");
            return;
        }

        try
        {
            var service = new TableServiceClient(cs);
            service.CreateTableIfNotExists("CheckHistory");
            _table = service.GetTableClient("CheckHistory");
            _log.LogInformation("Azure Table Storage history store initialised.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Table Storage init failed; falling back to in-memory history.");
        }
    }

    public async Task SaveAsync(CheckResult r)
    {
        if (_table is null)
        {
            lock (_lock)
            {
                _memory.Insert(0, r);
                if (_memory.Count > 50) _memory.RemoveAt(_memory.Count - 1);
            }
            return;
        }

        var entity = new TableEntity(r.Name, $"{DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks}-{Guid.NewGuid():N}")
        {
            ["Url"] = r.Url,
            ["StatusCode"] = r.StatusCode,
            ["ResponseTimeMs"] = r.ResponseTimeMs,
            ["IsUp"] = r.IsUp,
            ["Error"] = r.Error ?? "",
            ["CheckedAtUtc"] = r.CheckedAtUtc
        };

        await _table.UpsertEntityAsync(entity);
    }

    public async Task<List<CheckResult>> RecentAsync(int take = 20)
    {
        if (_table is null)
        {
            lock (_lock) return _memory.Take(take).ToList();
        }

        var results = new List<CheckResult>();
        AsyncPageable<TableEntity> query = _table.QueryAsync<TableEntity>(maxPerPage: take);

        await foreach (var e in query)
        {
            results.Add(new CheckResult
            {
                Name = e.PartitionKey,
                Url = e.GetString("Url") ?? "",
                StatusCode = e.GetInt32("StatusCode") ?? 0,
                ResponseTimeMs = e.GetInt64("ResponseTimeMs") ?? 0,
                IsUp = e.GetBoolean("IsUp") ?? false,
                Error = e.GetString("Error"),
                CheckedAtUtc = e.GetDateTimeOffset("CheckedAtUtc") ?? DateTimeOffset.MinValue
            });
            if (results.Count >= take) break;
        }

        return results.OrderByDescending(r => r.CheckedAtUtc).ToList();
    }

    public async Task<bool> IsReachableAsync()
    {
        if (_table is null) return true;
        try
        {
            await foreach (var _ in _table.QueryAsync<TableEntity>(maxPerPage: 1)) break;
            return true;
        }
        catch { return false; }
    }
}