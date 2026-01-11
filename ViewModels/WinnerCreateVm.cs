using Microsoft.AspNetCore.Mvc.Rendering;

namespace BingoBenji.ViewModels;

public class WinnerCreateVm
{
    public string GenerationCode { get; set; } = "";

    public List<SelectListItem> GenerationOptions { get; set; } = new();

    public string PlayerName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";
    public string? Phone { get; set; }

    public int SheetNumber { get; set; }
    public string VictoryType { get; set; } = "Full"; // Full / Corners / Line

    public decimal PrizeAmount { get; set; } // NUEVO

    public string? Notes { get; set; }
}
