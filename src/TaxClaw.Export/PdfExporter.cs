using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>
/// Renders the return as a PDF approximating form 25 5405 (one row per populated line). This is
/// export milestone 2. The exact official layout is refined against the downloaded form template.
/// </summary>
public sealed class PdfExporter : IReturnExporter<byte[]>
{
    static PdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Export(TaxReturn taxReturn)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Text($"Přiznání k dani z příjmů FO — {taxReturn.Year}").Bold().FontSize(14);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Řádek").Bold();
                        header.Cell().Text("Částka (Kč)").Bold();
                    });

                    foreach ((string lineId, decimal value) in taxReturn.Lines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        table.Cell().Text(lineId);
                        table.Cell().Text(value.ToString());
                    }
                });

                page.Footer().Text("Computed draft — not tax advice.").FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
}
