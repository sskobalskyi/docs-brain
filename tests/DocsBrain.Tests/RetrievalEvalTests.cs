using System.Text.Json;
using DocsBrain.Api.Ai;
using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Search;
using DocsBrain.Api.Seed;
using DocsBrain.Api.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace DocsBrain.Tests;

/// <summary>
/// Retrieval evals: a small question set run against the seed documents with the offline mock provider,
/// so the numbers are deterministic and need no model. They guard chunking, hybrid search and ranking.
/// </summary>
[Collection("postgres")]
public class RetrievalEvalTests(PostgresFixture postgres, ITestOutputHelper output)
{
    private sealed record EvalCase(string Tenant, string Question, string Document, string Fact);

    private static readonly Dictionary<string, Guid> Tenants = new()
    {
        ["acme"] = DocsBrainDbContext.AcmeTenantId,
        ["globex"] = DocsBrainDbContext.GlobexTenantId,
    };

    [Fact]
    public async Task Retrieval_quality_meets_the_baseline()
    {
        var create = await postgres.CreateIsolatedDatabaseAsync();
        var embeddings = new HashingEmbeddingGenerator();

        foreach (var (folder, tenantId) in Tenants)
        {
            await using var db = create(tenantId);
            var ingestion = new IngestionService(
                db, [new PlainTextExtractor()], embeddings, NullLogger<IngestionService>.Instance);
            foreach (var file in Directory.GetFiles(Path.Combine(DemoSeeder.SeedRoot, folder)))
            {
                await using var stream = File.OpenRead(file);
                Assert.True((await ingestion.IngestAsync(Path.GetFileName(file), stream, default)).IsSuccess);
            }
        }

        var cases = JsonSerializer.Deserialize<List<EvalCase>>(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Evals", "retrieval-cases.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        double hitAt1 = 0, hitAt3 = 0, reciprocalRank = 0, contextRecall = 0;
        foreach (var c in cases)
        {
            var tenantId = Tenants[c.Tenant];
            await using var db = create(tenantId);
            var search = new SearchService(
                db, new FixedTenantContext(tenantId), Options.Create(new SearchOptions { MinSimilarity = 0.2 }),
                embeddings, NullLogger<SearchService>.Instance);

            var hits = await search.SearchAsync(c.Question, 3, default);
            var rank = hits.ToList().FindIndex(h => h.Title == c.Document) + 1;
            var recalled = hits.Any(h => h.Text.Contains(c.Fact, StringComparison.OrdinalIgnoreCase));

            if (rank == 1) hitAt1++;
            if (rank > 0) hitAt3++;
            if (rank > 0) reciprocalRank += 1.0 / rank;
            if (recalled) contextRecall++;
            output.WriteLine($"{(rank > 0 ? rank.ToString() : "-")} {(recalled ? "fact" : "MISS")}  [{c.Tenant}] {c.Question}");
        }

        var n = cases.Count;
        output.WriteLine($"hit@1 {hitAt1 / n:P0}, hit@3 {hitAt3 / n:P0}, MRR {reciprocalRank / n:F2}, context recall@3 {contextRecall / n:P0}");

        Assert.True(hitAt3 / n >= 0.9, $"hit@3 was {hitAt3 / n:P0}");
        Assert.True(reciprocalRank / n >= 0.75, $"MRR was {reciprocalRank / n:F2}");
        Assert.True(contextRecall / n >= 0.9, $"context recall@3 was {contextRecall / n:P0}");
    }
}
