namespace BingoBenji.Models;

public class Winner
{
    public int Id { get; set; }
    public string GenerationCode { get; set; } = "";

    public string PlayerName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";
    public string? Phone { get; set; }

    public int SheetNumber { get; set; }
    public string VictoryType { get; set; } = "Full"; // Full / Corners / Line

    public decimal PrizeAmount { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
