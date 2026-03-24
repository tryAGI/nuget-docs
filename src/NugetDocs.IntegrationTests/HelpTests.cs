namespace NugetDocs.IntegrationTests;

[TestClass]
public class HelpTests
{
    [TestMethod]
    public async Task RootHelp_ShowsAllCommands()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("--help");

        exitCode.Should().Be(0);
        output.Should().Contain("list");
        output.Should().Contain("show");
        output.Should().Contain("search");
        output.Should().Contain("info");
        output.Should().Contain("deps");
        output.Should().Contain("versions");
        output.Should().Contain("diff");
    }

    [TestMethod]
    public async Task ListHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("list", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--version");
        output.Should().Contain("--framework");
        output.Should().Contain("--all");
        output.Should().Contain("--namespace");
        output.Should().Contain("--format");
        output.Should().Contain("--json");
        output.Should().Contain("--output");
    }

    [TestMethod]
    public async Task ShowHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("show", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--version");
        output.Should().Contain("--member");
        output.Should().Contain("--assembly");
        output.Should().Contain("--json");
    }

    [TestMethod]
    public async Task SearchHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("search", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--version");
        output.Should().Contain("--format");
        output.Should().Contain("--namespace");
        output.Should().Contain("--json");
    }

    [TestMethod]
    public async Task InfoHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("info", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--version");
        output.Should().Contain("--json");
    }

    [TestMethod]
    public async Task DepsHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("deps", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--version");
        output.Should().Contain("--depth");
        output.Should().Contain("--format");
        output.Should().Contain("--json");
    }

    [TestMethod]
    public async Task VersionsHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("versions", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--stable");
        output.Should().Contain("--prerelease");
        output.Should().Contain("--latest");
        output.Should().Contain("--since");
        output.Should().Contain("--count");
        output.Should().Contain("--deprecated");
        output.Should().Contain("--format");
        output.Should().Contain("--limit");
        output.Should().Contain("--json");
    }

    [TestMethod]
    public async Task DiffHelp_ShowsOptions()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync("diff", "--help");

        exitCode.Should().Be(0);
        output.Should().Contain("--from");
        output.Should().Contain("--to");
        output.Should().Contain("--type-only");
        output.Should().Contain("--breaking");
        output.Should().Contain("--member-diff");
        output.Should().Contain("--no-additive");
        output.Should().Contain("--ignore-docs");
        output.Should().Contain("--json");
    }
}
