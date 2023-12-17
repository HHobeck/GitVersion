using GitVersion.Configuration;
using GitVersion.Core.Tests;
using GitVersion.Core.Tests.IntegrationTests;
using GitVersion.VersionCalculation;

namespace GitVersion.Core.TrunkBased;

internal partial class TrunkBasedScenariosWithAGitHubFlow
{
    [Parallelizable(ParallelScope.All)]
    public class GivenAFeatureBranchWithOneCommitWhenCommitTaggedAsPreReleaseBar
    {
        private EmptyRepositoryFixture? fixture;

        private static GitHubFlowConfigurationBuilder TrunkBasedBuilder => GitHubFlowConfigurationBuilder.New
            .WithVersioningMode(VersioningMode.TrunkBased).WithLabel(null)
            .WithBranch("feature", _ => _.WithVersioningMode(VersioningMode.ManualDeployment).WithIsMainline(false));

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // A 59 minutes ago (HEAD -> feature/foo) (tag 0.0.0-bar)

            fixture = new EmptyRepositoryFixture("feature/foo");

            fixture.MakeACommit("A");
            fixture.ApplyTag("0.0.0-bar");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => fixture?.Dispose();

        [TestCase(IncrementStrategy.None, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Patch, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Minor, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Major, null, ExpectedResult = "0.0.0-bar")]

        [TestCase(IncrementStrategy.None, "", ExpectedResult = "0.0.0-1+1")]
        [TestCase(IncrementStrategy.Patch, "", ExpectedResult = "0.0.1-1+1")]
        [TestCase(IncrementStrategy.Minor, "", ExpectedResult = "0.1.0-1+1")]
        [TestCase(IncrementStrategy.Major, "", ExpectedResult = "1.0.0-1+1")]

        [TestCase(IncrementStrategy.None, "foo", ExpectedResult = "0.0.0-foo.1+1")]
        [TestCase(IncrementStrategy.Patch, "foo", ExpectedResult = "0.0.1-foo.1+1")]
        [TestCase(IncrementStrategy.Minor, "foo", ExpectedResult = "0.1.0-foo.1+1")]
        [TestCase(IncrementStrategy.Major, "foo", ExpectedResult = "1.0.0-foo.1+1")]

        [TestCase(IncrementStrategy.None, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Patch, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Minor, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Major, "bar", ExpectedResult = "0.0.0-bar")]
        public string GetVersion(IncrementStrategy increment, string? label)
        {
            IGitVersionConfiguration trunkBased = TrunkBasedBuilder
                .WithBranch("feature", _ => _.WithIncrement(increment).WithLabel(label))
                .Build();

            return fixture!.GetVersion(trunkBased).FullSemVer;
        }

        [Ignore("Enable if WithTakeIncrementedVersion(TakeIncrementedVersion.TakeAlwaysIncrementedVersion) feature has been implemented!")]
        [TestCase(IncrementStrategy.None, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Patch, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Minor, null, ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Major, null, ExpectedResult = "0.0.0-bar")]

        [TestCase(IncrementStrategy.None, "", ExpectedResult = "0.0.0-1+1")]
        [TestCase(IncrementStrategy.Patch, "", ExpectedResult = "0.0.1-1+1")]
        [TestCase(IncrementStrategy.Minor, "", ExpectedResult = "0.1.0-1+1")]
        [TestCase(IncrementStrategy.Major, "", ExpectedResult = "1.0.0-1+1")]

        [TestCase(IncrementStrategy.None, "foo", ExpectedResult = "0.0.0-foo.1+1")]
        [TestCase(IncrementStrategy.Patch, "foo", ExpectedResult = "0.0.1-foo.1+1")]
        [TestCase(IncrementStrategy.Minor, "foo", ExpectedResult = "0.1.0-foo.1+1")]
        [TestCase(IncrementStrategy.Major, "foo", ExpectedResult = "1.0.0-foo.1+1")]

        [TestCase(IncrementStrategy.None, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Patch, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Minor, "bar", ExpectedResult = "0.0.0-bar")]
        [TestCase(IncrementStrategy.Major, "bar", ExpectedResult = "0.0.0-bar")]
        public string GetVersionWithTakeAlwaysIncrementedVersion(IncrementStrategy increment, string? label)
        {
            IGitVersionConfiguration trunkBased = TrunkBasedBuilder
                //.WithTakeIncrementedVersion(TakeIncrementedVersion.TakeAlwaysIncrementedVersion)
                .WithBranch("feature", _ => _.WithIncrement(increment).WithLabel(label))
                .Build();

            return fixture!.GetVersion(trunkBased).FullSemVer;
        }
    }
}
