using DocsBrain.Core.Tenancy;

namespace DocsBrain.Api.Tenancy;

/// <summary>A tenant that is not tied to an HTTP request, for seeding and background work.</summary>
public sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}
