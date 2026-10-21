using DocsBrain.Api.Ai;
using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Search;
using DocsBrain.Core.Ingestion;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DocsBrain.Tests;

/// <summary>Runs hybrid search against a real PostgreSQL with pgvector (Testcontainers).</summary>
public class HybridSearchTests : IAsyncLifetime
{
    private sealed class FakeTenant(Guid? id) : ITenantContext
    {
        public Guid? TenantId { get; } = id;
    }

    private sealed class BrokenEmbeddings : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Ollama is down.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:0.8.0-pg17")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection).ExecuteNonQueryAsync();
        }

        // Npgsql caches type info per pool; drop pools opened before the extension existed.
        NpgsqlConnection.ClearAllPools();
        await using var db = CreateDb(DocsBrainDbContext.AcmeTenantId);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DocsBrainDbContext CreateDb(Guid tenant) =>
        new(new DbContextOptionsBuilder<DocsBrainDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), o => o.UseVector()).Options, new FakeTenant(tenant));

    private static IngestionService Ingestion(DocsBrainDbContext db, IEmbeddingGenerator<string, Embedding<float>> embeddings) =>
        new(db, [new PlainTextExtractor()], embeddings, NullLogger<IngestionService>.Instance);

    private static SearchService Search(DocsBrainDbContext db, Guid tenant, IEmbeddingGenerator<string, Embedding<float>> embeddings) =>
        new(db, new FakeTenant(tenant), embeddings, NullLogger<SearchService>.Instance);

    private async Task Ingest(Guid tenant, string fileName, string text, IEmbeddingGenerator<string, Embedding<float>>? embeddings = null)
    {
        await using var db = CreateDb(tenant);
        var result = await Ingestion(db, embeddings ?? new HashingEmbeddingGenerator())
            .IngestAsync(fileName, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), default);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Finds_the_relevant_chunk_and_stays_inside_the_tenant()
    {
        var acme = DocsBrainDbContext.AcmeTenantId;
        var globex = DocsBrainDbContext.GlobexTenantId;
        await Ingest(acme, "refunds.txt", "Refunds are issued within fourteen days of purchase.");
        await Ingest(acme, "shipping.txt", "Shipping takes three to five business days.");
        await Ingest(globex, "secret.txt", "Globex refunds are issued within thirty days.");

        await using var db = CreateDb(acme);
        var hits = await Search(db, acme, new HashingEmbeddingGenerator()).SearchAsync("how long do refunds take", 5, default);

        Assert.Equal("refunds", hits[0].Title);
        Assert.DoesNotContain(hits, h => h.Title == "secret");
    }

    [Fact]
    public async Task Falls_back_to_keyword_search_when_embeddings_are_unavailable()
    {
        var acme = DocsBrainDbContext.AcmeTenantId;
        await Ingest(acme, "warranty.txt", "The warranty covers manufacturing defects for two years.", new BrokenEmbeddings());

        await using var db = CreateDb(acme);
        var hits = await Search(db, acme, new BrokenEmbeddings()).SearchAsync("warranty defects", 5, default);

        Assert.Contains(hits, h => h.Title == "warranty");
    }
}
