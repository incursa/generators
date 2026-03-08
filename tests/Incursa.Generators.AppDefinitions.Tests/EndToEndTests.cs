namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Tests.Fixtures;
using Incursa.Generators.Tool.Console;

public sealed class EndToEndTests
{
    [Fact]
    public async Task Cli_generate_write_emits_expected_files()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath", "Source"));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await ToolCommandRunner.RunAsync(
            [
                "generate",
                "--config",
                Path.Combine(workspace.RootPath, "app-definitions.json"),
                "--write",
            ],
            stdout,
            stderr,
            TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0, stderr.ToString());
        stdout.ToString().ShouldContain("Generated 4 file(s);");
        Directory.Exists(Path.Combine(workspace.RootPath, "generated")).ShouldBeTrue();
    }

    [Fact]
    public async Task Cli_help_returns_success()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await ToolCommandRunner.RunAsync(
            ["--help"],
            stdout,
            stderr,
            TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0);
        stdout.ToString().ShouldContain("Usage:");
        stderr.ToString().ShouldBeEmpty();
    }
}
