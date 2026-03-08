namespace Incursa.Generators.Tool;

using Incursa.Generators.Tool.Console;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return ToolCommandRunner.RunAsync(args, System.Console.Out, System.Console.Error, CancellationToken.None);
    }
}
