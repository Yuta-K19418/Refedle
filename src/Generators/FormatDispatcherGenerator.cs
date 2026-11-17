using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Refedle.Generators;

/// <summary>Generates dispatch code for record readers and writers.</summary>
[Generator]
public class FormatDispatcherGenerator : IIncrementalGenerator
{
    /// <summary>Initializes the incremental generation pipeline.</summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all reader and writer declarations in a single pass
        IncrementalValueProvider<List<FormatInfo>> allDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (s, _) => HasAttributes(s),
                transform: static (ctx, _) => GetFormatAndType(ctx)
            )
            .Collect()
            .Select(static (array, _) => FormatDispatcherGenerator.FilterNulls(array));

        // Pass the collected list directly
        context.RegisterSourceOutput(
            allDeclarations,
            static (spc, source) =>
            {
                Execute(source, spc);
            }
        );
    }

    private static List<FormatInfo> FilterNulls(ImmutableArray<FormatInfo?> array)
    {
        List<FormatInfo> result = [];
        foreach (FormatInfo? item in array)
        {
            if (item is null)
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static bool HasAttributes(SyntaxNode node)
    {
        if (node is StructDeclarationSyntax structDecl && structDecl.AttributeLists.Count > 0)
        {
            return true;
        }

        if (node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Count > 0)
        {
            return true;
        }

        return false;
    }

    private static FormatInfo? GetFormatAndType(GeneratorSyntaxContext context)
    {
        TypeDeclarationSyntax typeDecl = (TypeDeclarationSyntax)context.Node;

        foreach (AttributeSyntax? attribute in typeDecl.AttributeLists.SelectMany(al => al.Attributes))
        {
            if (TryExtractFormatInfo(context, typeDecl, attribute, out FormatInfo? formatInfo))
            {
                return formatInfo;
            }
        }

        return null;
    }

    private static bool TryExtractFormatInfo(
        GeneratorSyntaxContext context,
        TypeDeclarationSyntax typeDecl,
        AttributeSyntax attribute,
        out FormatInfo? formatInfo
    )
    {
        formatInfo = null;
        string name = attribute.Name.ToString();
        if (!name.Contains("RecordReader") && !name.Contains("RecordWriter"))
        {
            return false;
        }

        string? arg = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString();

        if (arg is not null)
        {
            string formatValue = arg; // e.g. "DataFormat.Csv"
            if (formatValue.StartsWith("DataFormat.", StringComparison.Ordinal))
            {
                formatValue = formatValue.Substring("DataFormat.".Length);
            }

            string createdType = ExtractCreatedType(typeDecl);

            string typeName = context.SemanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol declaredSymbol
                ? declaredSymbol.Name
                : string.Empty;
            bool isReader = name.Contains("RecordReader");

            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(createdType))
            {
                formatInfo = new FormatInfo(formatValue, typeName, createdType, isReader);
                return true;
            }
        }

        return false;
    }

    private static string ExtractCreatedType(TypeDeclarationSyntax typeDecl)
    {
        if (typeDecl.BaseList is null)
        {
            return "";
        }

        foreach (BaseTypeSyntax baseType in typeDecl.BaseList.Types)
        {
            string baseStr = baseType.Type.ToString();
            if (
                !baseStr.Contains("IRecordReaderFactory<")
                && !baseStr.Contains("IRecordWriterFactory<")
            )
            {
                continue;
            }

            int startIndex = baseStr.IndexOf('<') + 1;
            // LastIndexOf, not IndexOf: a nested generic reader type (e.g.
            // IRecordReaderFactory<FullAggregationRecordReader<JsonArrayBatchSourceReader>>)
            // must be sliced at the outermost '>' so the closed type stays intact.
            int endIndex = baseStr.LastIndexOf('>');
            if (startIndex > 0 && endIndex > startIndex)
            {
                return baseStr.Substring(startIndex, endIndex - startIndex);
            }
        }

        return "";
    }

    private static void Execute(
        IReadOnlyList<FormatInfo> allItems,
        SourceProductionContext context
    )
    {
        // Extract readers and writers separately
        List<FormatInfo> readers = [.. allItems.Where(i => i.IsReader)];
        List<FormatInfo> writers = [.. allItems.Where(i => !i.IsReader)];

        if (readers.Count == 0 || writers.Count == 0)
        {
            return;
        }

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Refedle.Engine;");
        sb.AppendLine("using Refedle.Engine.Types;");
        sb.AppendLine("using Refedle.Engine.IO.DrillDown;");
        sb.AppendLine("using Refedle.App.Cli;");
        sb.AppendLine("using Refedle.App.Cli.Commands.Apply;");
        sb.AppendLine("using Refedle.App.Cli.IO.Csv;");
        sb.AppendLine("using Refedle.App.Cli.IO.Factories;");
        sb.AppendLine("using Refedle.App.Cli.IO.Json;");
        sb.AppendLine();
        sb.AppendLine("namespace Refedle.App.Cli.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class FormatDispatcher");
        sb.AppendLine("{");
        sb.AppendLine("    public static async ValueTask<BatchRunResult> DispatchAsync(");
        sb.AppendLine("        DataFormat inputFormat,");
        sb.AppendLine("        DataFormat outputFormat,");
        sb.AppendLine("        string inputFile,");
        sb.AppendLine("        string outputFile,");
        sb.AppendLine("        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,");
        sb.AppendLine("        IReadOnlyList<string> inputColumnNames,");
        sb.AppendLine("        BatchOutputSchema outputSchema,");
        sb.AppendLine("        IAppLogger logger,");
        sb.AppendLine("        CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return (inputFormat, outputFormat) switch");
        sb.AppendLine("        {");

        foreach (FormatInfo? reader in readers)
        {
            foreach (FormatInfo? writer in writers)
            {
                sb.AppendLine(
                    $"            (DataFormat.{reader.FormatName}, DataFormat.{writer.FormatName}) =>"
                );
                sb.AppendLine(
                    $"                await Run{reader.FormatName}To{writer.FormatName}Async(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),"
                );
            }
        }

        sb.AppendLine(
            "            _ => throw new NotSupportedException($\"Unsupported format combination: {inputFormat} -> {outputFormat}\")"
        );
        sb.AppendLine("        };");
        sb.AppendLine("    }");

        foreach (FormatInfo? reader in readers)
        {
            foreach (FormatInfo? writer in writers)
            {
                sb.AppendLine();
                sb.AppendLine(
                    $"    private static async ValueTask<BatchRunResult> Run{reader.FormatName}To{writer.FormatName}Async("
                );
                sb.AppendLine("        string inputFile,");
                sb.AppendLine("        string outputFile,");
                sb.AppendLine("        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,");
                sb.AppendLine("        IReadOnlyList<string> inputColumnNames,");
                sb.AppendLine("        BatchOutputSchema outputSchema,");
                sb.AppendLine("        IAppLogger logger,");
                sb.AppendLine("        CancellationToken ct)");
                sb.AppendLine("    {");

                sb.AppendLine($"        var readerFactory = new {reader.FactoryTypeName}();");
                sb.AppendLine(
                    $"        using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);"
                );

                sb.AppendLine($"        var writerFactory = new {writer.FactoryTypeName}();");
                sb.AppendLine(
                    $"        await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false);"
                );

                sb.AppendLine(
                    $"        return await RecordProcessor.ProcessAsync<{reader.CreatedTypeName}, {writer.CreatedTypeName}>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);"
                );
                sb.AppendLine("    }");
            }
        }

        sb.AppendLine("}");

        context.AddSource("FormatDispatcher.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private sealed record FormatInfo(
        string FormatName,
        string FactoryTypeName,
        string CreatedTypeName,
        bool IsReader
    );
}
