using BingoBenji.Models;

namespace BingoBenji.ViewModels;

public class WinnersListVm
{
    public List<Winner> Items { get; set; } = new();

    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int Total { get; set; }

    public string? FilterGenerationCode { get; set; }
    public string? FilterName { get; set; }
    public int? FilterSheetNumber { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
