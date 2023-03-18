using GitVersion.Configuration;
using GitVersion.Core.Tests.Helpers;
using GitVersion.VersionCalculation;
using LibGit2Sharp;

namespace GitVersion.Core.Tests.IntegrationTests;

public class XenoLibPackages : TestBase
{
    private GitVersionConfiguration GetConfiguration()
    {
        var configuration = GitFlowConfigurationBuilder.New
            .WithBranch("feature", _ => _
                .WithVersioningMode(VersioningMode.ContinuousDeployment)
                .WithIncrement(IncrementStrategy.Minor)
            ).WithBranch("pull-request", _ => _
                .WithIncrement(IncrementStrategy.None)
                .WithRegularExpression(@"^(pull|pull\-requests|pr)[/-]")
                .WithTrackMergeTarget(true)
            ).Build();
        return configuration;
    }

    [Test, Ignore("")]
    public void IncrementFeatureByMinor()
    {
        var configuration = GetConfiguration();

        using var fixture = new EmptyRepositoryFixture("main");

        fixture.MakeATaggedCommit("0.1.0");
        fixture.BranchTo("feature/foo", "foo");
        fixture.MakeACommit();
        fixture.AssertFullSemver("0.2.0-foo.1", configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("0.2.0-foo.2", configuration);
        fixture.Checkout("main");
        fixture.MergeNoFF("feature/foo");
        fixture.AssertFullSemver("0.2.0", configuration); // 0.1.1+3
    }

    [Test, Ignore("")]
    public void CanCalculatePullRequestChanges()
    {
        var configuration = GetConfiguration();

        using var fixture = new EmptyRepositoryFixture();

        fixture.MakeATaggedCommit("0.1.0");
        fixture.CreateBranch("feature/foo");
        fixture.MakeACommit();
        fixture.AssertFullSemver("0.2.0-foo.1", configuration);
        fixture.Repository.CreatePullRequestRef("feature/foo", MainBranch, normalise: true);

        fixture.AssertFullSemver("0.2.0-PullRequest0002.2"); // 0.1.1-PullRequest2.2
    }
}

[TestFixture]
public class OtherBranchScenarios : TestBase
{
    [Test]
    public void VerifyManuallyIncrementingVersion()
    {
        var configuration = GitFlowConfigurationBuilder.New
            .WithBranch("develop", _ => _
                .WithCommitMessageIncrementing(CommitMessageIncrementMode.Enabled)
                .WithVersioningMode(VersioningMode.ContinuousDelivery)
            ).Build();

        using var fixture = new EmptyRepositoryFixture();

        fixture.MakeACommit("1");

        fixture.BranchTo("develop");
        fixture.MakeACommit("+semver: fix");

        fixture.AssertFullSemver("0.1.1-alpha.1+2", configuration);

        fixture.MakeACommit("+semver: fix");

        fixture.AssertFullSemver("0.1.2-alpha.1+3", configuration);
    }

    [Test]
    public void CanTakeVersionFromReleaseBranch()
    {
        using var fixture = new EmptyRepositoryFixture();
        const string taggedVersion = "1.0.3";
        fixture.Repository.MakeATaggedCommit(taggedVersion);
        fixture.Repository.MakeCommits(5);
        fixture.Repository.CreateBranch("release/beta-2.0.0");
        Commands.Checkout(fixture.Repository, "release/beta-2.0.0");

        fixture.AssertFullSemver("2.0.0-beta.1+0");
    }

    [Test]
    public void BranchesWithIllegalCharsShouldNotBeUsedInVersionNames()
    {
        using var fixture = new EmptyRepositoryFixture();
        const string taggedVersion = "1.0.3";
        fixture.Repository.MakeATaggedCommit(taggedVersion);
        fixture.Repository.MakeCommits(5);
        fixture.Repository.CreateBranch("issue/m/github-569");
        Commands.Checkout(fixture.Repository, "issue/m/github-569");

        fixture.AssertFullSemver("1.0.4-issue-m-github-569.1+5");
    }

    [Test]
    public void ShouldNotGetVersionFromFeatureBranchIfNotMerged()
    {
        // * 1c08923 54 minutes ago  (HEAD -> develop)
        // | * 03dd6d5 56 minutes ago  (tag: 1.0.1-feature.1, feature)
        // |/  
        // * e2ff13b 58 minutes ago  (tag: 1.0.0-unstable.0, main)

        var configuration = GitFlowConfigurationBuilder.New
            .WithBranch("develop", builder => builder.WithTrackMergeTarget(false))
            .Build();

        using var fixture = new EmptyRepositoryFixture();

        fixture.MakeATaggedCommit("1.0.0-unstable.0"); // initial commit in main

        fixture.BranchTo("feature");
        fixture.MakeATaggedCommit("1.0.1-feature.1");
        fixture.Checkout(MainBranch);
        fixture.BranchTo("develop");
        fixture.MakeACommit();

        fixture.AssertFullSemver("1.0.0-alpha.2", configuration);

        fixture.Repository.DumpGraph();
    }

    [TestCase("alpha", "JIRA-123", "alpha")]
    [TestCase("useBranchName", "JIRA-123", "JIRA-123")]
    [TestCase($"alpha.{ConfigurationConstants.BranchNamePlaceholder}", "JIRA-123", "alpha.JIRA-123")]
    public void LabelIsBranchNameForBranchesWithoutPrefixedBranchName(string label, string branchName, string preReleaseTagName)
    {
        var configuration = GitFlowConfigurationBuilder.New
            .WithBranch("other", builder => builder
                .WithIncrement(IncrementStrategy.Patch)
                .WithRegularExpression(".*")
                .WithSourceBranches()
                .WithLabel(label))
            .Build();

        using var fixture = new EmptyRepositoryFixture();
        fixture.Repository.MakeATaggedCommit("1.0.0");
        fixture.Repository.CreateBranch(branchName);
        Commands.Checkout(fixture.Repository, branchName);
        fixture.Repository.MakeCommits(5);

        var expectedFullSemVer = $"1.0.1-{preReleaseTagName}.1+5";
        fixture.AssertFullSemver(expectedFullSemVer, configuration);
    }
}
