namespace SiteWatch.Models;

public class CheckResult
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public int StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool IsUp { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CheckedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}