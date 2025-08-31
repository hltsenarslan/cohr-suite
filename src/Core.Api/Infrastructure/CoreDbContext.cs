using Core.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Infrastructure;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();

    public DbSet<DomainMapping> DomainMappings => Set<DomainMapping>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<DomainMapping>().HasIndex(x => x.Host).IsUnique();
        b.Entity<DomainMapping>().Property(x => x.Module).HasConversion<string>();
        b.Entity<DomainMapping>().Property(x => x.PathMode).HasConversion<string>();

        b.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Tenant>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<Tenant>().Property(x => x.Slug).HasMaxLength(100);
        // DB default (deterministik model)

        if (!Database.IsNpgsql())
        {
            // SQLite UTC zaten 'now' UTC döner
            b.Entity<Tenant>().Property(x => x.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP"); // veya: "STRFTIME('%Y-%m-%d %H:%M:%f','now')"
        }
        else
        {
            // Postgres
            b.Entity<Tenant>().Property(x => x.CreatedAt)
                .HasDefaultValueSql("NOW() AT TIME ZONE 'utc'");
        }


        b.Entity<TenantDomain>().HasIndex(x => x.Host).IsUnique();
        b.Entity<TenantDomain>()
            .HasOne(x => x.Tenant)
            .WithMany(t => t.Domains)
            .HasForeignKey(x => x.TenantId);

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired();
        });

        b.Entity<UserTenant>(e =>
        {
            e.ToTable("UserTenants");
            e.HasKey(x => new { x.UserId, x.TenantId });
            e.HasOne(x => x.User).WithMany(x => x.UserTenants).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        //SEED ------------------------------------------------------------------

        var firm1 = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
        var firm2 = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8");
        var seedTime = new DateTime(2025, 8, 25, 0, 0, 0, DateTimeKind.Utc); // SABİT

        b.Entity<Tenant>().HasData(
            new Tenant { Id = firm1, Name = "Firm 1", Slug = "firm1", Status = "active", CreatedAt = seedTime },
            new Tenant { Id = firm2, Name = "Firm 2", Slug = "firm2", Status = "active", CreatedAt = seedTime }
        );

        b.Entity<TenantDomain>().HasData(
            new TenantDomain
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333331"), TenantId = firm1, Host = "pys.local",
                IsDefault = true
            },
            new TenantDomain
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333332"), TenantId = firm2, Host = "pay.local",
                IsDefault = true
            }
        );

        // Seed (M1 demo)
        b.Entity<DomainMapping>().HasData(
            new DomainMapping
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Host = "pys.local",
                Module = ModuleKind.performance,
                TenantId = null,
                PathMode = PathMode.slug,
                TenantSlug = null,
                IsActive = true
            },
            new DomainMapping
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Host = "pay.local",
                Module = ModuleKind.compensation,
                TenantId = null,
                PathMode = PathMode.slug,
                TenantSlug = null,
                IsActive = true
            }
        );

        // --- MS5: Auth seedleri -------------------------------------------------
// --- RBAC seed (MS5) ---
        var roleAdminId  = Guid.Parse("0F000000-0000-0000-0000-0000000000A1");
        var roleViewerId = Guid.Parse("0F000000-0000-0000-0000-0000000000A2");

        var userAdminId  = Guid.Parse("0E000000-0000-0000-0000-0000000000B1");
        var userViewerId = Guid.Parse("0E000000-0000-0000-0000-0000000000B2");

// NOT: Bu hash "Pass123$" içindir; Users tablosunda gördüğünle AYNI kalsın.
        const string passHash = "$2a$10$k4V0Ui0s5jJQk9S0iJYt9uYq2WmFQ7Y0yQ9bA4hQv8q1f9o8o0s3C";

// Roles
        b.Entity<Role>().HasData(
            new Role { Id = roleAdminId,  Name = "admin"  },
            new Role { Id = roleViewerId, Name = "viewer" }
        );

// Users
        b.Entity<User>().HasData(
            new User {
                Id = userAdminId,
                Email = "admin@firm1.local",
                PasswordHash = passHash,
                IsActive = true
                // CreatedAt: DB default (CURRENT_TIMESTAMP)
            },
            new User {
                Id = userViewerId,
                Email = "viewer@firm2.local",
                PasswordHash = passHash,
                IsActive = true
            }
        );

// UserTenants (ilişki)
        b.Entity<UserTenant>().HasData(
            new UserTenant { UserId = userAdminId,  TenantId = firm1, RoleId = roleAdminId  },
            new UserTenant { UserId = userViewerId, TenantId = firm2, RoleId = roleViewerId }
        );
// ------------------------------------------------------------------------
    }
}