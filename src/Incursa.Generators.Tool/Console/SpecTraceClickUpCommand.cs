namespace Incursa.Generators.Tool.Console;

using Incursa.Generators.SpecTrace.ClickUp;

internal static class SpecTraceClickUpCommand
{
    public static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || args.Any(static argument => argument is "--help" or "-h"))
        {
            WriteUsage(args.Length == 0 ? stderr : stdout);
            return Task.FromResult(args.Length == 0 ? 2 : 0);
        }

        if (!TryParse(args, out var request, out var error))
        {
            stderr.WriteLine(error);
            stderr.WriteLine();
            WriteUsage(stderr);
            return Task.FromResult(2);
        }

        var result = new ClickUpRequirementExporter().Export(request);
        foreach (var warning in result.Warnings)
        {
            stdout.WriteLine($"warning: {warning}");
        }

        foreach (var skippedFile in result.SkippedFiles)
        {
            stdout.WriteLine($"skipped: {skippedFile}");
        }

        foreach (var exportError in result.Errors)
        {
            stderr.WriteLine($"error: {exportError}");
        }

        var summaryWriter = result.Success ? stdout : stderr;
        summaryWriter.WriteLine();
        summaryWriter.WriteLine($"Exported {result.TotalRequirements} requirement(s).");
        summaryWriter.WriteLine($"CSV: {result.CsvPath}");
        summaryWriter.WriteLine($"Manifest: {result.ManifestPath}");

        return Task.FromResult(result.Success ? 0 : 1);
    }

    private static bool TryParse(string[] args, out ClickUpRequirementExportRequest request, out string? error)
    {
        var command = new ParsedExportCommand();
        request = new ClickUpRequirementExportRequest(string.Empty, string.Empty, null, null, null, DateOnly.FromDateTime(DateTime.Now));
        error = null;

        for (var index = 0; index < args.Length; index++)
        {
            if (!TryApplyOption(args, ref index, command, out error))
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(command.RepositoryRoot))
        {
            error = "Option '--repo-root' is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Repository))
        {
            error = "Option '--repository' is required.";
            return false;
        }

        request = new ClickUpRequirementExportRequest(
            command.RepositoryRoot,
            command.Repository,
            string.IsNullOrWhiteSpace(command.SourceCommit) ? null : command.SourceCommit,
            string.IsNullOrWhiteSpace(command.OutputPath) ? null : command.OutputPath,
            string.IsNullOrWhiteSpace(command.Filter) ? null : command.Filter,
            DateOnly.FromDateTime(DateTime.Now),
            string.IsNullOrWhiteSpace(command.ExcludeFilter) ? null : command.ExcludeFilter,
            command.SkipLifecycleStateDerivation);
        return true;
    }

    private static bool TryApplyOption(string[] args, ref int index, ParsedExportCommand command, out string? error)
    {
        error = null;
        var token = args[index];
        if (string.Equals(token, "--skip-lifecycle-state-derivation", StringComparison.Ordinal))
        {
            command.SkipLifecycleStateDerivation = true;
            return true;
        }

        if (!TryReadValue(args, ref index, out var value))
        {
            error = $"Option '{token}' requires a value.";
            return false;
        }

        switch (token)
        {
            case "--repo-root":
                command.RepositoryRoot = value;
                return true;
            case "--repository":
                command.Repository = value;
                return true;
            case "--source-commit":
                command.SourceCommit = value;
                return true;
            case "--out":
                command.OutputPath = value;
                return true;
            case "--filter":
                command.Filter = value;
                return true;
            case "--exclude-filter":
                command.ExcludeFilter = value;
                return true;
            default:
                error = $"Unknown option '{token}'.";
                return false;
        }
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  incursa-appdefs export-clickup-requirements --repo-root <path> --repository <owner/name> [--source-commit <sha>] [--out <path>] [--filter <glob-or-path>] [--exclude-filter <glob-or-path>] [--skip-lifecycle-state-derivation]");
    }

    private sealed class ParsedExportCommand
    {
        public string? RepositoryRoot { get; set; }

        public string? Repository { get; set; }

        public string? SourceCommit { get; set; }

        public string? OutputPath { get; set; }

        public string? Filter { get; set; }

        public string? ExcludeFilter { get; set; }

        public bool SkipLifecycleStateDerivation { get; set; }
    }
}
