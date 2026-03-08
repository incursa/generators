namespace Incursa.Generators.AppDefinitions.Emission;

public sealed record GeneratedFile(
    string TargetName,
    string TargetKind,
    string AbsolutePath,
    string RelativePath,
    string Content);
