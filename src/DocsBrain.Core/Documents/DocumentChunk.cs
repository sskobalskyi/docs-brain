using DocsBrain.Core.Tenancy;

namespace DocsBrain.Core.Documents;

public sealed class DocumentChunk : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int Index { get; set; }
    public required string Text { get; set; }

    /// <summary>Null when no embedding provider was available at ingestion time.</summary>
    public Pgvector.Vector? Embedding { get; set; }
}
