using System.Security.Cryptography;
using System.Text;

namespace BingoBenji.Services;

public class GenerationCodeService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sin 0/1/I/O para evitar confusión

    public string Create10()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);

        var sb = new StringBuilder(10);
        for (int i = 0; i < 10; i++)
        {
            sb.Append(Alphabet[bytes[i] % Alphabet.Length]);
        }
        return sb.ToString();
    }
}
