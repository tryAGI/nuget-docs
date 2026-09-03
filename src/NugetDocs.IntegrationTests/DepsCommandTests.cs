namespace NugetDocs.IntegrationTests;

[TestClass]
public class DepsCommandTests
{
    [TestMethod]
    public async Task Deps_ShowsDependencies()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI");

        exitCode.Should().Be(0);
        output.Should().Contain("Dependencies:");
        output.Should().Contain("Microsoft.Extensions.AI.Abstractions");
    }

    [TestMethod]
    public async Task Deps_TableFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("Package");
        output.Should().Contain("Version");
        output.Should().Contain("---");
    }

    [TestMethod]
    public async Task Deps_CsvFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--format", "csv");

        exitCode.Should().Be(0);
        output.Should().StartWith("Depth,Package,Version,Deduplicated");
    }

    [TestMethod]
    public async Task Deps_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"Id\"");
        output.Should().Contain("\"Dependencies\"");
    }

    [TestMethod]
    public async Task Deps_MetaPackage_ShowsDependencies()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Humanizer");

        exitCode.Should().Be(0);
        output.Should().Contain("Humanizer");
        output.Should().Contain("Humanizer.Core");
    }

    [TestMethod]
    public async Task Deps_DepthLimit()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "1");

        exitCode.Should().Be(0);
        output.Should().Contain("Dependencies:");
        // Depth 1 should show direct deps but not deep transitive ones
        output.Should().Contain("Microsoft.Extensions.AI.Abstractions");
    }

    [TestMethod]
    public async Task Deps_SpecificVersion()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Newtonsoft.Json", "--version", "13.0.1");

        exitCode.Should().Be(0);
        output.Should().Contain("13.0.1");
    }

    [TestMethod]
    public async Task Deps_FrameworkFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--framework", "net10.0");

        exitCode.Should().Be(0);
        output.Should().Contain("Dependencies:");
        output.Should().Contain("Microsoft.Extensions.AI.Abstractions");
    }

    [TestMethod]
    public async Task Deps_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--output", "json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"Id\"");
        output.Should().Contain("\"Dependencies\"");
    }

    [TestMethod]
    public async Task Deps_VersionKeyword_LatestStable()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Newtonsoft.Json", "--version", "latest-stable");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Deps_ExplicitLimit_IsHonored()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "5", "--format", "csv");

        exitCode.Should().Be(0);
        CountRows(output).Should().Be(5);
    }

    [TestMethod]
    public async Task Deps_TruncationFooter_SuggestsLowerDepth()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "5");

        exitCode.Should().Be(0);
        output.Should().Contain("... and ");
        output.Should().Contain("a lower --depth to narrow");
    }

    [TestMethod]
    public async Task Deps_LimitZero_ShowsAll()
    {
        var (exitCode, limited, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "5", "--format", "csv");
        var (exitCode2, all, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "0", "--format", "csv");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        // CSV stays machine-parseable — the footer is a text/table-format affordance only.
        limited.Should().NotContain("... and ");
        all.Should().NotContain("... and ");
        CountRows(all).Should().BeGreaterThan(CountRows(limited));
    }

    [TestMethod]
    public async Task Deps_NoTruncationFooter_WhenUnderLimit()
    {
        // Direct dependencies only — nowhere near the 200 default.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI");

        exitCode.Should().Be(0);
        output.Should().NotContain("... and ");
    }

    [TestMethod]
    public async Task Deps_JsonReportsTotalAndTruncated()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "5", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"Total\"");
        output.Should().Contain("\"Truncated\": true");

        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "deps", "Microsoft.Extensions.AI", "--depth", "3", "--limit", "0", "--json");

        exitCode2.Should().Be(0);
        full.Should().Contain("\"Truncated\": false");
    }

    private static int CountRows(string csv)
    {
        // Subtract the header line.
        return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }
}
