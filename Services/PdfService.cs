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

        var watermarkBytes = TryLoadWebRootImageBytesSmart(
            _config["BingoBenji:WatermarkImageRelativePath"] ?? "Images/Watermark.jpeg"
        );

        var logoBytes = TryLoadWebRootImageBytesSmart(
            _config["BingoBenji:LogoImageRelativePath"] ?? "Images/logo.png"
        );

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(header =>
                {
                    header.Row(row =>
                    {
                        row.RelativeItem().AlignLeft().AlignMiddle()
                           .Text($"TABLA #{sheetNumber:0000}")
                           .SemiBold().FontSize(13);

                        row.RelativeItem().AlignCenter().AlignMiddle().Height(100).Element(e =>
                        {
                            if (logoBytes != null)
                                e.Image(logoBytes).FitArea();
                        });

                        row.RelativeItem().AlignRight().AlignMiddle().Column(col =>
                        {
                            col.Item().Text("Generación")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);

                            col.Item().Text(generationCode)
                                .SemiBold()
                                .FontSize(12)
                                .FontColor(Colors.Grey.Darken2);
                        });
                    });
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(0), watermarkBytes, 1));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(1), watermarkBytes, 2));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(2), watermarkBytes, 3));
                    table.Cell().Element(e => BuildCard(e, payload.Cards.ElementAtOrDefault(3), watermarkBytes, 4));
                });

                page.Footer().AlignCenter()
                    .Text($"BinguitoSemanal • Gen {generationCode} • Impresión: {DateTime.Now:yyyy-MM-dd HH:mm}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Lighten1);
            });
        });

        return doc.GeneratePdf();
    }

    private static void BuildCard(IContainer container, BingoCardPayload? card, byte[]? watermarkBytes, int cardIndex)
    {
        container
            .Padding(6)
            .Height(270)
            .Layers(layers =>
            {
                // Watermark POR CARTÓN (capa inferior)
                if (watermarkBytes != null)
                {
                    layers.Layer()
                        .AlignCenter()
                        .AlignMiddle()
                        .Image(watermarkBytes)
                        .FitArea();
                }

                // Capa principal: SIN fondo blanco (porque tapaba el watermark)
                layers.PrimaryLayer()
                    .Border(1)
                    .BorderColor(Colors.Grey.Darken2)
                    .Padding(8)
                    .Column(col =>
                    {
                        col.Item()
                            .Text($"TABLA #{cardIndex}")
                            .FontSize(9)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken2);

                        col.Item().PaddingTop(4).Element(BuildBingoHeader);

                        if (card == null || card.Grid.Length != 5)
                        {
                            col.Item().PaddingTop(10)
                                .Text("Cartón inválido")
                                .FontColor(Colors.Red.Lighten2);
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

                                    t.Cell()
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Darken2)
                                        .MinHeight(26)
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Text(val.ToString())
                                        .FontSize(12)
                                        .SemiBold();
                                }
                            }
                        });
                    });
            });
    }

    private static void BuildBingoHeader(IContainer container)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
            });

            string[] letters = { "B", "I", "N", "G", "O" };

            for (int i = 0; i < 5; i++)
            {
                t.Cell()
                    .Border(1)
                    .BorderColor(Colors.Grey.Darken2)
                    .Background(Colors.Orange.Lighten4)
                    .MinHeight(28)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(letters[i])
                    .FontSize(16)
                    .SemiBold();
            }
        });
    }

    private byte[]? TryLoadWebRootImageBytesSmart(string relPathOrBase)
    {
        string Normalize(string p) => (p ?? "").Trim().Replace('/', Path.DirectorySeparatorChar);

        var input = Normalize(relPathOrBase);
        var hasExt = Path.HasExtension(input);

        var candidates = new List<string>();

        void AddBothCases(string p)
        {
            candidates.Add(p);
            candidates.Add(p.Replace($"{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}images{Path.DirectorySeparatorChar}"));
            candidates.Add(p.Replace($"{Path.DirectorySeparatorChar}images{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}"));
        }

        if (hasExt)
        {
            AddBothCases(input);
        }
        else
        {
            var exts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            foreach (var ext in exts)
                AddBothCases(input + ext);
        }

        foreach (var p in candidates.Distinct())
        {
            var full = Path.Combine(_env.WebRootPath, p);
            if (File.Exists(full))
                return File.ReadAllBytes(full);
        }

        return null;
    }
}
