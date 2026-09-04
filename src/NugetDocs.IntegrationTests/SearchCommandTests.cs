namespace NugetDocs.IntegrationTests;

[TestClass]
public class SearchCommandTests
{
    [TestMethod]
    public async Task Search_FindsMatchingTypes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*");

        exitCode.Should().Be(0);
        output.Should().Contain("JToken");
        output.Should().Contain("Results:");
    }

    [TestMethod]
    public async Task Search_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Convert*", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"results\"");
        output.Should().Contain("\"count\"");
    }

    [TestMethod]
    public async Task Search_TableFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("Kind");
        output.Should().Contain("Name");
        output.Should().Contain("---");
    }

    [TestMethod]
    public async Task Search_CsvFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--format", "csv");

        exitCode.Should().Be(0);
        output.Should().StartWith("Kind,MemberKind,Name,FullName");
    }

    [TestMethod]
    public async Task Search_NamespaceFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--namespace", "Newtonsoft.Json.Linq");

        exitCode.Should().Be(0);
        output.Should().Contain("JToken");
        output.Should().NotContain("Newtonsoft.Json.JsonToken");
    }

    [TestMethod]
    public async Task Search_AllIncludesInternalMembers()
    {
        // --limit 0 so the default cap cannot clip either side, and count rows rather than
        // characters: a capped larger result set can be shorter text than an uncapped smaller one.
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Serialize*", "--limit", "0", "--format", "csv");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Serialize*", "--all", "--limit", "0", "--format", "csv");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        CountRows(outputAll).Should().BeGreaterThanOrEqualTo(CountRows(outputPublic));
    }

    [TestMethod]
    public async Task Search_FrameworkFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--framework", "netstandard2.0");

        exitCode.Should().Be(0);
        output.Should().Contain("netstandard2.0");
        output.Should().Contain("JToken");
    }

    [TestMethod]
    public async Task Search_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Convert*", "--output", "json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"results\"");
        output.Should().Contain("\"count\"");
    }

    [TestMethod]
    public async Task Search_VersionKeyword_LatestStable()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--version", "latest-stable");

        exitCode.Should().Be(0);
        output.Should().Contain("JToken");
    }

    [TestMethod]
    public async Task Search_ExplicitLimit_IsHonored()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "5", "--format", "csv");

        exitCode.Should().Be(0);
        CountRows(output).Should().Be(5);
    }

    [TestMethod]
    public async Task Search_TruncationFooter_AndResultsLine()
    {
        // 33 public matches for *Token*, capped at 5 — the header states both numbers.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "5");

        exitCode.Should().Be(0);
        output.Should().Contain("(showing 5)");
        output.Should().Contain("... and ");
        output.Should().Contain("narrow the pattern");
    }

    [TestMethod]
    public async Task Search_LimitZero_ShowsAll()
    {
        var (exitCode, limited, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "5", "--format", "csv");
        var (exitCode2, all, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "0", "--format", "csv");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        // CSV stays machine-parseable — the footer is a text/table-format affordance only.
        limited.Should().NotContain("... and ");
        all.Should().NotContain("... and ");
        CountRows(all).Should().BeGreaterThan(CountRows(limited));
    }

    [TestMethod]
    public async Task Search_NoTruncationFooter_WhenUnderLimit()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*");

        exitCode.Should().Be(0);
        output.Should().NotContain("... and ");
        output.Should().NotContain("(showing ");
    }

    [TestMethod]
    public async Task Search_TableFormat_ShowsTruncationFooter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "5", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("... and ");
        output.Should().Contain("--limit 0 to show all");
    }

    [TestMethod]
    public async Task Search_JsonReportsTotalAndTruncated()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "5", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"count\": 5");
        output.Should().Contain("\"total\"");
        output.Should().Contain("\"truncated\": true");

        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--limit", "0", "--json");

        exitCode2.Should().Be(0);
        full.Should().Contain("\"truncated\": false");
    }

    private static int CountRows(string csv)
    {
        // Subtract the header line.
        return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }

    [TestMethod]
    public async Task Search_Deprecated_FiltersToObsoleteResults()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Binder*", "--deprecated");

        exitCode.Should().Be(0);
        // JsonSerializerSettings.Binder is [Obsolete]; ISerializationBinder is not.
        output.Should().Contain("** deprecated");
        output.Should().Contain("JsonSerializerSettings.Binder");
        output.Should().NotContain("ISerializationBinder");
    }

    [TestMethod]
    public async Task Search_JsonCarriesDeprecation()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Binder*", "--deprecated", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"deprecated\": true");
        output.Should().Contain("\"deprecationMessage\"");
    }
}
