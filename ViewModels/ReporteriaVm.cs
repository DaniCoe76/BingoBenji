using Microsoft.AspNetCore.Mvc.Rendering;

namespace BingoBenji.ViewModels;

public class ReporteriaVm
{
    // Filtros
    public string? FilterGenerationCode { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public List<SelectListItem> GenerationOptions { get; set; } = new();

    // KPIs
    public int TotalWinners { get; set; }
    public decimal TotalPrizeAll { get; set; }

    // Resumen por generación
    public List<GenSummary> ByGeneration { get; set; } = new();

    // Serie por día (premios entregados)
    public List<DailySeriesPoint> DailyPrizeSeries { get; set; } = new();

    public class GenSummary
    {
        public string GenerationCode { get; set; } = "";
        public int WinnersCount { get; set; }
        public decimal TotalPrize { get; set; }
    }

    public class DailySeriesPoint
    {
        public DateTime Date { get; set; }
        public decimal TotalPrize { get; set; }
        public int WinnersCount { get; set; }
    }
}
