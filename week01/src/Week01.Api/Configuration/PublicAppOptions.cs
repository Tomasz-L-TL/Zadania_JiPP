namespace Week01.Api.Configuration;

public sealed class PublicAppOptions
{
    public const string SectionName = "App";

    public string DisplayName { get; set; } = "Week 01 API";
    public string SupportEmail { get; set; } = "help@example.com";
    public bool MaintenanceMode { get; set; }
}
