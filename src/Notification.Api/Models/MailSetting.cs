using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Models;

public class MailSetting
{
    [Key] public Guid TenantId { get; set; }

    [MaxLength(200)] public string Host { get; set; } = default!;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;

    [MaxLength(200)] public string Username { get; set; } = default!;
    [MaxLength(500)] public string Password { get; set; } = default!; // demo: düz metin

    [MaxLength(200)] public string FromName { get; set; } = "No-Reply";
    [MaxLength(200)] public string FromEmail { get; set; } = "no-reply@example.com";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}