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
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--all");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        // --all should return at least as many types as public-only
        outputAll.Length.Should().BeGreaterThanOrEqualTo(outputPublic.Length);
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
}
