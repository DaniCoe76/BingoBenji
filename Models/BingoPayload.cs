namespace BingoBenji.Models;

public class BingoSheetPayload
{
    public List<BingoCardPayload> Cards { get; set; } = new();
}

public class BingoCardPayload
{
    // 5x5 números (SIN casilla free, porque pediste 1..90)
    public int[][] Grid { get; set; } = Array.Empty<int[]>();
}
