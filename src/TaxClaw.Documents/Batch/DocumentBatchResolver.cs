using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Batch;

/// <summary>A file found while expanding a folder or archive, with its bytes and a provenance name.</summary>
public sealed record ResolvedDocument(string Name, byte[] Bytes);

/// <summary>
/// Expands a single input path — an ordinary file, a directory, or an archive — into the one or more
/// documents it contains, so <c>/doc</c> can accept "drop a folder/zip of statements" as well as a
/// single file. Directories are scanned recursively; archives are read in memory (never extracted to
/// disk). Both skip hidden entries (dotfiles/dot-directories) and anything whose extension isn't one
/// the pipeline recognizes (<see cref="SourceDocument.HasKnownExtension"/>), so unrelated files (.git
/// metadata, .DS_Store, READMEs, nested archives) are skipped quietly rather than fed in as noise.
/// </summary>
public static class DocumentBatchResolver
{
    /// <summary>Whether a path's extension marks it as a supported archive (.zip, .tar, .tar.gz/.tgz).</summary>
    public static bool IsArchive(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a single ordinary file as one document.</summary>
    public static async IAsyncEnumerable<ResolvedDocument> ResolveFileAsync(
        string filePath, [EnumeratorCancellation] CancellationToken ct = default)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath, ct);
        yield return new ResolvedDocument(Path.GetFileName(filePath), bytes);
    }

    /// <summary>Recursively scans a directory for recognized document files.</summary>
    public static async IAsyncEnumerable<ResolvedDocument> ResolveDirectoryAsync(
        string directoryPath, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            string relative = Path.GetRelativePath(directoryPath, filePath);
            if (IsHidden(relative) || !SourceDocument.HasKnownExtension(filePath))
            {
                continue;
            }

            byte[] bytes = await File.ReadAllBytesAsync(filePath, ct);
            yield return new ResolvedDocument(Normalize(relative), bytes);
        }
    }

    /// <summary>Reads recognized document entries out of a .zip/.tar/.tar.gz/.tgz archive, in memory.</summary>
    public static async IAsyncEnumerable<ResolvedDocument> ResolveArchiveAsync(
        string archivePath, [EnumeratorCancellation] CancellationToken ct = default)
    {
        IAsyncEnumerable<ResolvedDocument> entries = archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? ResolveZipAsync(archivePath, ct)
            : ResolveTarAsync(archivePath, ct);

        await foreach (ResolvedDocument doc in entries)
        {
            yield return doc;
        }
    }

    private static async IAsyncEnumerable<ResolvedDocument> ResolveZipAsync(
        string archivePath, [EnumeratorCancellation] CancellationToken ct)
    {
        using ZipArchive zip = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // Directory entries have an empty Name (their FullName ends with '/').
            if (entry.Name.Length == 0 || IsHidden(entry.FullName) || !SourceDocument.HasKnownExtension(entry.Name))
            {
                continue;
            }

            using Stream entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, ct);
            yield return new ResolvedDocument(Normalize(entry.FullName), buffer.ToArray());
        }
    }

    private static async IAsyncEnumerable<ResolvedDocument> ResolveTarAsync(
        string archivePath, [EnumeratorCancellation] CancellationToken ct)
    {
        await using Stream fileStream = File.OpenRead(archivePath);
        await using Stream tarStream = archivePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
            ? fileStream
            : new GZipStream(fileStream, CompressionMode.Decompress);

        await using var reader = new TarReader(tarStream);
        while (await reader.GetNextEntryAsync(cancellationToken: ct) is { } entry)
        {
            if (entry.EntryType != TarEntryType.RegularFile
                || IsHidden(entry.Name)
                || !SourceDocument.HasKnownExtension(entry.Name))
            {
                continue;
            }

            using var buffer = new MemoryStream();
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(buffer, ct);
            }
            yield return new ResolvedDocument(Normalize(entry.Name), buffer.ToArray());
        }
    }

    /// <summary>True if any path segment is hidden (starts with '.'), e.g. ".git/", ".DS_Store".</summary>
    private static bool IsHidden(string relativePath) =>
        relativePath.Split('/', '\\').Any(segment => segment.StartsWith('.'));

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');
}
