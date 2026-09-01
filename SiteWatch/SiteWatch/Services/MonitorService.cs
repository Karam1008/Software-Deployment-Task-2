using System.Diagnostics;
using Microsoft.Extensions.Options;
using SiteWatch.Models;

namespace SiteWatch.Services;

public class MonitorService
{
    private readonly IHttpClientFactory _factory;
    private readonly MonitorOptions _options;
    private readonly HistoryStore _history;
    private readonly ILogger<MonitorService> _log;

    public MonitorService(IHttpClientFactory factory, IOptions<MonitorOptions> options,
                          HistoryStore history, ILogger<MonitorService> log)
    {
        _factory = factory;
        _options = options.Value;
        _history = history;
        _log = log;
    }

    public async Task<List<CheckResult>> RunChecksAsync(CancellationToken ct = default)
    {
        var results = new List<CheckResult>();

        foreach (var ep in _options.Endpoints)
        {
            var result = new CheckResult { Name = ep.Name, Url = ep.Url };
            var sw = Stopwatch.StartNew();

            try
            {
                var client = _factory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SiteWatch/1.0 (SWE40006)");

                using var response = await client.GetAsync(ep.Url, ct);
                sw.Stop();

                result.StatusCode = (int)response.StatusCode;
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
                result.IsUp = response.IsSuccessStatusCode;

                _log.LogInformation("Checked {Name} ({Url}) -> {Status} in {Ms}ms",
                    ep.Name, ep.Url, result.StatusCode, result.ResponseTimeMs);
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
                result.IsUp = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
                _log.LogWarning(ex, "Check failed for {Name} ({Url})", ep.Name, ep.Url);
            }

            results.Add(result);
            await _history.SaveAsync(result);
        }

        return results;
    }

    public Task<List<CheckResult>> HistoryAsync(int take = 20) => _history.RecentAsync(take);
    public bool UsingTableStorage => _history.UsingTableStorage;
}