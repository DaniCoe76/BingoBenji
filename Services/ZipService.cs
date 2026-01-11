using System.IO.Compression;

namespace BingoBenji.Services;

public class ZipService
{
    public byte[] BuildZip(List<(string FileName, byte[] Content)> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var f in files)
            {
                var entry = zip.CreateEntry(f.FileName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(f.Content, 0, f.Content.Length);
            }
        }
        return ms.ToArray();
    }
}
