using GitVersion.Attributes;

namespace GitVersion.Configuration;

internal record IgnoreConfiguration : IIgnoreConfiguration
{
    [JsonIgnore]
    public DateTimeOffset? Before { get; init; }

    [JsonPropertyName("commits-before")]
    [JsonPropertyDescription("Commits before this date will be ignored. Format: yyyy-MM-ddTHH:mm:ss.")]
    [JsonPropertyFormat(Format.DateTime)]
    public string? BeforeString
    {
        get => Before?.ToString("yyyy-MM-ddTHH:mm:ssZ");
        init => Before = value is null ? null : DateTimeOffset.Parse(value);
    }

    [JsonIgnore]
    IReadOnlyCollection<string> IIgnoreConfiguration.Shas => Shas;

    [JsonPropertyName("sha")]
    [JsonPropertyDescription("A sequence of SHAs to be excluded from the version calculations.")]
    public HashSet<string> Shas { get; init; } = new();

    [JsonPropertyName("paths")]
    [JsonPropertyDescription("A path filter to include or exclude parts of the repository.")]
    public IPathFilterConfiguration PathFilters { get; init; } = new PathFilterConfiguration();
}
