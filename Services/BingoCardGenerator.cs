using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BingoBenji.Models;

namespace BingoBenji.Services;

public class BingoCardGenerator
{
    public (string json, string sha256hex) GenerateSheetPayloadAndHash()
    {
        var payload = new BingoSheetPayload
        {
            Cards = new List<BingoCardPayload>
            {
                GenerateCard(),
                GenerateCard(),
                GenerateCard(),
                GenerateCard()
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var hash = ComputeSha256Hex(json);
        return (json, hash);
    }

    private BingoCardPayload GenerateCard()
    {
        // 5 rangos (1..90) para BINGO
        var colB = PickUniqueSorted(1, 18, 5);
        var colI = PickUniqueSorted(19, 36, 5);
        var colN = PickUniqueSorted(37, 54, 5);
        var colG = PickUniqueSorted(55, 72, 5);
        var colO = PickUniqueSorted(73, 90, 5);

        var grid = new int[5][];
        for (int r = 0; r < 5; r++)
            grid[r] = new int[5];

        for (int r = 0; r < 5; r++)
        {
            grid[r][0] = colB[r];
            grid[r][1] = colI[r];
            grid[r][2] = colN[r];
            grid[r][3] = colG[r];
            grid[r][4] = colO[r];
        }

        return new BingoCardPayload { Grid = grid };
    }

    private static int[] PickUniqueSorted(int min, int max, int count)
    {
        var set = new HashSet<int>();
        while (set.Count < count)
        {
            int n = RandomNumberGenerator.GetInt32(min, max + 1);
            set.Add(n);
        }
        return set.OrderBy(x => x).ToArray();
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }
}
