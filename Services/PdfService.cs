using BingoBenji.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace BingoBenji.Services;

public class PdfService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public PdfService(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    public byte[] GenerateSheetPdf(string generationCode, int sheetNumber, string cardsJson)
    {
        var payload = JsonSerializer.Deserialize<BingoSheetPayload>(cardsJson) ?? new BingoSheetPayload();

        // Ruta fija (NO BD): wwwroot/images/watermark.jpg
        var watermarkRel = _config["BingoBenji:WatermarkImageRelativePath"] ?? "images/watermark.jpg";
        var watermarkPath = Path.Combine(_env.WebRootPath, watermarkRel.Replace('/', Path.DirectorySeparatorChar));

        byte[]? watermarkBytes = File.Exists(watermarkPath) ? File.ReadAllBytes(watermarkPath) : null;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(10));

                // Marca de agua (sin Opacity para compatibilidad)
                // Tip: si la quieres más transparente, guarda watermark.jpg ya “lavada”.
                page.Background().Element(bg =>
                {
                    if (watermarkBytes == null) return;

                    bg.AlignCenter().AlignMiddle()
                      .Image(watermarkBytes, ImageScaling.FitArea);
                });

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text($"BingoBenji • Gen: {generationCode}")
                        .SemiBold().FontSize(14);

                    row.ConstantItem(220).AlignRight()
                        .Text($"Tabla #{sheetNumber}")
                        .SemiBold().FontSize(14);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    // 2 columnas -> 2x2 para 4 cartones
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(0), generationCode));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(1), generationCode));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(2), generationCode));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(3), generationCode));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generado: ");
                    t.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void BuildCard(IContainer container, BingoCardPayload? card, string generationCode)
    {
        container.Border(1).Padding(8).Height(260).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("B   I   N   G   O").SemiBold().FontSize(12);
                r.ConstantItem(150).AlignRight().Text($"Gen: {generationCode}").SemiBold().FontSize(10);
            });

            if (card == null || card.Grid.Length != 5)
            {
                col.Item().PaddingTop(10).Text("Cartón inválido").FontColor("#FF9999");
                return;
            }

            col.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                for (int rr = 0; rr < 5; rr++)
                {
                    for (int cc = 0; cc < 5; cc++)
                    {
                        var val = card.Grid[rr][cc];
                        t.Cell().Border(1).MinHeight(26)
                            .AlignCenter().AlignMiddle()
                            .Text(val.ToString()).FontSize(12);
                    }
                }
            });
        });
    }
}
