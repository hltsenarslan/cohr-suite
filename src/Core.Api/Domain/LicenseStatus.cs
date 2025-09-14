namespace Core.Api.Domain;

public class LicenseStatus
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "cloud"; // cloud | onprem
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    public string Fingerprint { get; set; } = default!;
    public string? RawInfo { get; set; } // troubleshoot için (maskelenmiş)
}