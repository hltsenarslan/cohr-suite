using Microsoft.EntityFrameworkCore;
using Notification.Api.Models;

namespace Notification.Api.Infrastructure;

public class NotifDbContext(DbContextOptions<NotifDbContext> opts) : DbContext(opts)
{
    public DbSet<MailSetting> MailSettings => Set<MailSetting>();
    public DbSet<MailQueueItem> MailQueue => Set<MailQueueItem>();
    public DbSet<MailAttachment> MailAttachments => Set<MailAttachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MailQueueItem>()
            .HasMany(m => m.Attachments)
            .WithOne(a => a.Mail)
            .HasForeignKey(a => a.MailId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MailQueueItem>()
            .Property(m => m.Status)
            .HasDefaultValue("waiting");

        // hızlı sorgular için indeksler
        b.Entity<MailQueueItem>().HasIndex(m => new { m.Status, m.StartedAt });
        b.Entity<MailSetting>().HasIndex(s => s.TenantId).IsUnique();
    }
}