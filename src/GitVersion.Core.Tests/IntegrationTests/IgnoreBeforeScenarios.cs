using GitVersion.Configuration;
using GitVersion.Core.Tests.Helpers;

namespace GitVersion.Core.Tests.IntegrationTests;

[TestFixture]
public class IgnoreBeforeScenarios : TestBase
{
    [Test]
    public void JustATest()
    {
        using EmptyRepositoryFixture fixture = new("main");

        fixture.MakeACommit("A");
        fixture.BranchTo("develop");
        fixture.MakeACommit("B", "LibA");
        fixture.BranchTo("release/1.1.0");
        fixture.MakeACommit("C", "LibA");
        fixture.Checkout("develop");

        IgnoreConfiguration ignoreConfiguration = new()
        {
            PathFilters = new PathFilterConfiguration()
            {
                Exclude = ["LibA"]
            }
        };

        var configuration = GitFlowConfigurationBuilder.New.WithIgnoreConfiguration(ignoreConfiguration).Build();

        fixture.AssertFullSemver("1.0.0-alpha.2", configuration);
    }

    [TestCase(null, "0.0.1-0")]
    [TestCase("0.0.1", "0.0.1-0")]
    [TestCase("0.1.0", "0.1.0-0")]
    [TestCase("1.0.0", "1.0.0-0")]
    public void ShouldFallbackToBaseVersionWhenAllCommitsAreIgnored(string? nextVersion, string expectedFullSemVer)
    {
        using var fixture = new EmptyRepositoryFixture();
        var dateTimeNow = DateTimeOffset.Now;
        fixture.MakeACommit();

        var configuration = GitFlowConfigurationBuilder.New.WithNextVersion(nextVersion)
            .WithIgnoreConfiguration(new IgnoreConfiguration { Before = dateTimeNow.AddDays(1) }).Build();

        fixture.AssertFullSemver(expectedFullSemVer, configuration);
    }

    [TestCase(null, "0.0.1-1")]
    [TestCase("0.0.1", "0.0.1-1")]
    [TestCase("0.1.0", "0.1.0-1")]
    [TestCase("1.0.0", "1.0.0-1")]
    public void ShouldNotFallbackToBaseVersionWhenAllCommitsAreNotIgnored(string? nextVersion, string expectedFullSemVer)
    {
        using var fixture = new EmptyRepositoryFixture();
        var dateTimeNow = DateTimeOffset.Now;
        fixture.MakeACommit();

        var configuration = GitFlowConfigurationBuilder.New.WithNextVersion(nextVersion)
            .WithIgnoreConfiguration(new IgnoreConfiguration { Before = dateTimeNow.AddDays(-1) }).Build();

        fixture.AssertFullSemver(expectedFullSemVer, configuration);
    }
}
