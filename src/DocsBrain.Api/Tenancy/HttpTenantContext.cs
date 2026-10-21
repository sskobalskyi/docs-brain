using DocsBrain.Core.Tenancy;

namespace DocsBrain.Api.Tenancy;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string ClaimType = "tenant_id";

    public Guid? TenantId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(ClaimType)?.Value, out var id) ? id : null;
}
