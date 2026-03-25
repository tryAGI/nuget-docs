namespace NugetDocs.IntegrationTests;

[TestClass]
public class ErrorTests
{
    [TestMethod]
    public async Task List_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "list", "ThisPackageDoesNotExist12345xyz");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Show_NonexistentType_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "NonExistentType12345");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Show_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "show", "ThisPackageDoesNotExist12345xyz", "SomeType");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Search_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "search", "ThisPackageDoesNotExist12345xyz", "*Foo*");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Info_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "info", "ThisPackageDoesNotExist12345xyz");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Versions_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "versions", "ThisPackageDoesNotExist12345xyz");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Deps_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "deps", "ThisPackageDoesNotExist12345xyz");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Diff_InvalidPackage_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "diff", "ThisPackageDoesNotExist12345xyz", "--from", "1.0.0", "--to", "2.0.0");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task List_InvalidVersion_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "list", "Newtonsoft.Json", "--version", "999.999.999");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task List_MetaPackage_SuggestsDependencies()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "list", "Humanizer");

        exitCode.Should().Be(1);
        error.Should().Contain("meta-package");
        error.Should().Contain("Try: Humanizer.Core");
    }

    [TestMethod]
    public async Task Show_InvalidVersion_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--version", "999.999.999");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Search_InvalidVersion_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*Token*", "--version", "999.999.999");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Diff_InvalidFromVersion_ReturnsError()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "999.999.999", "--to", "13.0.1");

        exitCode.Should().Be(1);
        error.Should().Contain("Error:");
    }

    [TestMethod]
    public async Task Search_NoResults_ReturnsEmpty()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "search", "Newtonsoft.Json", "*ZzzNonExistentPattern999*");

        exitCode.Should().Be(0);
        output.Should().Contain("Results: 0");
    }

    [TestMethod]
    public async Task Search_MetaPackage_SuggestsDependencies()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "search", "Humanizer", "*Core*");

        exitCode.Should().Be(1);
        error.Should().Contain("meta-package");
        error.Should().Contain("Try: Humanizer.Core");
    }

    [TestMethod]
    public async Task Show_MetaPackage_SuggestsDependencies()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "show", "Humanizer", "SomeType");

        exitCode.Should().Be(1);
        error.Should().Contain("meta-package");
        error.Should().Contain("Try: Humanizer.Core");
    }
}
