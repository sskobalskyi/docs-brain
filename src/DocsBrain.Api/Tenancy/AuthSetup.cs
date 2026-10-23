using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocsBrain.Api.Tenancy;

/// <summary>Trust in an external OpenID Connect authority, used by other services calling this API.</summary>
public sealed class AuthOptions
{
    /// <summary>Authority that serves the signing keys. Empty disables external tokens.</summary>
    public string? Authority { get; set; }

    /// <summary>Issuer that appears in the tokens, when it differs from <see cref="Authority"/> (Docker host names).</summary>
    public string? Issuer { get; set; }

    public string Audience { get; set; } = "docs-brain";

    /// <summary>Realm role that lets a caller act for any tenant named in the <c>X-Tenant-Id</c> header.</summary>
    public string ServiceRole { get; set; } = "docs-brain.assistant";
}

public static class AuthSetup
{
    private const string External = "External";
    private const string Smart = "Smart";

    public static IServiceCollection AddDocsBrainAuth(this IServiceCollection services, IConfiguration configuration, JwtOptions devJwt)
    {
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));

        // The dev token (symmetric key, Development only) and tokens of the external authority are told
        // apart by issuer; each is validated only by its own scheme.
        services.AddAuthentication(Smart)
            .AddPolicyScheme(Smart, null, o => o.ForwardDefaultSelector = http =>
            {
                var auth = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
                var header = http.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(auth.Authority) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return JwtBearerDefaults.AuthenticationScheme;

                var handler = new JwtSecurityTokenHandler();
                var raw = header["Bearer ".Length..].Trim();
                return handler.CanReadToken(raw) && handler.ReadJwtToken(raw).Issuer != devJwt.Issuer
                    ? External
                    : JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o => o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = devJwt.Issuer,
                ValidateAudience = false,
                IssuerSigningKey = JwtSetup.SigningKey(devJwt),
            })
            .AddJwtBearer(External, _ => { });

        services.AddOptions<JwtBearerOptions>(External).Configure<IOptions<AuthOptions>>((o, options) =>
        {
            var auth = options.Value;
            o.Authority = string.IsNullOrEmpty(auth.Authority) ? null : auth.Authority;
            o.RequireHttpsMetadata = auth.Authority?.StartsWith("https", StringComparison.OrdinalIgnoreCase) ?? true;
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = string.IsNullOrEmpty(auth.Issuer) ? auth.Authority : auth.Issuer,
                ValidAudience = auth.Audience,
                ValidateAudience = true,
                RoleClaimType = "role",
                NameClaimType = "sub",
            };
            o.Events = new JwtBearerEvents { OnTokenValidated = ctx => FlattenRealmRoles(ctx.Principal) };
        });

        // Every authenticated caller must resolve to a tenant; otherwise the request is forbidden.
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.Resource is HttpContext http && HttpTenantContext.Resolve(http) is not null)
                .Build());

        return services;
    }

    // Keycloak keeps realm roles inside the `realm_access` claim as JSON: {"roles":["a","b"]}.
    private static Task FlattenRealmRoles(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is ClaimsIdentity identity && identity.FindFirst("realm_access")?.Value is { } json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
                foreach (var role in roles.EnumerateArray())
                    if (role.GetString() is { } name)
                        identity.AddClaim(new Claim("role", name));
        }

        return Task.CompletedTask;
    }
}
