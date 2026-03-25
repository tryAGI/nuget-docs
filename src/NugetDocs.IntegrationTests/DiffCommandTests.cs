namespace NugetDocs.IntegrationTests;

[TestClass]
public class DiffCommandTests
{
    [TestMethod]
    public async Task Diff_TypeOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--type-only");

        // Exit code 0 = no breaking changes, 2 = breaking changes
        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Diff_Breaking_ShowsOnlyBreakingChanges()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--breaking");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
        // When --breaking is set, the output should indicate the filter
        if (exitCode == 0)
        {
            output.Should().Contain("No breaking");
        }
        else
        {
            output.Should().Contain("breaking");
        }
    }

    [TestMethod]
    public async Task Diff_MemberDiff_ShowsMemberChanges()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--member-diff");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
        // Member diff should show member-level markers (+ or ~)
        if (output.Contains("Changed:"))
        {
            output.Should().ContainAny("[Constructor]", "[Method]", "[Property]");
        }
    }

    [TestMethod]
    public async Task Diff_IgnoreDocs_ReducesNoise()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--ignore-docs");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Diff_NoAdditive_HidesAdditions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--no-additive", "--type-only");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
        // With --no-additive, the Added section should not appear
        output.Should().NotContain("Added:");
    }

    [TestMethod]
    public async Task Diff_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--type-only", "--json");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("\"package\"");
        output.Should().Contain("\"from\"");
        output.Should().Contain("\"to\"");
        output.Should().Contain("\"summary\"");
    }

    [TestMethod]
    public async Task Diff_NoChanges_ReturnsZero()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.3", "--to", "13.0.3", "--type-only");

        exitCode.Should().Be(0);
        output.Should().Contain("No public API changes detected");
    }

    [TestMethod]
    public async Task Diff_TableFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--format", "table");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Change");
        output.Should().Contain("Kind");
        output.Should().Contain("FullName");
        output.Should().Contain("---");
    }

    [TestMethod]
    public async Task Diff_CsvFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--format", "csv");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().StartWith("Change,Kind,FullName,Breaking");
    }

    [TestMethod]
    public async Task Diff_FrameworkFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3",
            "--type-only", "--framework", "netstandard2.0");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("netstandard2.0");
    }

    [TestMethod]
    public async Task Diff_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3",
            "--type-only", "--output", "json");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("\"package\"");
        output.Should().Contain("\"from\"");
    }

    [TestMethod]
    public async Task Diff_VersionKeywords_LatestStable()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "latest-stable", "--type-only");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Diff_VersionKeywords_BothDynamic()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Humanizer.Core", "--from", "latest-stable", "--to", "latest-prerelease", "--type-only");

        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Humanizer.Core");
    }
}
