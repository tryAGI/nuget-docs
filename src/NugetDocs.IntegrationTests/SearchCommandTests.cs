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
}
