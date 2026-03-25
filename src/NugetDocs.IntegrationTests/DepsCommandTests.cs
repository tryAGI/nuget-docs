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
}
