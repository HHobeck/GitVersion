namespace GitVersion.Configuration;

public interface IPathFilterConfiguration
{
    IReadOnlySet<string> Exclude { get; }

    IReadOnlySet<string> Include { get; }
}
