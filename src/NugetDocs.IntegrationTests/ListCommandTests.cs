namespace NugetDocs.IntegrationTests;

[TestClass]
public class ListCommandTests
{
    [TestMethod]
    public async Task List_ReturnsPublicTypes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("JsonConvert");
    }

    [TestMethod]
    public async Task List_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"package\"");
        output.Should().Contain("\"types\"");
    }

    [TestMethod]
    public async Task List_TableFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("Kind");
        output.Should().Contain("Name");
        output.Should().Contain("---");
    }

    [TestMethod]
    public async Task List_CsvFormat()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--format", "csv");

        exitCode.Should().Be(0);
        output.Should().StartWith("Kind,Name,FullName,Namespace,Summary");
    }

    [TestMethod]
    public async Task List_NamespaceFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--namespace", "Newtonsoft.Json.Linq");

        exitCode.Should().Be(0);
        output.Should().Contain("JToken");
        output.Should().NotContain("JsonConvert");
    }

    [TestMethod]
    public async Task List_AllIncludesInternalTypes()
    {
        // --limit 0 so the default cap does not clip either side, and count rows rather than
        // characters: internal types carry no XML summary, so a longer listing can be shorter text.
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--limit", "0", "--format", "csv");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--all", "--limit", "0", "--format", "csv");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        // --all should return at least as many types as public-only
        CountRows(outputAll).Should().BeGreaterThanOrEqualTo(CountRows(outputPublic));
    }

    [TestMethod]
    public async Task List_SpecificVersion()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--version", "13.0.1");

        exitCode.Should().Be(0);
        output.Should().Contain("13.0.1");
        output.Should().Contain("JsonConvert");
    }

    [TestMethod]
    public async Task List_FrameworkFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--framework", "netstandard2.0");

        exitCode.Should().Be(0);
        output.Should().Contain("netstandard2.0");
        output.Should().Contain("JsonConvert");
    }

    [TestMethod]
    public async Task List_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--output", "json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"package\"");
        output.Should().Contain("\"types\"");
    }

    [TestMethod]
    public async Task List_VersionKeyword_LatestPrerelease()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Humanizer.Core", "--version", "latest-prerelease");

        exitCode.Should().Be(0);
        output.Should().Contain("Humanizer.Core");
    }

    [TestMethod]
    public async Task List_VersionKeyword_LatestStable()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--version", "latest-stable");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("JsonConvert");
    }

    [TestMethod]
    public async Task List_VersionKeyword_Latest()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--version", "latest");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("JsonConvert");
    }

    [TestMethod]
    public async Task List_DefaultLimit_TruncatesAboveCap()
    {
        // Newtonsoft.Json with --all lists ~300 types, above the 200 default cap.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--all");

        exitCode.Should().Be(0);
        output.Should().Contain("... and ");
        output.Should().Contain("--limit 0 to show all");
    }

    [TestMethod]
    public async Task List_LimitZero_ShowsAll()
    {
        var (exitCode, limited, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--all", "--format", "csv");
        var (exitCode2, all, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--all", "--format", "csv", "--limit", "0");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        // CSV stays machine-parseable — the footer is a text/table-format affordance only.
        limited.Should().NotContain("... and ");
        all.Should().NotContain("... and ");
        CountRows(limited).Should().Be(200);
        CountRows(all).Should().BeGreaterThan(200);
    }

    [TestMethod]
    public async Task List_ExplicitLimit_IsHonored()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--limit", "5", "--format", "csv");

        exitCode.Should().Be(0);
        CountRows(output).Should().Be(5);
    }

    [TestMethod]
    public async Task List_NoTruncationFooter_WhenUnderLimit()
    {
        // Newtonsoft.Json's public surface (144 types) is well under the 200 default.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().NotContain("... and ");
    }

    [TestMethod]
    public async Task List_TableFormat_ShowsTruncationFooter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--limit", "5", "--format", "table");

        exitCode.Should().Be(0);
        output.Should().Contain("... and ");
        output.Should().Contain("--namespace to narrow");
    }

    [TestMethod]
    public async Task List_JsonReportsTotalAndTruncated()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--limit", "5", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"total\"");
        output.Should().Contain("\"truncated\": true");

        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--limit", "0", "--json");

        exitCode2.Should().Be(0);
        full.Should().Contain("\"truncated\": false");
    }

    private static int CountRows(string csv)
    {
        // Subtract the header line.
        return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }

    [TestMethod]
    public async Task List_PluralizesKindHeadings()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().Contain("Interfaces:");
        output.Should().Contain("Classes:");
        output.Should().Contain("Structs:");
        output.Should().Contain("Enums:");
        output.Should().Contain("Delegates:");
    }

    [TestMethod]
    public async Task List_MarksDeprecatedTypes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().Contain("** deprecated");
    }

    [TestMethod]
    public async Task List_Deprecated_FiltersToObsoleteTypes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--deprecated");

        exitCode.Should().Be(0);
        // JsonSchema and the Bson types carry [Obsolete]; JsonConvert does not.
        output.Should().Contain("JsonValidatingReader");
        output.Should().NotContain("JsonConvert");
    }

    [TestMethod]
    public async Task List_CsvCarriesDeprecationColumn()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--deprecated", "--format", "csv");

        exitCode.Should().Be(0);
        output.Should().StartWith("Kind,Name,FullName,Namespace,Summary,Deprecated");
        output.Should().Contain("moved to its own package");
    }

    [TestMethod]
    public async Task List_JsonCarriesDeprecation()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--deprecated", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"deprecated\": true");
        output.Should().Contain("\"deprecationMessage\"");
    }
}
