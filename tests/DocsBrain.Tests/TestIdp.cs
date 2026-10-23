using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace DocsBrain.Tests;

/// <summary>A stand-in for the external OpenID Connect authority (Keycloak in the ecosystem) that signs service tokens.</summary>
public sealed class TestIdp
{
    public const string Issuer = "http://idp.test/realms/greenhaus";
    public const string Audience = "docs-brain";
    public const string ServiceRole = "docs-brain.assistant";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test" };

    public string Token(string[]? roles = null, string audience = Audience)
    {
        var realmAccess = JsonSerializer.Serialize(new { roles = roles ?? [ServiceRole] });
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: [new Claim("sub", "greenhaus-assistant"), new Claim("realm_access", realmAccess, JsonClaimValueTypes.Json)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>The API on the shared PostgreSQL, trusting this authority, with the offline mock AI provider.</summary>
    public WebApplicationFactory<Program> CreateFactory(PostgresFixture postgres) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", postgres.ConnectionString);
            builder.UseSetting("Ai:Provider", "mock");
            builder.UseSetting("Search:MinSimilarity", "0.2");
            builder.UseSetting("Auth:Authority", Issuer);
            builder.UseSetting("Auth:Audience", Audience);
            // The authority is not reachable in tests: hand the scheme the signing key and skip metadata discovery.
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>("External", o =>
            {
                o.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                o.TokenValidationParameters.IssuerSigningKey = _key;
            }));
        });
}
