using DocsBrain.Api.Ai;
using DocsBrain.Api.Data;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;

namespace DocsBrain.Api.Search;

public sealed record SearchHit(Guid DocumentId, string Title, int ChunkIndex, string Text, double Score);

public sealed record ScoredChunk(Guid Id, double Score);

/// <summary>
/// Hybrid search: vector similarity and full-text rankings are merged with Reciprocal Rank Fusion.
/// If the embedding provider is unavailable the search degrades to keyword-only.
/// </summary>
public sealed class SearchService(
    DocsBrainDbContext db,
    ITenantContext tenant,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    ILogger<SearchService> logger)
{
    private const int CandidatesPerRanking = 20;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Search requires a tenant.");
        var keywords = string.Join(" or ", Tokenizer.Tokens(query).Distinct());
        if (keywords.Length == 0) return [];

        var queryVector = await TryEmbedAsync(query, ct);

        // The tenant is passed explicitly because raw SQL bypasses the EF query filter.
        var scored = queryVector is null
            ? await db.Database.SqlQuery<ScoredChunk>($"""
                SELECT c."Id", ts_rank_cd(c."SearchVector", q)::float8 AS "Score"
                FROM "Chunks" c, websearch_to_tsquery('simple', {keywords}) q
                WHERE c."TenantId" = {tenantId} AND c."SearchVector" @@ q
                ORDER BY "Score" DESC LIMIT {limit}
                """).ToListAsync(ct)
            : await db.Database.SqlQuery<ScoredChunk>($"""
                WITH vec AS (
                    SELECT "Id", row_number() OVER (ORDER BY "Embedding" <=> {queryVector}) AS rnk
                    FROM "Chunks"
                    WHERE "TenantId" = {tenantId} AND "Embedding" IS NOT NULL
                    ORDER BY "Embedding" <=> {queryVector} LIMIT {CandidatesPerRanking}),
                kw AS (
                    SELECT c."Id", row_number() OVER (ORDER BY ts_rank_cd(c."SearchVector", q) DESC) AS rnk
                    FROM "Chunks" c, websearch_to_tsquery('simple', {keywords}) q
                    WHERE c."TenantId" = {tenantId} AND c."SearchVector" @@ q
                    ORDER BY ts_rank_cd(c."SearchVector", q) DESC LIMIT {CandidatesPerRanking})
                SELECT COALESCE(vec."Id", kw."Id") AS "Id",
                       (COALESCE(1.0 / (60 + vec.rnk), 0) + COALESCE(1.0 / (60 + kw.rnk), 0))::float8 AS "Score"
                FROM vec FULL JOIN kw ON vec."Id" = kw."Id"
                ORDER BY "Score" DESC LIMIT {limit}
                """).ToListAsync(ct);

        if (scored.Count == 0) return [];

        // Loading through the DbSet applies the tenant query filter a second time.
        var ids = scored.Select(s => s.Id).ToList();
        var chunks = await db.Chunks.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var titles = await db.Documents
            .Where(d => chunks.Values.Select(c => c.DocumentId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Title, ct);

        return scored
            .Where(s => chunks.ContainsKey(s.Id))
            .Select(s => new SearchHit(
                chunks[s.Id].DocumentId,
                titles.GetValueOrDefault(chunks[s.Id].DocumentId, ""),
                chunks[s.Id].Index,
                chunks[s.Id].Text,
                s.Score))
            .ToList();
    }

    private async Task<Vector?> TryEmbedAsync(string query, CancellationToken ct)
    {
        try
        {
            var result = await embeddings.GenerateAsync([query], cancellationToken: ct);
            return new Vector(result[0].Vector.ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Embedding provider unavailable, falling back to keyword search.");
            return null;
        }
    }
}
