using BingoBenji.Models;

namespace BingoBenji.ViewModels;

public class PreviewSelectionVm
{
    public BingoGeneration ActiveGeneration { get; set; } = new();
    public int StockUnassigned { get; set; }

    public int Quantity { get; set; } = 1; // cuantos quieres ver
    public List<BingoSheet> PreviewSheets { get; set; } = new();

    public string? Message { get; set; }
}
