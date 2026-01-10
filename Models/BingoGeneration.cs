namespace BingoBenji.Models;

public class BingoGeneration
{
    public int Id { get; set; }
    public string GenerationCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
