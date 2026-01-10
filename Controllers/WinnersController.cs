using BingoBenji.Data;
using BingoBenji.Models;
using BingoBenji.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Controllers;

public class WinnersController : Controller
{
    private readonly BingoBenjiDbContext _db;

    public WinnersController(BingoBenjiDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Create()
    {
        return View(new WinnerCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WinnerCreateVm vm)
    {
        vm.GenerationCode = (vm.GenerationCode ?? "").Trim();

        if (vm.GenerationCode.Length != 10)
            ModelState.AddModelError(nameof(vm.GenerationCode), "El código de generación debe tener 10 caracteres.");

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

        var q = _db.Winners.AsQueryable();

        if (!string.IsNullOrWhiteSpace(gen))
        {
            gen = gen.Trim();
            q = q.Where(x => x.GenerationCode.Contains(gen));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            name = name.Trim();
            q = q.Where(x => x.PlayerName.Contains(name));
        }

        if (sheet.HasValue)
        {
            q = q.Where(x => x.SheetNumber == sheet.Value);
        }

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
            FilterGenerationCode = gen,
            FilterName = name,
            FilterSheetNumber = sheet
        };

        return View(vm);
    }
}
