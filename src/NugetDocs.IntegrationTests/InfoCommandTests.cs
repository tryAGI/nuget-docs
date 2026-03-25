namespace NugetDocs.IntegrationTests;

[TestClass]
public class InfoCommandTests
{
    [TestMethod]
    public async Task Info_ShowsPackageMetadata()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("James Newton-King");
    }

    [TestMethod]
    public async Task Info_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"id\"");
        output.Should().Contain("\"authors\"");
    }

    [TestMethod]
    public async Task Info_MetaPackage_ShowsMetadata()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Humanizer");

        exitCode.Should().Be(0);
        output.Should().Contain("Humanizer");
        output.Should().Contain("Dependencies:");
    }

    [TestMethod]
    public async Task Info_SpecificVersion()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json", "--version", "13.0.1");

        exitCode.Should().Be(0);
        output.Should().Contain("13.0.1");
        output.Should().Contain("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Info_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json", "--output", "json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"id\"");
        output.Should().Contain("\"authors\"");
    }
}
