using BingoBenji.Data;
using BingoBenji.Models;
using BingoBenji.Services;
using BingoBenji.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Controllers;

public class HomeController : Controller
{
    private readonly BingoBenjiDbContext _db;
    private readonly GenerationCodeService _codeSvc;
    private readonly BingoCardGenerator _gen;
    private readonly PdfService _pdf;
    private readonly ZipService _zip;

    public HomeController(
        BingoBenjiDbContext db,
        GenerationCodeService codeSvc,
        BingoCardGenerator gen,
        PdfService pdf,
        ZipService zip)
    {
        _db = db;
        _codeSvc = codeSvc;
        _gen = gen;
        _pdf = pdf;
        _zip = zip;
    }

    public async Task<IActionResult> Index()
    {
        var active = await GetActiveGenerationAsync();

        var vm = new HomeVm
        {
            ActiveGeneration = active
        };

        if (active != null)
        {
            vm.TotalSheets = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id);
            vm.StockUnassigned = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id && s.Status == "Unassigned");
            vm.SoldCount = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id && s.Status == "Sold");
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate1000()
    {
        var active = await GetActiveGenerationAsync();

        if (active == null)
        {
            active = await CreateNewActiveGenerationAsync();
        }

        // Si ya existen 1000, no regeneramos aquí
        var existing = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id);
        if (existing >= 1000)
        {
            TempData["Info"] = "Ya existen 1000 tablas generadas para esta generación.";
            return RedirectToAction(nameof(Index));
        }

        await GenerateSheetsForGenerationAsync(active, 1000);

        TempData["Success"] = $"Generadas 1000 tablas. Gen: {active.GenerationCode}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate()
    {
        // 1) Desactivar la generación activa actual (si existe)
        var actives = await _db.BingoGenerations.Where(g => g.IsActive).ToListAsync();
        foreach (var g in actives) g.IsActive = false;

        await _db.SaveChangesAsync();

        // 2) Crear nueva generación activa
        var newGen = await CreateNewActiveGenerationAsync();

        // 3) Generar 1000 nuevas sin borrar las antiguas
        await GenerateSheetsForGenerationAsync(newGen, 1000);

        TempData["Success"] = $"Regeneradas 1000 tablas. Nueva Gen: {newGen.GenerationCode}";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> Preview(int quantity = 1)
    {
        var active = await GetActiveGenerationAsync();
        if (active == null)
            return RedirectToAction(nameof(Index));

        quantity = Math.Clamp(quantity, 1, 50); // te dejo hasta 50 para preview, si quieres 1..10 dime.

        var stock = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id && s.Status == "Unassigned");
        if (stock == 0)
        {
            return View(new PreviewSelectionVm
            {
                ActiveGeneration = active,
                StockUnassigned = 0,
                Quantity = quantity,
                Message = "No hay stock. Regenera o crea nueva generación."
            });
        }

        if (stock < quantity)
            quantity = stock;

        // Elegir aleatorio de las no vendidas, SIN reservar (solo mostrar)
        var ids = await _db.BingoSheets
            .Where(s => s.GenerationId == active.Id && s.Status == "Unassigned")
            .Select(s => s.Id)
            .ToListAsync();

        var rng = new Random();
        var chosenIds = ids.OrderBy(_ => rng.Next()).Take(quantity).ToList();

        var preview = await _db.BingoSheets
            .Where(s => chosenIds.Contains(s.Id))
            .OrderBy(s => s.SheetNumber)
            .ToListAsync();

        return View(new PreviewSelectionVm
        {
            ActiveGeneration = active,
            StockUnassigned = stock,
            Quantity = quantity,
            PreviewSheets = preview
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int[] sheetIds)
    {
        if (sheetIds == null || sheetIds.Length == 0)
        {
            TempData["Error"] = "No se recibieron tablas para comprar.";
            return RedirectToAction(nameof(Index));
        }

        var active = await GetActiveGenerationAsync();
        if (active == null)
        {
            TempData["Error"] = "No hay generación activa.";
            return RedirectToAction(nameof(Index));
        }

        // Traer solo tablas no vendidas y que coincidan con los ids seleccionados
        var sheets = await _db.BingoSheets
            .Where(s => s.GenerationId == active.Id && s.Status == "Unassigned" && sheetIds.Contains(s.Id))
            .ToListAsync();

        if (sheets.Count == 0)
        {
            TempData["Error"] = "Esas tablas ya no están disponibles (quizá ya fueron compradas).";
            return RedirectToAction(nameof(Index));
        }

        foreach (var s in sheets)
        {
            s.Status = "Sold";
            s.SoldAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        // Generar PDFs y devolver ZIP
        var files = new Dictionary<string, byte[]>();
        foreach (var s in sheets.OrderBy(x => x.SheetNumber))
        {
            var pdf = _pdf.GenerateSheetPdf(s.GenerationCode, s.SheetNumber, s.CardsJson);
            var name = $"Gen_{s.GenerationCode}_Tabla_{s.SheetNumber}.pdf";
            files[name] = pdf;
        }

        var zipBytes = _zip.BuildZip(files);
        var zipName = $"BingoBenji_Compra_{active.GenerationCode}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

        return File(zipBytes, "application/zip", zipName);
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(string generationCode, int sheetNumber)
    {
        generationCode = (generationCode ?? "").Trim();
        if (generationCode.Length != 10 || sheetNumber <= 0)
            return NotFound("Parámetros inválidos.");

        var sheet = await _db.BingoSheets
            .FirstOrDefaultAsync(s => s.GenerationCode == generationCode && s.SheetNumber == sheetNumber);

        if (sheet == null) return NotFound("Tabla no encontrada.");

        var bytes = _pdf.GenerateSheetPdf(sheet.GenerationCode, sheet.SheetNumber, sheet.CardsJson);
        var fileName = $"Gen_{sheet.GenerationCode}_Tabla_{sheet.SheetNumber}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    // -------------------------
    // Helpers internos
    // -------------------------
    private async Task<BingoGeneration?> GetActiveGenerationAsync()
    {
        return await _db.BingoGenerations
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(g => g.IsActive);
    }

    private async Task<BingoGeneration> CreateNewActiveGenerationAsync()
    {
        // intentar crear un código único
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var code = _codeSvc.Create10();

            var exists = await _db.BingoGenerations.AnyAsync(g => g.GenerationCode == code);
            if (exists) continue;

            var gen = new BingoGeneration
            {
                GenerationCode = code,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _db.BingoGenerations.Add(gen);
            await _db.SaveChangesAsync();
            return gen;
        }

        throw new Exception("No se pudo generar un GenerationCode único (muy raro).");
    }

    private async Task GenerateSheetsForGenerationAsync(BingoGeneration gen, int total)
    {
        // Generar con garantía anti-duplicado por ContentHash (unique index)
        // Si hay colisión de hash (contenido repetido), reintenta esa tabla.
        var toAdd = new List<BingoSheet>(total);

        int sheetNumberStart = 1;
        var existingMax = await _db.BingoSheets
            .Where(s => s.GenerationId == gen.Id)
            .Select(s => (int?)s.SheetNumber)
            .MaxAsync() ?? 0;

        sheetNumberStart = existingMax + 1;

        int created = 0;
        int sheetNumber = sheetNumberStart;

        while (created < total && sheetNumber <= 1000)
        {
            var (json, hash) = _gen.GenerateSheetPayloadAndHash();

            var sheet = new BingoSheet
            {
                GenerationId = gen.Id,
                GenerationCode = gen.GenerationCode,
                SheetNumber = sheetNumber,
                Status = "Unassigned",
                CardsJson = json,
                ContentHash = hash,
                CreatedAt = DateTime.Now
            };

            _db.BingoSheets.Add(sheet);

            try
            {
                await _db.SaveChangesAsync();
                created++;
                sheetNumber++;
            }
            catch (DbUpdateException)
            {
                // Duplicado por ContentHash o SheetNumber, reintentar
                _db.ChangeTracker.Clear();
                // No incrementamos sheetNumber: reintenta para el mismo número
            }
        }

        // Si por alguna razón no llegó a 1000 (rarísimo), avisa por TempData desde controlador.
    }
}
