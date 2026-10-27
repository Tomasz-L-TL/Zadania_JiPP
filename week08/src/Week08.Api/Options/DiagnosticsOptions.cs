namespace Week08.Api.Options;

public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    // A real local storage dependency rather than a synthetic always-healthy probe.
    public string StorageDirectory { get; set; } = "data";
}
