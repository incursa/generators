namespace Incursa.Generators.Tool.Console;

using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Pipeline;

public static class ToolCommandRunner
{
    public static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var showHelp = IsHelpRequest(args);
        if (!TryParseArguments(args, out var command, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                stderr.WriteLine(error);
                stderr.WriteLine();
            }

            WriteUsage(showHelp ? stdout : stderr);
            return Task.FromResult(showHelp ? 0 : 2);
        }

        var generator = new AppDefinitionGenerator();
        var result = generator.Execute(
            new GenerationRequest(command.ConfigPath!, command.DefinitionsPath, command.Filter),
            command.ExecutionMode);

        WriteDiagnostics(result.Diagnostics, command.Verbosity, stdout, stderr);
        WriteSummary(result, stdout, stderr);

        return Task.FromResult(result.Success ? 0 : 1);
    }

    private static bool IsHelpRequest(string[] args)
    {
        return args.Length > 0
            && (string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase)
                || args.Any(static argument => argument is "--help" or "-h"));
    }

    private static bool TryParseArguments(string[] args, out ParsedCommand command, out string? error)
    {
        command = new ParsedCommand();
        error = null;

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            error = null;
            return false;
        }

        if (!string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unknown command '{args[0]}'.";
            return false;
        }

        var isGenerate = string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase);
        command = command with { CommandName = args[0], ExecutionMode = GenerationExecutionMode.Validate };

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            switch (token)
            {
                case "--config":
                    if (!TryReadValue(args, ref index, out var configPath))
                    {
                        error = "Option '--config' requires a path value.";
                        return false;
                    }

                    command = command with { ConfigPath = configPath };
                    break;

                case "--definitions":
                    if (!TryReadValue(args, ref index, out var definitionsPath))
                    {
                        error = "Option '--definitions' requires a path value.";
                        return false;
                    }

                    command = command with { DefinitionsPath = definitionsPath };
                    break;

                case "--filter":
                    if (!TryReadValue(args, ref index, out var filter))
                    {
                        error = "Option '--filter' requires a value.";
                        return false;
                    }

                    command = command with { Filter = filter };
                    break;

                case "--verbosity":
                    if (!TryReadValue(args, ref index, out var verbosityValue))
                    {
                        error = "Option '--verbosity' requires a value of quiet, normal, or detailed.";
                        return false;
                    }

                    if (!Enum.TryParse<ToolVerbosity>(verbosityValue, ignoreCase: true, out var verbosity))
                    {
                        error = $"Unsupported verbosity '{verbosityValue}'.";
                        return false;
                    }

                    command = command with { Verbosity = verbosity };
                    break;

                case "--write":
                    if (!isGenerate)
                    {
                        error = "Option '--write' is only valid with the generate command.";
                        return false;
                    }

                    command = command with { Write = true };
                    break;

                case "--check":
                    if (!isGenerate)
                    {
                        error = "Option '--check' is only valid with the generate command.";
                        return false;
                    }

                    command = command with { Check = true };
                    break;

                case "--help":
                case "-h":
                    error = null;
                    return false;

                default:
                    error = $"Unknown option '{token}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(command.ConfigPath))
        {
            error = "Option '--config' is required.";
            return false;
        }

        if (!isGenerate)
        {
            command = command with { ExecutionMode = GenerationExecutionMode.Validate };
            return true;
        }

        if (command.Write == command.Check)
        {
            error = "Generate requires exactly one of '--write' or '--check'.";
            return false;
        }

        command = command with
        {
            ExecutionMode = command.Write ? GenerationExecutionMode.Write : GenerationExecutionMode.Check,
        };

        return true;
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

    private static void WriteDiagnostics(
        IReadOnlyList<GeneratorDiagnostic> diagnostics,
        ToolVerbosity verbosity,
        TextWriter stdout,
        TextWriter stderr)
    {
        IEnumerable<GeneratorDiagnostic> filteredDiagnostics = verbosity switch
        {
            ToolVerbosity.Quiet => diagnostics.Where(static diagnostic => diagnostic.Severity == GeneratorDiagnosticSeverity.Error),
            ToolVerbosity.Normal => diagnostics.Where(static diagnostic => diagnostic.Severity != GeneratorDiagnosticSeverity.Info),
            _ => diagnostics,
        };

        foreach (var diagnostic in filteredDiagnostics)
        {
            var writer = diagnostic.Severity == GeneratorDiagnosticSeverity.Error ? stderr : stdout;
            writer.WriteLine(diagnostic.ToString());
        }
    }

    private static void WriteSummary(GenerationResult result, TextWriter stdout, TextWriter stderr)
    {
        var writer = result.Success ? stdout : stderr;
        writer.WriteLine();
        writer.WriteLine(
            result.Mode switch
            {
                GenerationExecutionMode.Validate => $"Validated {result.MatchedFeatureCount}/{result.DiscoveredFeatureCount} feature(s).",
                GenerationExecutionMode.Write => $"Generated {result.GeneratedFiles.Count} file(s); wrote {result.FilesWritten}, deleted {result.FilesDeleted}, unchanged {result.FilesUnchanged}.",
                GenerationExecutionMode.Check => $"Checked {result.GeneratedFiles.Count} file(s); unchanged {result.FilesUnchanged}.",
                _ => $"Completed {result.Mode}.",
            });
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  incursa-appdefs validate --config <path> [--definitions <path>] [--filter <pattern>] [--verbosity <level>]");
        writer.WriteLine("  incursa-appdefs generate --config <path> [--definitions <path>] [--filter <pattern>] (--write | --check) [--verbosity <level>]");
        writer.WriteLine();
        writer.WriteLine("Verbosity:");
        writer.WriteLine("  quiet | normal | detailed");
    }

    private enum ToolVerbosity
    {
        Quiet,
        Normal,
        Detailed,
    }

    private sealed record ParsedCommand(
        string? CommandName = null,
        string? ConfigPath = null,
        string? DefinitionsPath = null,
        string? Filter = null,
        bool Write = false,
        bool Check = false,
        ToolVerbosity Verbosity = ToolVerbosity.Normal,
        GenerationExecutionMode ExecutionMode = GenerationExecutionMode.Validate);
}
