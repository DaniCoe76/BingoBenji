using System.IO.Compression;

namespace BingoBenji.Services;

public class ZipService
{
    public byte[] BuildZip(Dictionary<string, byte[]> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kvp in files)
            {
                var entry = zip.CreateEntry(kvp.Key, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(kvp.Value, 0, kvp.Value.Length);
            }
        }
        return ms.ToArray();
    }
}
