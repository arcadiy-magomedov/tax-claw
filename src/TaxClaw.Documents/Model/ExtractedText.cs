namespace TaxClaw.Documents.Model;

/// <summary>Text recovered from a document, noting whether recognition (OCR/Vision) was needed.</summary>
public sealed record ExtractedText(string Text, bool UsedRecognition, int PageCount = 1);
