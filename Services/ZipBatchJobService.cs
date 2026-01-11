using System.Collections.Concurrent;
using System.IO.Compression;
using BingoBenji.Data;
using BingoBenji.Services;
using Microsoft.EntityFrameworkCore;

namespace BingoBenji.Services;

public class ZipBatchJobService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ZipBatchJobService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    private static readonly ConcurrentDictionary<string, JobInfo> _jobs = new();

    public JobInfo? Get(string jobId)
        => _jobs.TryGetValue(jobId, out var job) ? job : null;

    public string StartJob(string generationCode, bool markUnassignedAsSold)
    {
        // Si ya hay un job corriendo para esa generación, reutilizamos el mismo (evita duplicados)
        var existing = _jobs.Values.FirstOrDefault(j =>
            j.GenerationCode == generationCode && j.Status is JobStatus.Running or JobStatus.Pending);

        if (existing != null)
            return existing.Id;

        var id = Guid.NewGuid().ToString("N");
        var job = new JobInfo
        {
            Id = id,
            GenerationCode = generationCode,
            Status = JobStatus.Pending,
            Progress = 0,
            Message = "Iniciando…",
            CreatedAt = DateTime.UtcNow
        };

        _jobs[id] = job;

        _ = Task.Run(async () =>
        {
            job.Status = JobStatus.Running;
            job.Progress = 1;
            job.Message = "Preparando…";

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider.GetRequiredService<BingoBenjiDbContext>();
                var pdf = scope.ServiceProvider.GetRequiredService<PdfService>();

                // Buscar generación activa por code
                var gen = await db.BingoGenerations.FirstOrDefaultAsync(g => g.GenerationCode == generationCode);
                if (gen == null)
                    throw new Exception("Generación no encontrada.");

                // Traer tablas en orden
                var sheets = await db.BingoSheets
                    .Where(s => s.GenerationId == gen.Id)
                    .OrderBy(s => s.SheetNumber)
                    .ToListAsync();

                if (sheets.Count == 0)
                    throw new Exception("No hay tablas generadas.");

                // Si corresponde, marcar Unassigned -> Sold
                if (markUnassignedAsSold)
                {
                    var now = DateTime.Now;
                    var changed = false;

                    foreach (var s in sheets)
                    {
                        if (s.Status == "Unassigned")
                        {
                            s.Status = "Sold";
                            s.SoldAt = now;
                            changed = true;
                        }
                    }

                    if (changed)
                        await db.SaveChangesAsync();
                }

                // Crear ZIP en disco (para descargar instantáneo al final)
                var tempDir = Path.Combine(Path.GetTempPath(), "BingoBenji");
                Directory.CreateDirectory(tempDir);

                var zipPath = Path.Combine(tempDir, $"BingoBenji_{generationCode}_{DateTime.Now:yyyyMMdd_HHmmss}_{id}.zip");

                job.Message = "Generando PDFs…";
                job.Progress = 2;

                // Generar + zippear incrementalmente
                // IMPORTANT: dejamos el zip abierto y vamos metiendo entries uno por uno
                using (var fs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var total = sheets.Count; // debe ser 1000
                    for (int i = 0; i < total; i++)
                    {
                        var s = sheets[i];

                        // PDF
                        var pdfBytes = pdf.GenerateSheetPdf(s.GenerationCode, s.SheetNumber, s.CardsJson);

                        // Nombre en orden
                        var entryName = $"Gen_{s.GenerationCode}_Tabla_{s.SheetNumber:0000}.pdf";

                        // Entry
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                        await using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                        }

                        // Progreso (2..99)
                        var pct = 2 + (int)Math.Floor((i + 1) * 97.0 / total);
                        job.Progress = Math.Clamp(pct, 2, 99);

                        if ((i + 1) % 20 == 0)
                            job.Message = $"Generando PDF {i + 1}/{total}…";
                    }
                }

                job.ZipPath = zipPath;
                job.Status = JobStatus.Done;
                job.Progress = 100;
                job.Message = "ZIP listo ✅";
                job.CompletedAt = DateTime.UtcNow;

                CleanupOldJobs();
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Error;
                job.Message = ex.Message;
                job.Progress = Math.Min(job.Progress, 99);
            }
        });

        return id;
    }

    public void CleanupOldJobs()
    {
        // Borra jobs antiguos y sus zips (ej: > 2 horas)
        var cutoff = DateTime.UtcNow.AddHours(-2);

        foreach (var kv in _jobs.ToArray())
        {
            var job = kv.Value;

            var doneTime = job.CompletedAt ?? job.CreatedAt;
            if (doneTime < cutoff)
            {
                if (!string.IsNullOrWhiteSpace(job.ZipPath))
                {
                    try { File.Delete(job.ZipPath); } catch { /* ignore */ }
                }

                _jobs.TryRemove(kv.Key, out _);
            }
        }
    }

    public class JobInfo
    {
        public string Id { get; set; } = "";
        public string GenerationCode { get; set; } = "";
        public JobStatus Status { get; set; } = JobStatus.Pending;
        public int Progress { get; set; }
        public string Message { get; set; } = "";
        public string? ZipPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum JobStatus
    {
        Pending,
        Running,
        Done,
        Error
    }
}
