using BingoBenji.Data;
using BingoBenji.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Controllers;

public class ReporteriaController : Controller
{
    private readonly BingoBenjiDbContext _db;

    public ReporteriaController(BingoBenjiDbContext db)
    {
        _db = db;
    }

    private async Task<List<SelectListItem>> LoadGenerationOptionsAsync(string? selected)
    {
        var gens = await _db.BingoGenerations
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => g.GenerationCode)
            .ToListAsync();

        var items = new List<SelectListItem>
        {
            new SelectListItem
            {
                Value = "",
                Text = "Todas las generaciones",
                Selected = string.IsNullOrWhiteSpace(selected)
            }
        };

        items.AddRange(gens.Select(code => new SelectListItem
        {
            Value = code,
            Text = code,
            Selected = (!string.IsNullOrWhiteSpace(selected) && code == selected)
        }));

        return items;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? gen, DateTime? from, DateTime? to)
    {
        gen = (gen ?? "").Trim();

        // Normalizar rango: to inclusive (fin del día)
        DateTime? fromDt = from?.Date;
        DateTime? toDtExclusive = to?.Date.AddDays(1); // exclusivo

        var q = _db.Winners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(gen))
            q = q.Where(w => w.GenerationCode == gen);

        if (fromDt.HasValue)
            q = q.Where(w => w.CreatedAt >= fromDt.Value);

        if (toDtExclusive.HasValue)
            q = q.Where(w => w.CreatedAt < toDtExclusive.Value);

        // KPIs
        var totalWinners = await q.CountAsync();
        var totalPrizeAll = await q.SumAsync(w => (decimal?)w.PrizeAmount) ?? 0m;

        // Por generación
        var byGen = await q
            .GroupBy(w => w.GenerationCode)
            .Select(g => new ReporteriaVm.GenSummary
            {
                GenerationCode = g.Key,
                WinnersCount = g.Count(),
                TotalPrize = g.Sum(x => x.PrizeAmount)
            })
            .OrderByDescending(x => x.TotalPrize)
            .ToListAsync();

        // Serie diaria
        var daily = await q
            .GroupBy(w => w.CreatedAt.Date)
            .Select(g => new ReporteriaVm.DailySeriesPoint
            {
                Date = g.Key,
                TotalPrize = g.Sum(x => x.PrizeAmount),
                WinnersCount = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var vm = new ReporteriaVm
        {
            FilterGenerationCode = string.IsNullOrWhiteSpace(gen) ? null : gen,
            DateFrom = fromDt,
            DateTo = to?.Date,
            GenerationOptions = await LoadGenerationOptionsAsync(string.IsNullOrWhiteSpace(gen) ? null : gen),

            TotalWinners = totalWinners,
            TotalPrizeAll = totalPrizeAll,
            ByGeneration = byGen,
            DailyPrizeSeries = daily
        };

        return View(vm);
    }
}
