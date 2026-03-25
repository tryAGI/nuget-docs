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
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Serialize*");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Serialize*", "--all");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        outputAll.Length.Should().BeGreaterThanOrEqualTo(outputPublic.Length);
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
}
