using DocsBrain.Core.Tenancy;

namespace DocsBrain.Core.Chat;

/// <summary>LLM usage of one tenant for one UTC day, used to enforce the daily token budget.</summary>
public sealed class UsageRecord : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateOnly Day { get; set; }
    public long Tokens { get; set; }
    public int Requests { get; set; }
}
