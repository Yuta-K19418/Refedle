using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Refedle.Tests.Generators;

/// <summary>An in-memory C# source file fed to <see cref="SourceGeneratorRunner"/>.</summary>
internal readonly record struct SourceFile(string Path, string Content);

/// <summary>The observable outcome of a single generator run.</summary>
/// <param name="GeneratorDiagnostics">Diagnostics reported by the generator itself, including generator exceptions.</param>
/// <param name="GeneratedSources">Sources added by the generator.</param>
/// <param name="CompilationDiagnostics">Warnings and errors of the compilation that includes the generated sources.</param>
internal sealed record SourceGeneratorOutput(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources,
    ImmutableArray<Diagnostic> CompilationDiagnostics);

/// <summary>
/// Runs an incremental generator over in-memory sources with <see cref="CSharpGeneratorDriver"/>.
/// The compilation references the framework assemblies of the running .NET runtime, so no reference
/// packs are downloaded from NuGet.
/// </summary>
internal static class SourceGeneratorRunner
{
    private const string AssemblyName = "GeneratorTestAssembly";

    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    public static SourceGeneratorOutput Run<TGenerator>(IReadOnlyList<SourceFile> sources)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default;
        SyntaxTree[] syntaxTrees =
        [
            .. sources.Select(source => CSharpSyntaxTree.ParseText(source.Content, parseOptions, source.Path)),
        ];
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);
        var compilation = CSharpCompilation.Create(AssemblyName, syntaxTrees, FrameworkReferences, compilationOptions);

        var generator = new TGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator], parseOptions: parseOptions);
        var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var driverRunResult = updatedDriver.GetRunResult();
        var generatorRunResult = driverRunResult.Results.Single();

        ImmutableArray<Diagnostic> compilationDiagnostics =
        [
            .. outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning),
        ];

        return new SourceGeneratorOutput(
            driverRunResult.Diagnostics,
            generatorRunResult.GeneratedSources,
            compilationDiagnostics);
    }

    // Only the shared framework directory is taken from the TPA list: the test host's own
    // assemblies (e.g. Refedle.App) would otherwise clash with the stub types in the sources.
    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedPlatformAssemblies)
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not available in this runtime.");
        }

        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var assemblyPaths = trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return
        [
            .. assemblyPaths
                .Where(path => string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.Ordinal))
                .Select(path => MetadataReference.CreateFromFile(path)),
        ];
    }
}
