using BingoBenji.Data;
using BingoBenji.Models;
using BingoBenji.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Controllers;

public class WinnersController : Controller
{
    private readonly BingoBenjiDbContext _db;

    public WinnersController(BingoBenjiDbContext db) => _db = db;

    private async Task<string?> GetActiveGenerationCodeAsync()
    {
        return await _db.BingoGenerations
            .Where(g => g.IsActive)
            .OrderByDescending(g => g.Id)
            .Select(g => g.GenerationCode)
            .FirstOrDefaultAsync();
    }

    private async Task<List<SelectListItem>> LoadGenerationOptionsAsync(string? selected = null)
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
                Text = "-- Selecciona generación --",
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
    public async Task<IActionResult> Create()
    {
        // ✅ Default = generación activa (pero deja elegir otra)
        var activeCode = await GetActiveGenerationCodeAsync();

        var vm = new WinnerCreateVm
        {
            GenerationCode = activeCode ?? "",
            GenerationOptions = await LoadGenerationOptionsAsync(activeCode)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WinnerCreateVm vm)
    {
        vm.GenerationCode = (vm.GenerationCode ?? "").Trim();

        // Recargar dropdown con lo que el usuario eligió
        vm.GenerationOptions = await LoadGenerationOptionsAsync(vm.GenerationCode);

        if (string.IsNullOrWhiteSpace(vm.GenerationCode) || vm.GenerationCode.Length != 10)
            ModelState.AddModelError(nameof(vm.GenerationCode), "Selecciona un código de generación válido (10 caracteres).");

        if (string.IsNullOrWhiteSpace(vm.PlayerName))
            ModelState.AddModelError(nameof(vm.PlayerName), "Nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(vm.BankName))
            ModelState.AddModelError(nameof(vm.BankName), "Banco es obligatorio.");

        if (string.IsNullOrWhiteSpace(vm.BankAccountNumber))
            ModelState.AddModelError(nameof(vm.BankAccountNumber), "Cuenta bancaria es obligatoria.");

        if (vm.SheetNumber <= 0 || vm.SheetNumber > 1000)
            ModelState.AddModelError(nameof(vm.SheetNumber), "Número de tabla inválido.");

        var type = (vm.VictoryType ?? "Full").Trim();
        if (type != "Full" && type != "Corners" && type != "Line")
            ModelState.AddModelError(nameof(vm.VictoryType), "Tipo inválido.");

        if (vm.PrizeAmount < 0)
            ModelState.AddModelError(nameof(vm.PrizeAmount), "El valor del premio no puede ser negativo.");

        if (!ModelState.IsValid) return View(vm);

        var winner = new Winner
        {
            GenerationCode = vm.GenerationCode,
            PlayerName = vm.PlayerName.Trim(),
            BankName = vm.BankName.Trim(),
            BankAccountNumber = vm.BankAccountNumber.Trim(),
            Phone = vm.Phone?.Trim(),
            SheetNumber = vm.SheetNumber,
            VictoryType = type,
            PrizeAmount = vm.PrizeAmount,
            Notes = vm.Notes?.Trim(),
            CreatedAt = DateTime.Now
        };

        _db.Winners.Add(winner);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Ganador guardado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? gen, string? name, int? sheet, int page = 1)
    {
        page = Math.Max(1, page);

        gen = (gen ?? "").Trim();
        name = (name ?? "").Trim();

        var q = _db.Winners.AsQueryable();

        if (!string.IsNullOrWhiteSpace(gen))
            q = q.Where(x => x.GenerationCode == gen);

        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(x => x.PlayerName.Contains(name));

        if (sheet.HasValue)
            q = q.Where(x => x.SheetNumber == sheet.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * 10)
            .Take(10)
            .ToListAsync();

        var vm = new WinnersListVm
        {
            Items = items,
            Page = page,
            PageSize = 10,
            Total = total,
            FilterGenerationCode = string.IsNullOrWhiteSpace(gen) ? null : gen,
            FilterName = string.IsNullOrWhiteSpace(name) ? null : name,
            FilterSheetNumber = sheet,
            GenerationOptions = await LoadGenerationOptionsAsync(string.IsNullOrWhiteSpace(gen) ? null : gen)
        };

        return View(vm);
    }
}
