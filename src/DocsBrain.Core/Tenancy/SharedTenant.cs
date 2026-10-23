namespace DocsBrain.Core.Tenancy;

/// <summary>
/// Reserved tenant that owns the shared corpus, readable by every tenant and writable only through the
/// service-only endpoints. It can never be the tenant of a caller.
/// </summary>
public static class SharedTenant
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
