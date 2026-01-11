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
    private readonly ZipBatchJobService _zipJob;

    public HomeController(
        BingoBenjiDbContext db,
        GenerationCodeService codeSvc,
        BingoCardGenerator gen,
        PdfService pdf,
        ZipBatchJobService zipJob)
    {
        _db = db;
        _codeSvc = codeSvc;
        _gen = gen;
        _pdf = pdf;
        _zipJob = zipJob;
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
        else
        {
            vm.TotalSheets = 0;
            vm.StockUnassigned = 0;
            vm.SoldCount = 0;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate1000()
    {
        var active = await GetActiveGenerationAsync();
        if (active == null)
            active = await CreateNewActiveGenerationAsync();

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
        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // 1) Desactivar activa
                var actives = await _db.BingoGenerations.Where(g => g.IsActive).ToListAsync();
                foreach (var g in actives) g.IsActive = false;
                await _db.SaveChangesAsync();

                // 2) Borrar SOLO BingoSheets
                await _db.BingoSheets.ExecuteDeleteAsync();

                // 3) Nueva generación activa
                var newGen = await CreateNewActiveGenerationAsync();

                // 4) Generar 1000 nuevas
                await GenerateSheetsForGenerationAsync(newGen, 1000);

                await tx.CommitAsync();
            });

            TempData["Success"] = "Regeneradas 1000 tablas. Nueva generación creada.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            return BadRequest("Error al regenerar: " + ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAllZipStatus()
    {
        var active = await GetActiveGenerationAsync();
        if (active == null)
            return Json(new { ok = false, message = "No hay generación activa." });

        var total = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id);
        var unassigned = await _db.BingoSheets.CountAsync(s => s.GenerationId == active.Id && s.Status == "Unassigned");
        var allSold = (total > 0 && unassigned == 0);

        return Json(new
        {
            ok = true,
            generationCode = active.GenerationCode,
            total,
            unassigned,
            allSold
        });
    }

    // -------- JOB START (con progreso) --------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDownloadZipJob(bool markUnassignedAsSold)
    {
        var active = await GetActiveGenerationAsync();
        if (active == null)
            return BadRequest("No hay generación activa.");

        var jobId = _zipJob.StartJob(active.GenerationCode, markUnassignedAsSold);
        return Json(new { ok = true, jobId });
    }

    [HttpGet]
    public IActionResult ZipJobStatus(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Json(new { ok = false, message = "Job inválido." });

        var job = _zipJob.Get(id);
        if (job == null)
            return Json(new { ok = false, message = "Job no encontrado." });

        return Json(new
        {
            ok = true,
            status = job.Status.ToString(),
            progress = job.Progress,
            message = job.Message
        });
    }

    [HttpGet]
    public IActionResult DownloadZipJob(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Job inválido.");

        var job = _zipJob.Get(id);
        if (job == null)
            return NotFound("Job no encontrado.");

        if (job.Status != ZipBatchJobService.JobStatus.Done || string.IsNullOrWhiteSpace(job.ZipPath))
            return BadRequest("ZIP aún no está listo.");

        if (!System.IO.File.Exists(job.ZipPath))
            return NotFound("ZIP no encontrado en disco.");

        var fileName = Path.GetFileName(job.ZipPath);
        var stream = new FileStream(job.ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/zip", fileName);
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

        var fileName = $"Gen_{sheet.GenerationCode}_Tabla_{sheet.SheetNumber:0000}.pdf";
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";

        return File(bytes, "application/pdf");
    }

    [HttpGet]
    public async Task<IActionResult> GenerationCodes()
    {
        var codes = await _db.BingoGenerations
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => g.GenerationCode)
            .ToListAsync();

        return Json(new { ok = true, codes });
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
        int created = 0;
        int sheetNumber = 1;

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
                _db.ChangeTracker.Clear();
            }
        }
    }
}
