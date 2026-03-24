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
}
