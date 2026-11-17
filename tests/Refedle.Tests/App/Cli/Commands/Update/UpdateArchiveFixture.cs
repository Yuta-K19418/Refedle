using System.Formats.Tar;
using System.IO.Compression;

namespace Refedle.Tests.App.Cli.Commands.Update;

/// <summary>
/// Creates an isolated temp directory with a target file and a <c>.tar.gz</c> archive for
/// <see cref="Refedle.App.Cli.Commands.Update.ArchiveBinaryReplacer"/> tests. Disposing removes the
/// whole directory tree.
/// </summary>
internal sealed class UpdateArchiveFixture : IDisposable
{
    private const string BinaryEntryName = "refedle";

    /// <summary>The bytes written to the target file before a replacement is attempted.</summary>
    public static readonly byte[] OriginalTargetBytes = "old refedle binary"u8.ToArray();

    private UpdateArchiveFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"refedle-replacer-{Guid.NewGuid():N}");
        TargetDirectory = Directory.CreateDirectory(Path.Combine(RootPath, "bin"));
        TargetPath = Path.Combine(TargetDirectory.FullName, BinaryEntryName);
        ArchivePath = Path.Combine(RootPath, "refedle-v1.2.3-linux-x64.tar.gz");
        File.WriteAllBytes(TargetPath, OriginalTargetBytes);
    }

    public string RootPath { get; }

    public DirectoryInfo TargetDirectory { get; }

    public string TargetPath { get; }

    public string ArchivePath { get; }

    /// <summary>Builds a fixture whose archive contains a <c>refedle</c> entry with the given bytes.</summary>
    public static UpdateArchiveFixture WithBinaryEntry(byte[] binaryContent)
    {
        UpdateArchiveFixture fixture = new();
        fixture.WriteTarGz([(BinaryEntryName, binaryContent)]);
        return fixture;
    }

    /// <summary>Builds a fixture whose archive is valid but has no <c>refedle</c> entry.</summary>
    public static UpdateArchiveFixture WithoutBinaryEntry()
    {
        UpdateArchiveFixture fixture = new();
        fixture.WriteTarGz([("README.md", "not the binary"u8.ToArray())]);
        return fixture;
    }

    /// <summary>Builds a fixture whose archive starts with the gzip magic but holds garbage.</summary>
    public static UpdateArchiveFixture WithCorruptGzip()
    {
        UpdateArchiveFixture fixture = new();
        File.WriteAllBytes(fixture.ArchivePath, [0x1F, 0x8B, 0x08, 0x00, 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04]);
        return fixture;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private void WriteTarGz(IReadOnlyList<(string name, byte[] content)> entries)
    {
        using FileStream fileStream = File.Create(ArchivePath);
        using GZipStream gzipStream = new(fileStream, CompressionLevel.Optimal);
        using TarWriter tarWriter = new(gzipStream, TarEntryFormat.Pax);
        foreach ((string? name, byte[]? content) in entries)
        {
            using MemoryStream dataStream = new(content);
            PaxTarEntry entry = new(TarEntryType.RegularFile, name) { DataStream = dataStream };
            tarWriter.WriteEntry(entry);
        }
    }
}
