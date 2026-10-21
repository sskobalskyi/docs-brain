using DocsBrain.Api.Ai;
using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Search;
using DocsBrain.Core.Tenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocsBrain.Tests;

/// <summary>Runs hybrid search against a real PostgreSQL with pgvector (Testcontainers).</summary>
[Collection("postgres")]
public class HybridSearchTests(PostgresFixture postgres)
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

    private static SearchService Search(DocsBrainDbContext db, Guid tenant, IEmbeddingGenerator<string, Embedding<float>> embeddings) =>
        new(db, new FakeTenant(tenant), Options.Create(new SearchOptions { MinSimilarity = 0.2 }), embeddings, NullLogger<SearchService>.Instance);

    private async Task Ingest(Guid tenant, string fileName, string text, IEmbeddingGenerator<string, Embedding<float>>? embeddings = null)
    {
        await using var db = postgres.CreateDb(tenant);
        var ingestion = new IngestionService(
            db, [new PlainTextExtractor()], embeddings ?? new HashingEmbeddingGenerator(), NullLogger<IngestionService>.Instance);
        var result = await ingestion.IngestAsync(fileName, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), default);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Finds_the_relevant_chunk_and_stays_inside_the_tenant()
    {
        var acme = DocsBrainDbContext.AcmeTenantId;
        var globex = DocsBrainDbContext.GlobexTenantId;
        await Ingest(acme, "parking.txt", "Parking permits are renewed every September.");
        await Ingest(acme, "cafeteria.txt", "The cafeteria opens at eight in the morning.");
        await Ingest(globex, "secret.txt", "Globex parking permits are renewed every March.");

        await using var db = postgres.CreateDb(acme);
        var hits = await Search(db, acme, new HashingEmbeddingGenerator()).SearchAsync("when are parking permits renewed", 5, default);

        Assert.Equal("parking", hits[0].Title);
        Assert.DoesNotContain(hits, h => h.Title == "secret");
    }

    [Fact]
    public async Task Falls_back_to_keyword_search_when_embeddings_are_unavailable()
    {
        var acme = DocsBrainDbContext.AcmeTenantId;
        await Ingest(acme, "warranty.txt", "The warranty covers manufacturing defects for two years.", new BrokenEmbeddings());

        await using var db = postgres.CreateDb(acme);
        var hits = await Search(db, acme, new BrokenEmbeddings()).SearchAsync("warranty defects", 5, default);

        Assert.Contains(hits, h => h.Title == "warranty");
    }
}
