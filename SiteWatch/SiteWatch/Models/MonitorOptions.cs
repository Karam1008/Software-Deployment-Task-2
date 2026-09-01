namespace SiteWatch.Models;

public class MonitorOptions
{
    public const string SectionName = "SiteWatch";

    public int TimeoutSeconds { get; set; } = 10;
    public string? AllowedOrigin { get; set; }
    public List<EndpointConfig> Endpoints { get; set; } = new();
}

public class EndpointConfig
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}