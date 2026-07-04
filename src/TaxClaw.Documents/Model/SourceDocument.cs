namespace TaxClaw.Documents.Model;

/// <summary>Broad category of file content, used to choose the extraction path.</summary>
public enum MediaKind { Pdf, Image, Tabular, Text, Unknown }

/// <summary>A raw document handed to the pipeline. Content is untrusted input.</summary>
public sealed record SourceDocument(string FileName, byte[] Bytes, MediaKind Kind)
{
    public static SourceDocument FromBytes(string fileName, byte[] bytes) =>
        new(fileName, bytes, InferKind(fileName));

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
