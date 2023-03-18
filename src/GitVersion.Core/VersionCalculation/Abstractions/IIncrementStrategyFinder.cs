using GitVersion.Configuration;

namespace GitVersion.VersionCalculation;

public interface IIncrementStrategyFinder
{
    VersionField[] DetermineIncrementedFields(
        ICommit? currentCommit, BaseVersion baseVersion, EffectiveConfiguration configuration
    );

    VersionField? GetIncrementForCommits(
        string? majorVersionBumpMessage, string? minorVersionBumpMessage, string? patchVersionBumpMessage, string? noBumpMessage,
        IEnumerable<ICommit> commits
    );
}
