using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GitVersion.Configuration;
using GitVersion.Extensions;

namespace GitVersion.VersionCalculation;

public class IncrementStrategyFinder : IIncrementStrategyFinder
{
    public const string DefaultMajorPattern = @"\+semver:\s?(breaking|major)";
    public const string DefaultMinorPattern = @"\+semver:\s?(feature|minor)";
    public const string DefaultPatchPattern = @"\+semver:\s?(fix|patch)";
    public const string DefaultNoBumpPattern = @"\+semver:\s?(none|skip)";

    private static readonly ConcurrentDictionary<string, Regex> CompiledRegexCache = new();
    private readonly Dictionary<string, VersionField?> commitIncrementCache = new();
    private readonly Dictionary<string, Dictionary<string, int>> headCommitsMapCache = new();
    private readonly Dictionary<string, ICommit[]> headCommitsCache = new();

    private static readonly Regex DefaultMajorPatternRegex = new(DefaultMajorPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefaultMinorPatternRegex = new(DefaultMinorPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefaultPatchPatternRegex = new(DefaultPatchPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefaultNoBumpPatternRegex = new(DefaultNoBumpPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IGitRepository repository;

    public IncrementStrategyFinder(IGitRepository repository) => this.repository = repository.NotNull();

    public VersionField[] DetermineIncrementedFields(
        ICommit? currentCommit, BaseVersion baseVersion, EffectiveConfiguration configuration)
    {
        baseVersion.NotNull();
        configuration.NotNull();

        return DetermineIncrementedFieldsInternal(currentCommit, baseVersion, configuration).ToArray();
    }

    private IEnumerable<VersionField> DetermineIncrementedFieldsInternal(
        ICommit? currentCommit, BaseVersion baseVersion, EffectiveConfiguration configuration)
    {
        var commitMessageIncrements = FindCommitMessageIncrements(
            configuration, baseVersion.BaseVersionSource, currentCommit
        ).ToArray();

        var defaultIncrement = configuration.Increment.ToVersionField();

        // use the default branch configuration increment strategy if there are no commit message overrides
        if (commitMessageIncrements.Length == 0)
        {
            yield return baseVersion.ShouldIncrement ? defaultIncrement : VersionField.None;
            yield break;
        }

        if (baseVersion.ShouldIncrement && defaultIncrement != VersionField.None)
        {
            yield return defaultIncrement;
        }

        // var maximalIncrement = baseVersion.ShouldIncrement ? defaultIncrement : VersionField.Major;
        foreach (var commitMessageIncrement in commitMessageIncrements)
        {
            // don't increment for less than the branch configuration increment, if the absence of commit messages would have
            // still resulted in an increment of configuration.Increment
            //if (commitMessageIncrement <= maximalIncrement)
            {
                yield return commitMessageIncrement;
            }
        }
    }

    private IEnumerable<VersionField> GetIncrementForCommitsInternal(
        string? majorVersionBumpMessage, string? minorVersionBumpMessage,
        string? patchVersionBumpMessage, string? noBumpMessage, IEnumerable<ICommit> commits)
    {
        commits.NotNull();

        var majorRegex = TryGetRegexOrDefault(majorVersionBumpMessage, DefaultMajorPatternRegex);
        var minorRegex = TryGetRegexOrDefault(minorVersionBumpMessage, DefaultMinorPatternRegex);
        var patchRegex = TryGetRegexOrDefault(patchVersionBumpMessage, DefaultPatchPatternRegex);
        var none = TryGetRegexOrDefault(noBumpMessage, DefaultNoBumpPatternRegex);

        return commits
            .Select(c => GetIncrementFromCommit(c, majorRegex, minorRegex, patchRegex, none))
            .Where(v => v != null).Select(el => el!.Value);
    }

    public VersionField? GetIncrementForCommits(string? majorVersionBumpMessage, string? minorVersionBumpMessage,
        string? patchVersionBumpMessage, string? noBumpMessage, IEnumerable<ICommit> commits)
    {
        commits.NotNull();

        var majorRegex = TryGetRegexOrDefault(majorVersionBumpMessage, DefaultMajorPatternRegex);
        var minorRegex = TryGetRegexOrDefault(minorVersionBumpMessage, DefaultMinorPatternRegex);
        var patchRegex = TryGetRegexOrDefault(patchVersionBumpMessage, DefaultPatchPatternRegex);
        var none = TryGetRegexOrDefault(noBumpMessage, DefaultNoBumpPatternRegex);

        var increments = commits
            .Select(c => GetIncrementFromCommit(c, majorRegex, minorRegex, patchRegex, none))
            .Where(v => v != null)
            .ToList();

        return increments.Any()
            ? increments.Max()
            : null;
    }

    private IEnumerable<VersionField> FindCommitMessageIncrements(
        EffectiveConfiguration configuration, ICommit? baseCommit, ICommit? currentCommit)
    {
        if (configuration.CommitMessageIncrementing == CommitMessageIncrementMode.Disabled)
        {
            return Enumerable.Empty<VersionField>();
        }

        var commits = GetIntermediateCommits(baseCommit, currentCommit);

        // consider commit messages since latest tag only (see #3071)
        var tags = new HashSet<string?>(repository.Tags.Select(t => t.TargetSha));
        commits = commits
            .Reverse()
            .TakeWhile(x => !tags.Contains(x.Sha))
            .Reverse();

        if (configuration.CommitMessageIncrementing == CommitMessageIncrementMode.MergeMessageOnly)
        {
            commits = commits.Where(c => c.Parents.Count() > 1);
        }

        return GetIncrementForCommitsInternal(
            majorVersionBumpMessage: configuration.MajorVersionBumpMessage,
            minorVersionBumpMessage: configuration.MinorVersionBumpMessage,
            patchVersionBumpMessage: configuration.PatchVersionBumpMessage,
            noBumpMessage: configuration.NoBumpMessage,
            commits: commits
        );
    }

    private static Regex TryGetRegexOrDefault(string? messageRegex, Regex defaultRegex) =>
        messageRegex == null
            ? defaultRegex
            : CompiledRegexCache.GetOrAdd(messageRegex, pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));

    /// <summary>
    /// Get the sequence of commits in a repository between a <paramref name="baseCommit"/> (exclusive)
    /// and a particular <paramref name="headCommit"/> (inclusive)
    /// </summary>
    private IEnumerable<ICommit> GetIntermediateCommits(ICommit? baseCommit, ICommit? headCommit)
    {
        var map = GetHeadCommitsMap(headCommit);

        var commitAfterBaseIndex = 0;
        if (baseCommit != null)
        {
            if (!map.TryGetValue(baseCommit.Sha, out var baseIndex)) return Enumerable.Empty<ICommit>();
            commitAfterBaseIndex = baseIndex + 1;
        }

        var headCommits = GetHeadCommits(headCommit);
        return new ArraySegment<ICommit>(headCommits, commitAfterBaseIndex, headCommits.Length - commitAfterBaseIndex);
    }

    /// <summary>
    /// Get a mapping of commit shas to their zero-based position in the sequence of commits from the beginning of a
    /// repository to a particular <paramref name="headCommit"/>
    /// </summary>
    private Dictionary<string, int> GetHeadCommitsMap(ICommit? headCommit) =>
        this.headCommitsMapCache.GetOrAdd(headCommit?.Sha ?? "NULL", () =>
            GetHeadCommits(headCommit)
                .Select((commit, index) => (commit.Sha, Index: index))
                .ToDictionary(t => t.Sha, t => t.Index));

    /// <summary>
    /// Get the sequence of commits from the beginning of a repository to a particular
    /// <paramref name="headCommit"/> (inclusive)
    /// </summary>
    private ICommit[] GetHeadCommits(ICommit? headCommit) =>
        this.headCommitsCache.GetOrAdd(headCommit?.Sha ?? "NULL", () =>
            GetCommitsReacheableFromHead(repository, headCommit).ToArray());

    private VersionField? GetIncrementFromCommit(ICommit commit, Regex majorRegex, Regex minorRegex, Regex patchRegex, Regex none) =>
        this.commitIncrementCache.GetOrAdd(commit.Sha, () =>
            GetIncrementFromMessage(commit.Message, majorRegex, minorRegex, patchRegex, none));

    private static VersionField? GetIncrementFromMessage(string message, Regex majorRegex, Regex minorRegex, Regex patchRegex, Regex none)
    {
        if (none.IsMatch(message)) return VersionField.None;
        if (majorRegex.IsMatch(message)) return VersionField.Major;
        if (minorRegex.IsMatch(message)) return VersionField.Minor;
        if (patchRegex.IsMatch(message)) return VersionField.Patch;
        return null;
    }

    /// <summary>
    /// Query a <paramref name="repo"/> for the sequence of commits from the beginning to a particular
    /// <paramref name="headCommit"/> (inclusive)
    /// </summary>
    private static IEnumerable<ICommit> GetCommitsReacheableFromHead(IGitRepository repo, ICommit? headCommit)
    {
        var filter = new CommitFilter
        {
            IncludeReachableFrom = headCommit,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse
        };

        return repo.Commits.QueryBy(filter);
    }
}
