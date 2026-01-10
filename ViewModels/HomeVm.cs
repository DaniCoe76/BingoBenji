using BingoBenji.Models;

namespace BingoBenji.ViewModels;

public class HomeVm
{
    public BingoGeneration? ActiveGeneration { get; set; }
    public int TotalSheets { get; set; }
    public int StockUnassigned { get; set; }
    public int SoldCount { get; set; }

    public string? SearchGenerationCode { get; set; }
    public int? SearchSheetNumber { get; set; }
}
