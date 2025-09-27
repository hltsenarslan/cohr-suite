using Microsoft.EntityFrameworkCore;

namespace File.Api.Infrastructure;

public class FilesDbContext(DbContextOptions<FilesDbContext> opts) : DbContext(opts)
{
    public DbSet<FileRecord> Files => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FileRecord>().HasKey(x => x.Id);
        b.Entity<FileRecord>().Property(x => x.ContentType).HasMaxLength(256);
        b.Entity<FileRecord>().Property(x => x.RelPath).HasMaxLength(1024);
        b.Entity<FileRecord>().HasIndex(x => new { x.TenantId, x.IsSecure });
    }
}

public class FileRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool IsSecure { get; set; }
    public string RelPath { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string Sha256 { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? OriginalName { get; set; }
}