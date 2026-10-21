using DocsBrain.Api.Data;
using DocsBrain.Core.Documents;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace DocsBrain.Tests;

public class TenantIsolationTests
{
    private sealed class FakeTenant(Guid? id) : ITenantContext
    {
        public Guid? TenantId { get; } = id;
    }

    private static DocsBrainDbContext CreateDb(string name, Guid? tenant) =>
        new(new DbContextOptionsBuilder<DocsBrainDbContext>().UseInMemoryDatabase(name).Options, new FakeTenant(tenant));

    [Fact]
    public async Task Queries_return_only_the_current_tenants_documents()
    {
        var name = Guid.NewGuid().ToString();
        await using (var acme = CreateDb(name, DocsBrainDbContext.AcmeTenantId))
        {
            acme.Documents.Add(new Document { Title = "Acme handbook" });
            await acme.SaveChangesAsync();
        }
        await using (var globex = CreateDb(name, DocsBrainDbContext.GlobexTenantId))
        {
            globex.Documents.Add(new Document { Title = "Globex policy" });
            await globex.SaveChangesAsync();
        }

        await using var reader = CreateDb(name, DocsBrainDbContext.AcmeTenantId);
        var titles = await reader.Documents.Select(d => d.Title).ToListAsync();

        Assert.Equal(["Acme handbook"], titles);
    }

    [Fact]
    public async Task Caller_without_tenant_sees_nothing_and_cannot_save()
    {
        var name = Guid.NewGuid().ToString();
        await using (var acme = CreateDb(name, DocsBrainDbContext.AcmeTenantId))
        {
            acme.Documents.Add(new Document { Title = "Acme handbook" });
            await acme.SaveChangesAsync();
        }

        await using var anonymous = CreateDb(name, null);
        Assert.Empty(await anonymous.Documents.ToListAsync());

        anonymous.Documents.Add(new Document { Title = "orphan" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.SaveChangesAsync());
    }
}
