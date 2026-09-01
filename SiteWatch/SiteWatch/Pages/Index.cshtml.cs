using Microsoft.AspNetCore.Mvc.RazorPages;
using SiteWatch.Models;
using SiteWatch.Services;

namespace SiteWatch.Pages;

public class IndexModel : PageModel
{
    private readonly MonitorService _monitor;
    public IndexModel(MonitorService monitor) => _monitor = monitor;

    public List<CheckResult> Results { get; private set; } = new();
    public List<CheckResult> History { get; private set; } = new();
    public string Backend => _monitor.UsingTableStorage ? "Azure Table Storage" : "In-memory";

    public async Task OnGetAsync()
    {
        Results = await _monitor.RunChecksAsync();
        History = await _monitor.HistoryAsync(20);
    }
}