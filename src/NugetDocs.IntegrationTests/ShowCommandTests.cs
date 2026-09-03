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
        // --max-lines 0 so the default cap does not clip the --all side: with internal members
        // JsonConvert runs past 1,000 lines, which would make the larger listing the shorter text.
        var (exitCode, outputPublic, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--max-lines", "0");
        var (exitCode2, outputAll, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--all", "--max-lines", "0");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        outputAll.Length.Should().BeGreaterThanOrEqualTo(outputPublic.Length);
    }

    [TestMethod]
    public async Task Show_FrameworkFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--framework", "netstandard2.0");

        exitCode.Should().Be(0);
        output.Should().Contain("netstandard2.0");
        output.Should().Contain("class JsonConvert");
    }

    [TestMethod]
    public async Task Show_OutputJsonLongForm()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--output", "json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"source\"");
    }

    [TestMethod]
    public async Task Show_VersionKeyword_LatestStable()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--version", "latest-stable");

        exitCode.Should().Be(0);
        output.Should().Contain("class JsonConvert");
    }

    [TestMethod]
    public async Task Show_DefaultMaxLines_TruncatesLargeType()
    {
        // JToken decompiles to ~2,600 lines, above the 1,000 default.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JToken");

        exitCode.Should().Be(0);
        output.Should().Contain("more lines");
        output.Should().Contain("--max-lines 0 for the full source");
        CountLines(output).Should().BeLessThan(1100);
    }

    [TestMethod]
    public async Task Show_MaxLinesZero_ShowsAll()
    {
        var (exitCode, capped, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JToken");
        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JToken", "--max-lines", "0");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        full.Should().NotContain("more lines");
        CountLines(full).Should().BeGreaterThan(CountLines(capped));
    }

    [TestMethod]
    public async Task Show_ExplicitMaxLines_IsHonored()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--max-lines", "20");

        exitCode.Should().Be(0);
        output.Should().Contain("more lines");
        // 20 source lines plus the package header, blank line and footer.
        CountLines(output).Should().BeLessThan(30);
    }

    [TestMethod]
    public async Task Show_NoTruncationFooter_WhenUnderLimit()
    {
        // JsonConvert is 972 lines, just under the 1,000 default.
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert");

        exitCode.Should().Be(0);
        output.Should().NotContain("more lines");
    }

    [TestMethod]
    public async Task Show_Signatures_OmitsBodies()
    {
        var (exitCode, signatures, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--signatures");
        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--max-lines", "0");

        exitCode.Should().Be(0);
        exitCode2.Should().Be(0);
        signatures.Should().Contain("// Members:");
        signatures.Should().Contain("SerializeObject");
        signatures.Length.Should().BeLessThan(full.Length);
    }

    [TestMethod]
    public async Task Show_Signatures_WithMemberFiltersToThatMember()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--signatures", "--member", "SerializeObject");

        exitCode.Should().Be(0);
        output.Should().Contain("SerializeObject");
        output.Should().NotContain("DeserializeObject");
    }

    [TestMethod]
    public async Task Show_Signatures_UnknownMemberFails()
    {
        var (exitCode, _, error) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--signatures", "--member", "NoSuchMember");

        exitCode.Should().Be(1);
        error.Should().Contain("not found");
    }

    [TestMethod]
    public async Task Show_JsonReportsTotalLinesAndTruncated()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JToken", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"totalLines\"");
        output.Should().Contain("\"truncated\": true");

        var (exitCode2, full, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JToken", "--max-lines", "0", "--json");

        exitCode2.Should().Be(0);
        full.Should().Contain("\"truncated\": false");
    }

    private static int CountLines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
