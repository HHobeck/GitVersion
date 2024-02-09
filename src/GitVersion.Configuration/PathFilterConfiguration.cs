using GitVersion.Attributes;

namespace GitVersion.Configuration;

public record PathFilterConfiguration : IPathFilterConfiguration
{
    [JsonPropertyName("exclude")]
    [JsonPropertyDescription("A path excluded from the version calculations.")]
    public HashSet<string> Exclude { get; init; } = new();

    [JsonIgnore]
    IReadOnlySet<string> IPathFilterConfiguration.Exclude => Exclude;

    [JsonPropertyName("include")]
    [JsonPropertyDescription("A path included from the version calculations.")]
    public HashSet<string> Include { get; init; } = new();

    [JsonIgnore]
    IReadOnlySet<string> IPathFilterConfiguration.Include => Include;
}
