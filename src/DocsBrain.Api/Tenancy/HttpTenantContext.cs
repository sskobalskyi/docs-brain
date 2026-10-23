using DocsBrain.Core.Tenancy;
using Microsoft.Extensions.Options;

namespace DocsBrain.Api.Tenancy;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string ClaimType = "tenant_id";
    public const string HeaderName = "X-Tenant-Id";

    public Guid? TenantId => accessor.HttpContext is { } http ? Resolve(http) : null;

    /// <summary>
    /// The tenant claim of the token. Only a caller holding the service role may instead name a tenant in the
    /// <c>X-Tenant-Id</c> header; for everyone else the header is ignored. No tenant means no access.
    /// </summary>
    public static Guid? Resolve(HttpContext http)
    {
        var tenant = ResolveRaw(http);

        // The shared corpus has its own owner; nobody may act as it through a token or a header.
        return tenant == SharedTenant.Id ? null : tenant;
    }

    private static Guid? ResolveRaw(HttpContext http)
    {
        var user = http.User;
        if (Guid.TryParse(user.FindFirst(ClaimType)?.Value, out var claimed)) return claimed;

        var serviceRole = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.ServiceRole;
        return user.Identity?.IsAuthenticated == true
               && user.IsInRole(serviceRole)
               && Guid.TryParse(http.Request.Headers[HeaderName].ToString(), out var named)
            ? named
            : null;
    }
}
