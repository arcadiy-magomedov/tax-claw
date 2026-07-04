namespace TaxClaw.Documents.Model;

/// <summary>Broad category of file content, used to choose the extraction path.</summary>
public enum MediaKind { Pdf, Image, Tabular, Text, Unknown }

/// <summary>A raw document handed to the pipeline. Content is untrusted input.</summary>
public sealed record SourceDocument(string FileName, byte[] Bytes, MediaKind Kind)
{
    public static SourceDocument FromBytes(string fileName, byte[] bytes) =>
        new(fileName, bytes, InferKind(fileName));

    /// <summary>
    /// Whether a filename's extension is one <see cref="InferKind"/> recognizes. Used to filter
    /// folder/archive scans so unrelated files (.DS_Store, READMEs, nested archives, ...) are
    /// skipped quietly rather than fed to the pipeline as noise.
    /// </summary>
    public static bool HasKnownExtension(string fileName) => InferKind(fileName) != MediaKind.Unknown;

    private static MediaKind InferKind(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => MediaKind.Pdf,
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".heif" or ".tif" or ".tiff" => MediaKind.Image,
            ".csv" or ".xlsx" or ".xls" => MediaKind.Tabular,
            ".txt" => MediaKind.Text,
            _ => MediaKind.Unknown
        };
}
