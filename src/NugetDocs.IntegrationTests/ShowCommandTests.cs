namespace NugetDocs.IntegrationTests;

[TestClass]
public class ShowCommandTests
{
    [TestMethod]
    public async Task Show_DecompilesType()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert");

        exitCode.Should().Be(0);
        output.Should().Contain("class JsonConvert");
        output.Should().Contain("SerializeObject");
    }

    [TestMethod]
    public async Task Show_ShortNameResolution()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Microsoft.Extensions.AI.Abstractions", "IChatClient");

        exitCode.Should().Be(0);
        output.Should().Contain("interface IChatClient");
    }

    [TestMethod]
    public async Task Show_MemberFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--member", "SerializeObject");

        exitCode.Should().Be(0);
        output.Should().Contain("SerializeObject");
    }

    [TestMethod]
    public async Task Show_AssemblyAttributes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "--assembly");

        exitCode.Should().Be(0);
        output.Should().Contain("assembly:");
    }

    [TestMethod]
    public async Task Show_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"source\"");
    }

    [TestMethod]
    public async Task Show_SpecificVersion()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--version", "13.0.1");

        exitCode.Should().Be(0);
        output.Should().Contain("13.0.1");
        output.Should().Contain("class JsonConvert");
    }

    [TestMethod]
    public async Task Show_AllIncludesInternalTypes()
    {
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--all");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        outputAll.Length.Should().BeGreaterThanOrEqualTo(outputPublic.Length);
    }
}
