using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Tenancy;
using DocsBrain.Core.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace DocsBrain.Api.Seed;

/// <summary>Loads the sample documents in <c>Seed/&lt;tenant&gt;/</c> for tenants that have no documents yet.</summary>
public sealed class DemoSeeder(IServiceProvider services, ILogger<DemoSeeder> logger)
{
    private static readonly (Guid TenantId, string Folder)[] Tenants =
    [
        (DocsBrainDbContext.AcmeTenantId, "acme"),
        (DocsBrainDbContext.GlobexTenantId, "globex"),
    ];

    public static string SeedRoot => Path.Combine(AppContext.BaseDirectory, "Seed");

    public async Task SeedAsync(CancellationToken ct)
    {
        foreach (var (tenantId, folder) in Tenants)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            await using var db = new DocsBrainDbContext(
                sp.GetRequiredService<DbContextOptions<DocsBrainDbContext>>(), new FixedTenantContext(tenantId));
            if (await db.Documents.AnyAsync(ct)) continue;

            var ingestion = new IngestionService(
                db,
                sp.GetServices<ITextExtractor>(),
                sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
                sp.GetRequiredService<ILogger<IngestionService>>());

            foreach (var file in Directory.GetFiles(Path.Combine(SeedRoot, folder)).Order())
            {
                await using var stream = File.OpenRead(file);
                var result = await ingestion.IngestAsync(Path.GetFileName(file), stream, ct);
                if (result.IsSuccess) logger.LogInformation("Seeded {File} for {Tenant}.", Path.GetFileName(file), folder);
                else logger.LogWarning("Could not seed {File}: {Error}", file, result.Error.Message);
            }
        }
    }
}
