namespace BingoBenji.Models;

public class BingoSheet
{
    public int Id { get; set; }

    public int GenerationId { get; set; }
    public string GenerationCode { get; set; } = "";

    public int SheetNumber { get; set; } // 1..1000

    public string Status { get; set; } = "Unassigned"; // Unassigned / Sold
    public DateTime? SoldAt { get; set; }

    public string CardsJson { get; set; } = "";
    public string ContentHash { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}
