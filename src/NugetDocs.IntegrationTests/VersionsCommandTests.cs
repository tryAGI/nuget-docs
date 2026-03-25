namespace NugetDocs.IntegrationTests;

[TestClass]
public class VersionsCommandTests
{
    [TestMethod]
    public async Task Versions_ListsVersions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Newtonsoft.Json", "--limit", "5");

        exitCode.Should().Be(0);
        output.Should().Contain("Versions: Newtonsoft.Json");
        output.Should().Contain("13.0");
    }

    [TestMethod]
    public async Task Versions_StableOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Humanizer", "--stable", "--limit", "3");

        exitCode.Should().Be(0);
        output.Should().Contain("stable only");
    }

    [TestMethod]
    public async Task Versions_CountOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Newtonsoft.Json", "--count", "--stable");

        exitCode.Should().Be(0);

        var trimmed = output.Trim();
        int.TryParse(trimmed, out var count).Should().BeTrue();
        count.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task Versions_TableFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Humanizer", "--stable", "--limit", "3", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("Version");
        output.Should().Contain("Pre");
        output.Should().Contain("---");
    }

    [TestMethod]
    public async Task Versions_CsvFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Humanizer", "--stable", "--limit", "3", "--format", "csv");

        exitCode.Should().Be(0);
        output.Should().StartWith("Version,Prerelease");
    }

    [TestMethod]
    public async Task Versions_SinceFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Newtonsoft.Json", "--since", "13.0.1", "--stable");

        exitCode.Should().Be(0);
        output.Should().Contain("since 13.0.1");
        output.Should().NotContain("13.0.1\n"); // 13.0.1 itself should not be listed
    }

    [TestMethod]
    public async Task Versions_CountWithSince()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Newtonsoft.Json", "--count", "--since", "13.0.1", "--stable");

        exitCode.Should().Be(0);
        var trimmed = output.Trim();
        int.TryParse(trimmed, out var count).Should().BeTrue();
        count.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task Versions_LatestOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Humanizer", "--latest");

        exitCode.Should().Be(0);
        output.Should().Contain("latest");
    }

    [TestMethod]
    public async Task Versions_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Newtonsoft.Json", "--json", "--limit", "3");

        exitCode.Should().Be(0);
        output.Should().Contain("\"versions\"");
    }

    [TestMethod]
    public async Task Versions_PrereleaseOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "Humanizer", "--prerelease", "--limit", "5");

        exitCode.Should().Be(0);
        output.Should().Contain("prerelease only");
    }

    [TestMethod]
    public async Task Versions_DeprecatedFlag()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "versions", "WindowsAzure.Storage", "--deprecated", "--limit", "3");

        exitCode.Should().Be(0);
        output.Should().Contain("deprecated");
    }
}
