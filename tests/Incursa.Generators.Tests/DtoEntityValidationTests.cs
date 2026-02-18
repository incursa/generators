namespace Incursa.Generators.Tests;

using System.Collections.Immutable;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

public static class DtoEntityValidationTests
{
    [Fact]
    public static void DefaultedStringWithoutExplicitRequiredDoesNotEmitWarning()
    {
        const string json = "{\n  \"name\": \"OrgContext\",\n  \"namespace\": \"Incursa.Platform.Core\",\n  \"properties\": [\n    {\n      \"name\": \"DefaultTimeZoneId\",\n      \"type\": \"string\",\n      \"defaultValue\": \"UTC\"\n    }\n  ]\n}";

        var diagnostics = RunGenerator(json);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public static void RequiredNonNullableStringWithDefaultValueEmitsWarning()
    {
        const string json = "{\n  \"name\": \"OrgContext\",\n  \"namespace\": \"Incursa.Platform.Core\",\n  \"properties\": [\n    {\n      \"name\": \"DefaultTimeZoneId\",\n      \"type\": \"string\",\n      \"required\": true,\n      \"defaultValue\": \"UTC\"\n    }\n  ]\n}";

        var diagnostics = RunGenerator(json);

        diagnostics.ShouldContain(d => d.Id == "BG006" && d.Severity == DiagnosticSeverity.Warning && d.GetMessage(null).Contains("Required non-nullable properties should not have a default value."));
    }

    [Fact]
    public static void OptionalNonNullableReferenceWithoutDefaultEmitsError()
    {
        const string json = "{\n  \"name\": \"OrgContext\",\n  \"namespace\": \"Incursa.Platform.Core\",\n  \"properties\": [\n    {\n      \"name\": \"DisplayName\",\n      \"type\": \"string\",\n      \"required\": false,\n      \"nullable\": false\n    }\n  ]\n}";

        var diagnostics = RunGenerator(json);

        diagnostics.ShouldContain(d => d.Id == "BG004" && d.Severity == DiagnosticSeverity.Error && d.GetMessage(null).Contains("Non-nullable reference type properties that are not required must have a default value."));
    }

    [Fact]
    public static void StrictDtoWithSettablePropertyEmitsError()
    {
        const string json = "{\n  \"name\": \"OrgContext\",\n  \"namespace\": \"Incursa.Platform.Core\",\n  \"strict\": true,\n  \"properties\": [\n    {\n      \"name\": \"DisplayName\",\n      \"type\": \"string\",\n      \"settable\": true\n    }\n  ]\n}";

        var diagnostics = RunGenerator(json);

        diagnostics.ShouldContain(d => d.Id == "BG004" && d.Severity == DiagnosticSeverity.Error && d.GetMessage(null).Contains("Strict DTOs cannot have settable properties."));
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string json)
    {
        var generator = new DtoEntitySourceGenerator();
        var additionalTexts = ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("OrgContext.dto.json", json));
        var parseOptions = new CSharpParseOptions(LanguageVersion.LatestMajor);
        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions,
            additionalTexts: additionalTexts);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, Xunit.TestContext.Current.CancellationToken);
        var runResult = driver.GetRunResult();
        return runResult.Diagnostics;
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly string content;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            this.content = content;
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, Encoding.UTF8);
    }
}
