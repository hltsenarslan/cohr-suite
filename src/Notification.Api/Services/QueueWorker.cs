using Microsoft.EntityFrameworkCore;
using Notification.Api.Infrastructure;

namespace Notification.Api.Services;

public class QueueWorker(ILogger<QueueWorker> logger, IServiceProvider sp, IConfiguration cfg, IHttpClientFactory hcf)
    : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    private readonly string _fileApiBase = cfg["FileApi:BaseUrl"] ?? "http://file-api:8080";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk başta da bir tur çalışsın
        await ProcessOnce(stoppingToken);

        var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOnce(stoppingToken);
        }
    }

    public async Task ProcessOnce(CancellationToken ct)
    {
        try
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotifDbContext>();
            var mailer = scope.ServiceProvider.GetRequiredService<IMailer>();
            var http = hcf.CreateClient("file");

            // yarış engelle: waiting & StartedAt null olan ilk 20 kaydı “başlat”
            var batch = await db.MailQueue
                .Where(m => m.Status == "waiting" && m.StartedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            foreach (var m in batch)
            {
                m.StartedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            foreach (var item in batch)
            {
                try
                {
                    var setting = await db.MailSettings.FirstOrDefaultAsync(s => s.TenantId == item.TenantId, ct);
                    if (setting is null)
                        throw new InvalidOperationException("mail_setting_not_found");

                    await db.Entry(item).Collection(i => i.Attachments).LoadAsync(ct);

                    // max 5 enforce
                    var attachList = item.Attachments.Take(5).ToList();

                    // File.Api'dan indir
                    var streams = new List<(Stream, string)>();
                    foreach (var a in attachList)
                    {
                        var url = $"{_fileApiBase}/api/files/{a.FileId}";
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);
                        // tenant güvenliği için header forward edelim
                        var res = await http.SendAsync(req, ct);
                        res.EnsureSuccessStatusCode();
                        var ms = new MemoryStream();
                        await res.Content.CopyToAsync(ms, ct);

                        // Content-Disposition: attachment; filename="report.pdf"
                        var filename = a.FileName;
                        if (string.IsNullOrWhiteSpace(filename) &&
                            res.Content.Headers.ContentDisposition?.FileNameStar != null)
                        {
                            filename = res.Content.Headers.ContentDisposition.FileNameStar.Trim('"');
                        }
                        else if (string.IsNullOrWhiteSpace(filename) &&
                                 res.Content.Headers.ContentDisposition?.FileName != null)
                        {
                            filename = res.Content.Headers.ContentDisposition.FileName.Trim('"');
                        }
                        filename ??= $"{a.FileId}{Path.GetExtension(res.Content.Headers.ContentType?.MediaType ?? "")}";


                        streams.Add((ms, filename));
                        
                        
                    }

                    await mailer.SendAsync(setting, item, streams, ct);
                    item.Status = "done";
                    item.FinishedAt = DateTime.UtcNow;
                    item.Error = null;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "queue item failed: {MailId}", item.Id);
                    item.Status = "fail";
                    item.FinishedAt = DateTime.UtcNow;
                    item.Error = ex.Message[..Math.Min(995, ex.Message.Length)];
                }
                finally
                {
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            // Top-level hata: swallow + log
            Console.WriteLine($"[QueueWorker] top-level error: {ex.Message}");
        }
    }
}