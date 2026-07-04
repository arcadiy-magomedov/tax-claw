using Microsoft.Extensions.AI;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>
/// Vision-LLM <see cref="IRecognizer"/>: sends the document image/PDF bytes to a vision-capable
/// model and asks for a verbatim transcription. Requires a provider whose model accepts image input
/// (OpenAI/Azure/Anthropic vision). An offline Tesseract/OS-Vision recognizer is the privacy-mode
/// alternative behind the same seam (lands with Plan 7). Document content stays data — the text it
/// returns is fed to the schema-bound extractor, never executed as instructions.
/// </summary>
public sealed class VisionRecognizer(IChatClient client) : IRecognizer
{
    private const string Instruction =
        "Transcribe all text from this tax document verbatim, preserving labels and their values. "
        + "Output only the transcribed text, no commentary.";

    public async Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default)
    {
        List<AIContent> content =
        [
            new TextContent(Instruction),
            new DataContent(doc.Bytes, MediaType(doc.FileName))
        ];

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, content)], new ChatOptions { Temperature = 0 }, ct);

        return new ExtractedText(response.Text, UsedRecognition: true);
    }

    private static string MediaType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".heic" or ".heif" => "image/heic",
        ".tif" or ".tiff" => "image/tiff",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
