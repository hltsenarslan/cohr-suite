using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notification.Api.Models;

public class MailQueueItem
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    // comma-separated recipient list (To)
    [MaxLength(2000)] public string ToList { get; set; } = default!;

    [MaxLength(500)] public string Subject { get; set; } = default!;
    public string HtmlBody { get; set; } = default!;

    // waiting | done | fail
    [MaxLength(20)] public string Status { get; set; } = "waiting";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    [MaxLength(1000)] public string? Error { get; set; }

    public List<MailAttachment> Attachments { get; set; } = new();
}

public class MailAttachment
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MailId { get; set; }

    // File.Api dosya kimliği (Guid). Gerekirse dış URL’yi de destekleyebiliriz.
    public Guid FileId { get; set; }

    // opsiyonel: eğer isimlendirme istiyorsan
    [MaxLength(255)] public string? FileName { get; set; }

    [ForeignKey(nameof(MailId))] public MailQueueItem Mail { get; set; } = default!;
}