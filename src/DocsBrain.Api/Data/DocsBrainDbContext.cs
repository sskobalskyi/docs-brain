using DocsBrain.Core.Documents;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace DocsBrain.Api.Data;

public sealed class DocsBrainDbContext(DbContextOptions<DocsBrainDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    public static readonly Guid AcmeTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid GlobexTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();

    // Read at query time. A caller without a tenant matches nothing (fail closed).
    private Guid CurrentTenantId => tenant.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200);
            e.HasData(
                new Tenant { Id = AcmeTenantId, Name = "Acme" },
                new Tenant { Id = GlobexTenantId, Name = "Globex" });
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Title).HasMaxLength(500);
            e.HasIndex(d => d.TenantId);
            e.Property(d => d.FileName).HasMaxLength(500);
            e.HasQueryFilter(d => d.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.DocumentId, c.Index }).IsUnique();
            e.HasOne<Document>().WithMany().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => c.TenantId == CurrentTenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State != EntityState.Added) continue;

            entry.Entity.TenantId = tenant.TenantId
                ?? throw new InvalidOperationException("Cannot save a tenant-owned entity without a tenant.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
