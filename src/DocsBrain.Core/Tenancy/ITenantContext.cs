namespace DocsBrain.Core.Tenancy;

public interface ITenantContext
{
    /// <summary>The tenant of the current request, or <c>null</c> when the caller has no tenant claim.</summary>
    Guid? TenantId { get; }
}

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
